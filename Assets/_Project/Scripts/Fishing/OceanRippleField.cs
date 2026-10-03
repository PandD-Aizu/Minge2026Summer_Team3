using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace _Project.Scripts.Fishing
{
    /// <summary>魚影が残した波を共有テクスチャへ描き、水面の反射と屈折へ渡す</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class OceanRippleField : MonoBehaviour
    {
        private const int Capacity = 64;
        private const int Resolution = 512;
        private const float PlaneTolerance = 0.5f;

        private static readonly int FieldId = Shader.PropertyToID("_OceanRippleField");
        private static readonly int AreaId = Shader.PropertyToID("_OceanRippleArea");
        private static readonly int PlaneId = Shader.PropertyToID("_OceanRipplePlane");
        private static readonly int RectId = Shader.PropertyToID("_RippleRect");
        private static readonly int ShapeId = Shader.PropertyToID("_RippleShape");
        private static readonly int WaveId = Shader.PropertyToID("_RippleWave");
        private static readonly int DirectionId = Shader.PropertyToID("_RippleDirection");
        private static OceanRippleField _instance;

        private struct Ripple
        {
            public Vector2 Center;
            public Vector2 Direction;
            public float Age;
            public float Duration;
            public float Wavelength;
            public float Speed;
            public float Radius;
            public float Amplitude;
            public float Directionality;
            public Scene SourceScene;
        }

        private readonly Ripple[] _ripples = new Ripple[Capacity];
        private MaterialPropertyBlock _properties;
        private CommandBuffer _commands;
        private RenderTexture _field;
        private Mesh _quad;
        private Material _material;
        private float _surfaceY;
        private int _nextRipple;
        private bool _published;

        /// <summary>Domain Reloadを無効にした再生でも前回の波とGPU参照を残さない</summary>
        /// <example>UnityがPlay Modeの初期化時に呼ぶ</example>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (_instance != null) Destroy(_instance.gameObject);
            _instance = null;
            ClearGlobals();
        }

        /// <summary>発生済みの波から独立した新しい波を固定容量の配列へ登録する</summary>
        /// <param name="material">波の勾配を加算描画する共有Material</param>
        /// <param name="position">魚影のワールド位置、波の中心は移動に追従しない</param>
        /// <param name="size">魚影の幅、単位はメートル</param>
        /// <param name="velocity">XZ方向の移動速度、単位はメートル毎秒</param>
        /// <param name="strength">基準となる波の高さ、単位はメートル</param>
        /// <param name="duration">ゲーム時間での波の寿命、単位は秒</param>
        /// <param name="isAppearance">出現時の円形波ならtrue、移動方向の航跡ならfalse</param>
        /// <param name="scene">魚影の所属シーン、アンロードした波は描画しない</param>
        /// <returns>波を登録できた場合はtrue</returns>
        /// <example>FishRippleEmitterから移動距離の閾値を超えたときに呼ぶ</example>
        public static bool Emit(Material material, Vector3 position, float size, Vector2 velocity,
            float strength, float duration, bool isAppearance, Scene scene)
        {
            if (!Application.isPlaying || material == null || !material.shader.isSupported ||
                !scene.IsValid() || !scene.isLoaded || strength <= 0f ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return false;

            // 描画資源は最初の魚影が必要としたときだけ確保する
            if (_instance == null)
            {
                var owner = new GameObject("Ocean Fish Ripple Field") { hideFlags = HideFlags.DontSave };
                SceneManager.MoveGameObjectToScene(owner, scene);
                _instance = owner.AddComponent<OceanRippleField>();
                _instance.Initialize(material, position.y);
            }

            // 現在の海は一枚の水平面なので、別の高さの水域へ同じ波を映さない
            if (!_instance.isActiveAndEnabled || _instance._field == null || !_instance._field.IsCreated() ||
                Mathf.Abs(position.y - _instance._surfaceY) > PlaneTolerance) return false;

            float width = Mathf.Clamp(size, 0.15f, 4f);
            float speed = velocity.magnitude;
            _instance._ripples[_instance._nextRipple] = new Ripple
            {
                Center = new Vector2(position.x, position.z),
                Direction = speed > 0.001f ? velocity / speed : Vector2.zero,
                Duration = Mathf.Clamp(duration, 0.3f, 6f),
                Wavelength = Mathf.Lerp(0.42f, 0.8f, Mathf.Clamp01(width / 2.5f)),
                Speed = Mathf.Lerp(0.65f, 1.05f, Mathf.Clamp01(width / 2.5f)),
                Radius = width * 0.2f,
                Amplitude = Mathf.Clamp(strength, 0f, 0.1f) * Mathf.Sqrt(width) *
                    (isAppearance ? 1f : Mathf.Lerp(0.5f, 1.1f, Mathf.Clamp01(speed / 0.7f))),
                Directionality = isAppearance ? 0f : 1f,
                SourceScene = scene
            };
            _instance._nextRipple = (_instance._nextRipple + 1) % Capacity;
            return true;
        }

        /// <summary>波専用の小さな描画先と再利用するQuadを一度だけ生成する</summary>
        /// <param name="material">各波の描画に使う共有Material</param>
        /// <param name="surfaceY">魚影の基準高さ</param>
        /// <example>最初のEmitで512四方の浮動小数点テクスチャを用意する</example>
        private void Initialize(Material material, float surfaceY)
        {
            _material = material;
            _surfaceY = surfaceY;
            _properties = new MaterialPropertyBlock();
            _commands = new CommandBuffer { name = "Fish ripple surface gradients" };
            _field = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "Ocean fish ripple gradients",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            _field.Create();

            _quad = new Mesh { name = "Fish ripple stamp quad", hideFlags = HideFlags.HideAndDontSave };
            _quad.vertices = new[]
            {
                new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f)
            };
            _quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _quad.UploadMeshData(true);
        }

        /// <summary>ゲーム時間で波を広げ、魚影の更新後に全波の勾配をGPUで合成する</summary>
        /// <example>魚が停止しても残った波は寿命まで広がり、ポーズ中は同じ形を保つ</example>
        private void LateUpdate()
        {
            if (_field == null || _material == null) return;

            // 画面切り替えなどで描画先を失った場合も、次のフレームで波を再描画する
            if (!_field.IsCreated() && !_field.Create())
            {
                if (_published) ClearGlobals();
                _published = false;
                return;
            }

            Vector2 minimum = new(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new(float.NegativeInfinity, float.NegativeInfinity);
            int activeCount = 0;
            for (int i = 0; i < Capacity; i++)
            {
                ref Ripple ripple = ref _ripples[i];
                if (ripple.Duration <= 0f) continue;
                ripple.Age += Time.deltaTime;
                if (ripple.Age >= ripple.Duration || !ripple.SourceScene.isLoaded)
                {
                    ripple.Duration = 0f;
                    continue;
                }

                // 寿命中の最大半径を確保し、広がる波をテクスチャ境界で切らない
                float extent = GetExtent(ripple);
                minimum = Vector2.Min(minimum, ripple.Center - Vector2.one * extent);
                maximum = Vector2.Max(maximum, ripple.Center + Vector2.one * extent);
                activeCount++;
            }

            if (activeCount == 0)
            {
                if (_published) ClearGlobals();
                _published = false;
                return;
            }

            // XZを独立に詰めて横に長い釣り場でも波長の解像度を保つ
            Vector2 center = (minimum + maximum) * 0.5f;
            Vector2 span = Vector2.Max(maximum - minimum + Vector2.one, Vector2.one * 12f);
            Vector2 texel = span / Resolution;
            center = new Vector2(Mathf.Round(center.x / texel.x) * texel.x,
                Mathf.Round(center.y / texel.y) * texel.y);
            minimum = center - span * 0.5f;
            Vector4 area = new(minimum.x, minimum.y, 1f / span.x, 1f / span.y);

            _commands.Clear();
            _commands.SetRenderTarget(_field);
            _commands.ClearRenderTarget(false, true, Color.clear);
            for (int i = 0; i < Capacity; i++)
            {
                Ripple ripple = _ripples[i];
                if (ripple.Duration <= 0f) continue;
                _properties.SetVector(AreaId, area);
                _properties.SetVector(RectId, new Vector4(ripple.Center.x, ripple.Center.y, GetExtent(ripple), 0f));
                _properties.SetVector(ShapeId, new Vector4(ripple.Age, ripple.Duration, ripple.Wavelength, ripple.Speed));
                _properties.SetVector(WaveId, new Vector4(ripple.Radius, ripple.Amplitude, 0f, 0f));
                _properties.SetVector(DirectionId, new Vector4(ripple.Direction.x, ripple.Direction.y, ripple.Directionality, 0f));
                _commands.DrawMesh(_quad, Matrix4x4.identity, _material, 0, 0, _properties);
            }

            // カメラの枚数に依存せず一度だけ描画し、水面からは一回の参照で利用する
            _commands.SetGlobalTexture(FieldId, _field);
            _commands.SetGlobalVector(AreaId, area);
            _commands.SetGlobalVector(PlaneId, new Vector4(_surfaceY, PlaneTolerance, 1f, 0f));
            RenderTexture previous = RenderTexture.active;
            Graphics.ExecuteCommandBuffer(_commands);
            RenderTexture.active = previous;
            _published = true;
        }

        /// <summary>波の外側の包絡線を含めた描画範囲を返す</summary>
        /// <param name="ripple">範囲を調べる波</param>
        /// <returns>発生位置からQuad端までのメートル値</returns>
        /// <example>波の寿命中は同じ範囲を使い、解像度の急変を抑える</example>
        private static float GetExtent(Ripple ripple) =>
            ripple.Radius + ripple.Duration * ripple.Speed + ripple.Wavelength;

        /// <summary>海から解放済みテクスチャや前シーンの波を参照させない</summary>
        /// <example>最後の波の消滅時とPlay Mode終了時に呼ぶ</example>
        private static void ClearGlobals()
        {
            Shader.SetGlobalTexture(FieldId, Texture2D.blackTexture);
            Shader.SetGlobalVector(AreaId, Vector4.zero);
            Shader.SetGlobalVector(PlaneId, Vector4.zero);
        }

        /// <summary>描画の無効化中に最後の波が海面へ焼き付いたままになるのを防ぐ</summary>
        /// <example>共有オブジェクトを無効化した場合も水面の法線を元へ戻す</example>
        private void OnDisable()
        {
            if (_instance == this) ClearGlobals();
            System.Array.Clear(_ripples, 0, Capacity);
            _nextRipple = 0;
            _published = false;
        }

        /// <summary>シーン破棄時に共有GPU資源と波の参照をすべて解放する</summary>
        /// <example>釣り場からキャンプへ移動したときにも古い波を残さない</example>
        private void OnDestroy()
        {
            if (_instance == this)
            {
                ClearGlobals();
                _instance = null;
            }

            _commands?.Release();
            if (_field != null)
            {
                _field.Release();
                Destroy(_field);
            }

            if (_quad != null) Destroy(_quad);
        }
    }
}

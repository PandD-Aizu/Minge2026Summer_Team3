using UnityEngine;
using UnityEngine.Rendering;

namespace Minge2026.Weather
{
    /// <summary>プレイヤー付近の降雨と再利用可能な落雷の描画を担当する</summary>
    [DisallowMultipleComponent]
    public sealed class WeatherPrecipitation : MonoBehaviour
    {
        private const float RainHeight = 14f;
        private const float RainRate = 1450f;
        private const float FlashDuration = 0.38f;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int GroundYId = Shader.PropertyToID("_GroundY");

        private readonly Vector3[] _boltPoints = new Vector3[10];
        private readonly Vector3[] _branchPoints = new Vector3[4];
        private GameObject _rainObject;
        private GameObject _lightningObject;
        private ParticleSystem _rain;
        private ParticleSystemRenderer _rainRenderer;
        private LineRenderer _bolt;
        private LineRenderer _halo;
        private LineRenderer _branch;
        private Light _flashLight;
        private MaterialPropertyBlock _rainProperties;
        private MaterialPropertyBlock _lightningProperties;
        private Bounds _groundBounds;
        private Color _boltColor = new Color(4.6f, 5.8f, 8f, 1f);
        private float _intensity;
        private float _flashAge;
        private bool _flashActive;
        private bool _configured;

        /// <summary>共有素材と地面の範囲を受け取り、雨と雷の描画オブジェクトを一度だけ用意する</summary>
        /// <param name="rainMaterial">頂点色と透明度に対応する雨の共有素材</param>
        /// <param name="lightningMaterial">発光する雷の共有素材</param>
        /// <param name="groundBounds">地表を上面とするワールド座標の範囲</param>
        /// <example>precipitation.Configure(rainMaterial, lightningMaterial, terrain.bounds)</example>
        public void Configure(Material rainMaterial, Material lightningMaterial, Bounds groundBounds)
        {
            _groundBounds = groundBounds;
            _rainProperties ??= new MaterialPropertyBlock();
            _lightningProperties ??= new MaterialPropertyBlock();

            // シーン内に専用オブジェクトを一度だけ生成し、設定変更では再利用する
            if (_rain == null) CreateRain();
            if (_bolt == null) CreateLightning();

            _rainRenderer.sharedMaterial = rainMaterial;
            _rainRenderer.enabled = rainMaterial != null;
            _rainProperties.SetFloat(GroundYId, groundBounds.max.y);
            _rainRenderer.SetPropertyBlock(_rainProperties);
            _bolt.sharedMaterial = lightningMaterial;
            _halo.sharedMaterial = lightningMaterial;
            _branch.sharedMaterial = lightningMaterial;

            // 素材の HDR 色を保持し、落雷ごとの明滅には PropertyBlock を使う
            if (lightningMaterial != null)
            {
                if (lightningMaterial.HasProperty(BaseColorId))
                    _boltColor = lightningMaterial.GetColor(BaseColorId);
                else if (lightningMaterial.HasProperty(ColorId))
                    _boltColor = lightningMaterial.GetColor(ColorId);
            }

            _configured = true;
            PositionRain(null);
            SetIntensity(_intensity);
        }

        /// <summary>雨の量を設定し、雨が止んだら残った粒だけを自然に落下させる</summary>
        /// <param name="intensity">降雨強度、0 は停止で 1 は通常の本降り</param>
        /// <example>precipitation.SetIntensity(0.75f)</example>
        public void SetIntensity(float intensity)
        {
            _intensity = Mathf.Clamp01(intensity);
            if (!_configured || !isActiveAndEnabled) return;

            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = RainRate * _intensity;

            // 低い強度でも連続した遷移にし、無効化された素材では粒を生成しない
            if (_intensity > 0.001f && _rainRenderer.sharedMaterial != null)
            {
                if (!_rain.isPlaying) _rain.Play(false);
            }
            else if (_rain.isPlaying)
            {
                _rain.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>降雨範囲を追従させ、ゲーム内時間に従って雷の短い閃光を進める</summary>
        /// <param name="deltaTime">今回進める秒数、0 のとき雷の明滅も停止する</param>
        /// <param name="focus">雨の中心となるプレイヤー、null の場合は地面の中心</param>
        /// <example>precipitation.Tick(Time.deltaTime, player.transform)</example>
        public void Tick(float deltaTime, Transform focus)
        {
            if (!_configured || !isActiveAndEnabled) return;

            // ワールド空間の粒は残し、これから発生する雨だけをプレイヤーへ追従させる
            PositionRain(focus);
            if (!_flashActive) return;

            _flashAge += Mathf.Max(0f, deltaTime);
            if (_flashAge >= FlashDuration)
            {
                HideLightning();
                return;
            }

            // 最初の鋭い閃光、短い間隔、残光の順で一度の落雷を見せる
            float brightness = _flashAge < 0.055f ? 1f
                : _flashAge < 0.105f ? 0.12f
                : Mathf.Pow(1f - Mathf.InverseLerp(0.105f, FlashDuration, _flashAge), 2f);
            ApplyFlash(brightness);
        }

        /// <summary>指定地点へ短い枝を持つ稲妻を表示し、地面付近を一瞬照らす</summary>
        /// <param name="groundPosition">落雷の終点となるワールド座標、ダメージは発生しない</param>
        /// <example>precipitation.Strike(new Vector3(4f, 0.5f, 6f))</example>
        public void Strike(Vector3 groundPosition)
        {
            if (!_configured || !isActiveAndEnabled || _bolt.sharedMaterial == null) return;

            // 縦に落ちる軌道を少し折り、同じ Renderer の頂点だけを更新する
            Vector3 top = groundPosition + new Vector3(Random.Range(-2.5f, 2.5f), 12f, Random.Range(-1f, 1f));
            for (int i = 0; i < _boltPoints.Length; i++)
            {
                float progress = i / (float)(_boltPoints.Length - 1);
                Vector3 point = Vector3.Lerp(top, groundPosition, progress);
                if (i > 0 && i < _boltPoints.Length - 1)
                    point += new Vector3(Random.Range(-0.7f, 0.7f), 0f, Random.Range(-0.25f, 0.25f));
                _boltPoints[i] = point;
            }
            _bolt.SetPositions(_boltPoints);
            _halo.SetPositions(_boltPoints);

            // 主線の中腹から小さな枝を伸ばし、低解像度でも雷の形が読めるようにする
            Vector3 branchStart = _boltPoints[4];
            float direction = Random.value < 0.5f ? -1f : 1f;
            for (int i = 0; i < _branchPoints.Length; i++)
            {
                float progress = i / (float)(_branchPoints.Length - 1);
                _branchPoints[i] = branchStart + new Vector3(direction * progress * 2.7f,
                    -progress * 3.2f, progress * 0.4f);
                if (i > 0 && i < _branchPoints.Length - 1)
                    _branchPoints[i].x += Random.Range(-0.35f, 0.35f);
            }
            _branch.SetPositions(_branchPoints);

            _flashLight.transform.position = groundPosition + Vector3.up * 2.8f;
            _flashAge = 0f;
            _flashActive = true;
            _bolt.enabled = true;
            _halo.enabled = true;
            _branch.enabled = true;
            _flashLight.enabled = true;
            ApplyFlash(1f);
        }

        /// <summary>静的な屋根や地面へ当たると消える降雨の ParticleSystem を生成する</summary>
        /// <example>Configure の初回だけ呼ばれる</example>
        private void CreateRain()
        {
            _rainObject = new GameObject("Weather Rain");
            _rainObject.transform.SetParent(transform, false);
            _rain = _rainObject.AddComponent<ParticleSystem>();
            _rain.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 上空の薄い箱から斜めに落とし、ゲームのポーズと同じ時間軸で更新する
            ParticleSystem.MainModule main = _rain.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.88f, 1.02f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.021f, 0.035f);
            main.startColor = new Color(0.88f, 0.94f, 1f, 0.6f);
            main.maxParticles = 2000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.useUnscaledTime = false;
            // 発生位置が画面上端より高くても地表へ落ちるまでシミュレーションを続ける
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.ShapeModule shape = _rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.3f, 25f);

            ParticleSystem.EmissionModule emission = _rain.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.VelocityOverLifetimeModule velocity = _rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = -2.2f;
            velocity.y = -18f;
            velocity.z = 0.35f;

            // 静的な建物への衝突だけを調べ、動的 Collider の大量照合を避ける
            ParticleSystem.CollisionModule collision = _rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.enableDynamicColliders = false;
            collision.collidesWith = Physics.DefaultRaycastLayers;
            collision.maxCollisionShapes = 128;
            collision.voxelSize = 0.5f;
            collision.bounce = 0f;
            collision.dampen = 1f;
            collision.lifetimeLoss = 1f;
            collision.sendCollisionMessages = false;
            collision.radiusScale = 0.25f;

            _rainRenderer = _rain.GetComponent<ParticleSystemRenderer>();
            _rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            _rainRenderer.velocityScale = 0.024f;
            _rainRenderer.lengthScale = 1.3f;
            _rainRenderer.cameraVelocityScale = 0f;
            _rainRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _rainRenderer.receiveShadows = false;
            _rainRenderer.lightProbeUsage = LightProbeUsage.Off;
            _rainRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>雷の芯、淡い周囲光、枝と地面照明を一度だけ生成する</summary>
        /// <example>Configure の初回だけ呼ばれる</example>
        private void CreateLightning()
        {
            _lightningObject = new GameObject("Weather Lightning");
            _lightningObject.transform.SetParent(transform, false);
            _bolt = CreateLine("Lightning Core", 0.055f, _boltPoints.Length);
            _halo = CreateLine("Lightning Halo", 0.22f, _boltPoints.Length);
            _branch = CreateLine("Lightning Branch", 0.025f, _branchPoints.Length);

            _flashLight = _lightningObject.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.color = new Color(0.65f, 0.78f, 1f);
            _flashLight.range = 15f;
            _flashLight.shadows = LightShadows.None;
            HideLightning();
        }

        /// <summary>既存の親の下にワールド座標で描く細い稲妻の線を生成する</summary>
        /// <param name="objectName">Hierarchy に表示する名前</param>
        /// <param name="width">線の基準となる幅、単位はメートル</param>
        /// <param name="pointCount">折れ線を構成する頂点数</param>
        /// <returns>共有素材を後から設定する無効状態の線</returns>
        /// <example>CreateLine("Lightning Core", 0.055f, 10)</example>
        private LineRenderer CreateLine(string objectName, float width, int pointCount)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(_lightningObject.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = pointCount;
            line.startWidth = width;
            line.endWidth = width * 0.45f;
            line.numCornerVertices = 1;
            line.numCapVertices = 1;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.startColor = Color.white;
            line.endColor = Color.white;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        /// <summary>新しい雨が生まれる箱を地表の範囲内でプレイヤーに追従させる</summary>
        /// <param name="focus">追従する Transform、null のときは地面の中心</param>
        /// <example>PositionRain(player.transform)</example>
        private void PositionRain(Transform focus)
        {
            Vector3 center = focus != null ? focus.position : _groundBounds.center;
            center.x = Mathf.Clamp(center.x, _groundBounds.min.x, _groundBounds.max.x);
            center.z = Mathf.Clamp(center.z, _groundBounds.min.z, _groundBounds.max.z);
            center.y = _groundBounds.max.y + RainHeight;
            _rainObject.transform.SetPositionAndRotation(center, Quaternion.identity);
        }

        /// <summary>共有素材を変更せず、各線と地面の照明に閃光の明るさを反映する</summary>
        /// <param name="brightness">0 から 1 の閃光強度</param>
        /// <example>ApplyFlash(0.5f)</example>
        private void ApplyFlash(float brightness)
        {
            SetLineBrightness(_bolt, brightness);
            SetLineBrightness(_halo, brightness * 0.15f);
            SetLineBrightness(_branch, brightness * 0.8f);
            _flashLight.intensity = brightness * 14f;
        }

        /// <summary>線の発光色と地表クリップ高さを Renderer ごとに設定する</summary>
        /// <param name="line">設定する雷の線</param>
        /// <param name="brightness">色の明るさに掛ける倍率</param>
        /// <example>SetLineBrightness(_halo, 0.15f)</example>
        private void SetLineBrightness(LineRenderer line, float brightness)
        {
            Color color = _boltColor;
            color.a *= brightness;
            _lightningProperties.SetColor(BaseColorId, color);
            _lightningProperties.SetColor(ColorId, color);
            _lightningProperties.SetFloat(GroundYId, _groundBounds.max.y - 0.1f);
            line.SetPropertyBlock(_lightningProperties);
        }

        /// <summary>再利用する雷の線と照明を非表示にし、閃光の進行を止める</summary>
        /// <example>落雷の残光が終わったときに呼ばれる</example>
        private void HideLightning()
        {
            _flashActive = false;
            if (_bolt != null) _bolt.enabled = false;
            if (_halo != null) _halo.enabled = false;
            if (_branch != null) _branch.enabled = false;
            if (_flashLight != null) _flashLight.enabled = false;
        }

        /// <summary>再有効化時に記憶した降雨強度から新しい雨を発生させる</summary>
        /// <example>雨の途中でコンポーネントを無効化してから戻す場合に呼ばれる</example>
        private void OnEnable()
        {
            if (_configured) SetIntensity(_intensity);
        }

        /// <summary>無効化時に残った粒と雷を消し、停止中の描画を防ぐ</summary>
        /// <example>シーン内で天候コンポーネントを無効化するときに呼ばれる</example>
        private void OnDisable()
        {
            if (_rain != null) _rain.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            HideLightning();
        }

        /// <summary>このコンポーネントが生成した子だけを破棄し、共有素材は保持する</summary>
        /// <example>シーン終了時やコンポーネント単体の削除時に呼ばれる</example>
        private void OnDestroy()
        {
            if (_rainObject != null) Destroy(_rainObject);
            if (_lightningObject != null) Destroy(_lightningObject);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Minge2026.Weather
{
    /// <summary>地面の濡れを共有マテリアルへ反映し、雨上がりの水たまりをまとめて描画する</summary>
    [DisallowMultipleComponent]
    public sealed class WeatherGroundSurface : MonoBehaviour
    {
        [Header("濡れた地面")]
        [SerializeField] private Color wetColorMultiplier = new(0.61f, 0.70f, 0.77f, 1f);
        [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.82f;

        [Header("雨上がりの水たまり")]
        [SerializeField, Min(1f)] private float puddleSpacing = 3.4f;
        [SerializeField, Range(0f, 1f)] private float puddleCoverage = 0.82f;
        [SerializeField, Min(1)] private int maximumPuddles = 512;
        [SerializeField] private Vector2 puddleRadiusRange = new(0.65f, 1.35f);
        [SerializeField, Min(0.005f)] private float maximumHeightDifference = 0.035f;
        [SerializeField] private int placementSeed = 731;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int PuddleAmountId = Shader.PropertyToID("_PuddleAmount");
        private static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");
        private static readonly int WetnessId = Shader.PropertyToID("_Wetness");

        private sealed class GroundMaterial
        {
            public Material Original;
            public Material Runtime;
            public int ColorProperty;
            public Color DryColor;
            public int SmoothnessProperty;
            public float DrySmoothness;
        }

        private sealed class GroundRenderer
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Runtime;
        }

        private readonly List<GroundMaterial> _materials = new();
        private readonly List<GroundRenderer> _renderers = new();
        private readonly HashSet<Collider> _groundColliders = new();
        private Material _puddleMaterial;
        private Mesh _puddleMesh;
        private MeshRenderer _puddleRenderer;
        private GameObject _puddleObject;
        private float _wetness;
        private float _puddleAmount;
        private float _rainIntensity;
        private float _appliedWetness = -1f;

        /// <summary>対象の地面を登録し、専用マテリアルと静的な水たまりメッシュを一度だけ作る</summary>
        /// <param name="groundRenderers">濡らす地面のRenderer、同じGameObjectのColliderだけを水たまりの接地に使う</param>
        /// <param name="puddleMaterial">Minge/Weather/HD2D Puddleを使う元のマテリアル</param>
        /// <param name="groundBounds">水たまり候補を配置するワールド座標の範囲</param>
        /// <example>surface.Configure(grassRenderers, puddleMaterial, stageBounds)</example>
        public void Configure(Renderer[] groundRenderers, Material puddleMaterial, Bounds groundBounds)
        {
            ReleaseResources();
            if (groundRenderers == null || groundRenderers.Length == 0) return;

            // 同じ地面マテリアルの複製を全タイルで共有し、毎フレームの更新数を抑える
            var instances = new Dictionary<Material, GroundMaterial>();
            var uniqueRenderers = new HashSet<Renderer>();
            foreach (Renderer ground in groundRenderers)
            {
                if (ground == null || !uniqueRenderers.Add(ground)) continue;
                Material[] originals = ground.sharedMaterials;
                Material[] runtime = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    if (original == null) continue;
                    if (!instances.TryGetValue(original, out GroundMaterial material))
                    {
                        material = CreateGroundMaterial(original);
                        instances.Add(original, material);
                        _materials.Add(material);
                    }

                    runtime[i] = material.Runtime;
                }

                _renderers.Add(new GroundRenderer { Renderer = ground, Original = originals, Runtime = runtime });
                ground.sharedMaterials = runtime;
                foreach (Collider collider in ground.GetComponents<Collider>())
                {
                    if (collider.enabled && !collider.isTrigger) _groundColliders.Add(collider);
                }
            }

            // 地面以外の屋根や海はRaycastで除外し、物理問い合わせは初期化時だけ行う
            if (puddleMaterial != null && _groundColliders.Count > 0 && groundBounds.size.x > 0f && groundBounds.size.z > 0f)
            {
                _puddleMaterial = new Material(puddleMaterial)
                {
                    name = "Weather puddles (runtime)",
                    hideFlags = HideFlags.DontSave
                };
                BuildPuddleMesh(groundBounds);
            }

            ApplyState(isActiveAndEnabled);
        }

        /// <summary>濡れ、水たまり、雨の強さを0から1で受け取り、地面と水面を滑らかに変化させる</summary>
        /// <param name="wetness">地面の濡れ具合、0で元の色と滑らかさへ戻る</param>
        /// <param name="puddleAmount">雨上がりの水たまり量、0で水面を非表示にする</param>
        /// <param name="rainIntensity">水面の波紋を強める雨の強さ</param>
        /// <example>surface.SetWetness(1f, 0.8f, 0f)</example>
        public void SetWetness(float wetness, float puddleAmount, float rainIntensity)
        {
            _wetness = Mathf.Clamp01(wetness);
            _puddleAmount = Mathf.Clamp01(puddleAmount);
            _rainIntensity = Mathf.Clamp01(rainIntensity);
            ApplyState(isActiveAndEnabled);
        }

        /// <summary>元のアセットを変更せず、濡れに必要な初期値と複製を用意する</summary>
        /// <param name="original">地面が共有している元のマテリアル</param>
        /// <returns>元の色と滑らかさを保持した実行時マテリアル</returns>
        /// <example>CreateGroundMaterial(grass.sharedMaterial)</example>
        private static GroundMaterial CreateGroundMaterial(Material original)
        {
            int colorProperty = original.HasProperty(BaseColorId) ? BaseColorId :
                original.HasProperty(ColorId) ? ColorId : -1;
            int smoothnessProperty = original.HasProperty(SmoothnessId) ? SmoothnessId :
                original.HasProperty(GlossinessId) ? GlossinessId : -1;
            return new GroundMaterial
            {
                Original = original,
                Runtime = new Material(original) { name = original.name + " (weather)", hideFlags = HideFlags.DontSave },
                ColorProperty = colorProperty,
                DryColor = colorProperty != -1 ? original.GetColor(colorProperty) : Color.white,
                SmoothnessProperty = smoothnessProperty,
                DrySmoothness = smoothnessProperty != -1 ? original.GetFloat(smoothnessProperty) : 0f
            };
        }

        /// <summary>地面の共有マテリアルだけを更新し、水たまりの表示量を渡す</summary>
        /// <param name="visible">コンポーネントが有効な場合だけ濡れと水面を描く</param>
        /// <example>ApplyState(isActiveAndEnabled)</example>
        private void ApplyState(bool visible)
        {
            float wetness = visible ? _wetness : 0f;
            if (Mathf.Abs(_appliedWetness - wetness) > 0.0005f)
            {
                foreach (GroundMaterial material in _materials)
                {
                    if (material.Runtime == null) continue;
                    if (material.ColorProperty != -1)
                    {
                        Color wetColor = material.DryColor * wetColorMultiplier;
                        wetColor.a = material.DryColor.a;
                        material.Runtime.SetColor(material.ColorProperty, Color.Lerp(material.DryColor, wetColor, wetness));
                    }

                    if (material.SmoothnessProperty != -1)
                    {
                        material.Runtime.SetFloat(material.SmoothnessProperty,
                            Mathf.Lerp(material.DrySmoothness, Mathf.Max(material.DrySmoothness, wetSmoothness), wetness));
                    }
                }

                _appliedWetness = wetness;
            }

            // 水面全体で1枚のマテリアルを使い、雨上がりも個別のUpdateを作らない
            if (_puddleMaterial != null)
            {
                _puddleMaterial.SetFloat(PuddleAmountId, visible ? _puddleAmount : 0f);
                _puddleMaterial.SetFloat(RainIntensityId, visible ? _rainIntensity : 0f);
                _puddleMaterial.SetFloat(WetnessId, wetness);
            }

            if (_puddleRenderer != null) _puddleRenderer.enabled = visible && _puddleAmount > 0.001f;
        }

        /// <summary>ジッター格子上の水たまりを地面へ接地させ、1つの静的メッシュへ結合する</summary>
        /// <param name="bounds">候補位置を走査するワールド範囲</param>
        /// <example>BuildPuddleMesh(stageBounds)</example>
        private void BuildPuddleMesh(Bounds bounds)
        {
            var random = new System.Random(placementSeed);
            var vertices = new List<Vector3>();
            var uv = new List<Vector3>();
            var triangles = new List<int>();
            float spacing = Mathf.Max(1f, puddleSpacing);
            float rayTop = bounds.max.y + 20f;
            float rayLength = bounds.size.y + 40f;
            float minRadius = Mathf.Max(0.2f, Mathf.Min(puddleRadiusRange.x, puddleRadiusRange.y));
            float maxRadius = Mathf.Max(minRadius, Mathf.Max(puddleRadiusRange.x, puddleRadiusRange.y));
            int count = 0;

            // プレイヤー側の手前から走査し、上限へ達しても前景の水たまりを確保する
            for (float z = bounds.min.z + spacing * 0.5f; z < bounds.max.z && count < maximumPuddles; z += spacing)
            {
                for (float x = bounds.min.x + spacing * 0.5f; x < bounds.max.x && count < maximumPuddles; x += spacing)
                {
                    if (random.NextDouble() > puddleCoverage) continue;
                    Vector3 center = new(x + ((float)random.NextDouble() - 0.5f) * spacing * 0.65f,
                        0f, z + ((float)random.NextDouble() - 0.5f) * spacing * 0.65f);
                    float radius = Mathf.Lerp(minRadius, maxRadius, (float)random.NextDouble());
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    Vector3 axisX = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    Vector3 axisZ = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * radius *
                        Mathf.Lerp(0.5f, 0.85f, (float)random.NextDouble());
                    if (!TryPlacePuddle(center, axisX, axisZ, rayTop, rayLength, out float height)) continue;

                    center.y = height + 0.014f;
                    float seed = (float)random.NextDouble();
                    int start = vertices.Count;
                    vertices.Add(transform.InverseTransformPoint(center - axisX - axisZ));
                    vertices.Add(transform.InverseTransformPoint(center - axisX + axisZ));
                    vertices.Add(transform.InverseTransformPoint(center + axisX + axisZ));
                    vertices.Add(transform.InverseTransformPoint(center + axisX - axisZ));
                    uv.Add(new Vector3(0f, 0f, seed));
                    uv.Add(new Vector3(0f, 1f, seed));
                    uv.Add(new Vector3(1f, 1f, seed));
                    uv.Add(new Vector3(1f, 0f, seed));
                    triangles.Add(start);
                    triangles.Add(start + 1);
                    triangles.Add(start + 2);
                    triangles.Add(start);
                    triangles.Add(start + 2);
                    triangles.Add(start + 3);
                    count++;
                }
            }

            if (vertices.Count == 0) return;
            _puddleMesh = new Mesh { name = "Weather puddles combined", hideFlags = HideFlags.DontSave };
            _puddleMesh.SetVertices(vertices);
            _puddleMesh.SetUVs(0, uv);
            _puddleMesh.SetTriangles(triangles, 0);
            _puddleMesh.RecalculateBounds();
            _puddleMesh.UploadMeshData(true);

            // 衝突判定を持たない薄い水面なので移動や釣りの判定に影響しない
            _puddleObject = new GameObject("Rain aftermath puddles") { hideFlags = HideFlags.DontSave };
            _puddleObject.transform.SetParent(transform, false);
            _puddleObject.AddComponent<MeshFilter>().sharedMesh = _puddleMesh;
            _puddleRenderer = _puddleObject.AddComponent<MeshRenderer>();
            _puddleRenderer.sharedMaterial = _puddleMaterial;
            _puddleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _puddleRenderer.receiveShadows = true;
            _puddleRenderer.lightProbeUsage = LightProbeUsage.Off;
            _puddleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        /// <summary>中心と輪郭の接地を調べ、崖や段差へ水面がはみ出す候補を除外する</summary>
        /// <param name="center">候補のXZ中心</param>
        /// <param name="axisX">楕円の横半径と方向</param>
        /// <param name="axisZ">楕円の縦半径と方向</param>
        /// <param name="rayTop">遮蔽物も含めて調べるRayの開始高さ</param>
        /// <param name="rayLength">地面より下まで届くRayの長さ</param>
        /// <param name="height">全サンプルの最大地面高さ</param>
        /// <returns>水平な指定地面だけで輪郭全体を支えられればtrue</returns>
        /// <example>TryPlacePuddle(center, axisX, axisZ, rayTop, rayLength, out height)</example>
        private bool TryPlacePuddle(Vector3 center, Vector3 axisX, Vector3 axisZ,
            float rayTop, float rayLength, out float height)
        {
            height = float.NegativeInfinity;
            float lowest = float.PositiveInfinity;
            for (int i = -1; i < 16; i++)
            {
                float angle = Mathf.PI * 2f * i / 16f;
                Vector3 sample = i < 0 ? center : center + axisX * Mathf.Cos(angle) + axisZ * Mathf.Sin(angle);
                sample.y = rayTop;
                if (!Physics.Raycast(sample, Vector3.down, out RaycastHit hit, rayLength,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    !_groundColliders.Contains(hit.collider) || hit.normal.y < 0.97f) return false;

                height = Mathf.Max(height, hit.point.y);
                lowest = Mathf.Min(lowest, hit.point.y);
                if (height - lowest > Mathf.Max(0.005f, maximumHeightDifference)) return false;
            }

            return true;
        }

        /// <summary>無効化中に水面や濡れが残らないよう描画値を一時的に戻す</summary>
        /// <example>天候オブジェクトを無効化したときにUnityが呼ぶ</example>
        private void OnDisable() => ApplyState(false);

        /// <summary>再度有効になったときに最後の天候状態を描画へ戻す</summary>
        /// <example>天候オブジェクトを有効化したときにUnityが呼ぶ</example>
        private void OnEnable() => ApplyState(true);

        /// <summary>元のマテリアル参照へ戻し、実行時だけのGPU資源を破棄する</summary>
        /// <example>再設定時とシーン終了時に呼ぶ</example>
        private void ReleaseResources()
        {
            foreach (GroundRenderer ground in _renderers)
            {
                if (ground.Renderer == null) continue;
                Material[] current = ground.Renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < current.Length && i < ground.Runtime.Length; i++)
                {
                    // 別の処理が差し替えたスロットは上書きせず、自分が所有する参照だけ戻す
                    if (current[i] != ground.Runtime[i]) continue;
                    current[i] = ground.Original[i];
                    changed = true;
                }

                if (changed) ground.Renderer.sharedMaterials = current;
            }

            foreach (GroundMaterial material in _materials) DestroyRuntimeObject(material.Runtime);
            if (_puddleRenderer != null) _puddleRenderer.enabled = false;
            DestroyRuntimeObject(_puddleObject);
            DestroyRuntimeObject(_puddleMesh);
            DestroyRuntimeObject(_puddleMaterial);
            _renderers.Clear();
            _materials.Clear();
            _groundColliders.Clear();
            _puddleObject = null;
            _puddleRenderer = null;
            _puddleMesh = null;
            _puddleMaterial = null;
            _appliedWetness = -1f;
        }

        /// <summary>Play Modeと編集時のどちらでも実行時生成物を残さず解放する</summary>
        /// <param name="runtimeObject">このコンポーネントが生成したオブジェクト</param>
        /// <example>DestroyRuntimeObject(_puddleMesh)</example>
        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject == null) return;
            if (Application.isPlaying) Destroy(runtimeObject);
            else DestroyImmediate(runtimeObject);
        }

        /// <summary>シーンのアンロードやコンポーネント削除時に元の地面を復元する</summary>
        /// <example>Unityが破棄時に呼ぶ</example>
        private void OnDestroy() => ReleaseResources();
    }
}

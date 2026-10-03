using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EditorTools.Ocean
{
    /// <summary>海の確認シーンへ暖かな斜光と冷たい空気感を設定する</summary>
    public static class OceanTranslucentLighting
    {
        private const string ScenePath = "Assets/_Project/Scenes/Develop/OceanLookDev.unity";
        private const string Directory = "Assets/_Project/Graphics/Ocean/LookDev/TranslucentLighting";
        private const string RootName = "HD-2D translucent sunlight";

        /// <summary>開いているOceanLookDevへ専用の光、色調、光線を設定し、保存待ちにする</summary>
        /// <example>Tools/Ocean/Apply Translucent Lighting to LookDevを実行してシーンを保存する</example>
        [MenuItem("Tools/Ocean/Apply Translucent Lighting to LookDev")]
        public static void Apply()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("OceanLookDevを単独で開き、再生を止めてから実行する");

            var pipeline = UniversalRenderPipeline.asset;
            var camera = Camera.main;
            var sun = RenderSettings.sun;
            var volume = UnityEngine.Object.FindFirstObjectByType<Volume>();
            var water = GameObject.Find("Ocean — depth, refraction, waves and foam");
            var shader = Shader.Find("Minge/Environment/Coastal Light Shaft");
            if (pipeline == null || !pipeline.supportsHDR || camera == null || sun == null || volume == null || water == null || shader == null)
                throw new InvalidOperationException("HDR対応のURP、カメラ、太陽、Volume、海面、光線Shaderが必要");

            if (!AssetDatabase.IsValidFolder(Directory))
                AssetDatabase.CreateFolder("Assets/_Project/Graphics/Ocean/LookDev", "TranslucentLighting");

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Apply translucent coastal lighting");

            // 明部は淡い金色、影は天空光の青を残し、遠景へだけ薄い霞をかける
            Undo.RecordObject(sun, "Tune coastal sun");
            sun.color = new Color(1f, 0.93f, 0.80f);
            sun.intensity = 2.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            Undo.RecordObjects(Resources.FindObjectsOfTypeAll<RenderSettings>(), "Tune coastal atmosphere");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.53f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.47f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.23f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.64f, 0.73f, 0.79f);
            RenderSettings.fogStartDistance = 58f;
            RenderSettings.fogEndDistance = 200f;

            // 既存の共有素材は複製し、この確認シーンの光だけを調整する
            var renderer = water.GetComponent<MeshRenderer>();
            var ocean = CopyMaterial(renderer.sharedMaterial, "M_TranslucentOcean.mat");
            Undo.RecordObject(ocean, "Tune coastal reflections");
            ocean.SetFloat("_SunGlintStrength", 1.45f);
            ocean.SetFloat("_Roughness", 0.19f);
            ocean.SetFloat("_ReflectionStrength", 1.1f);
            EditorUtility.SetDirty(ocean);
            Undo.RecordObject(renderer, "Assign look development water");
            renderer.sharedMaterial = ocean;

            var profile = GetProfile();
            ConfigureProfile(profile);
            Undo.RecordObject(volume, "Assign translucent color grading");
            volume.sharedProfile = profile;
            volume.enabled = true;
            volume.isGlobal = true;
            volume.weight = 1f;

            var data = camera.GetUniversalAdditionalCameraData();
            Undo.RecordObjects(new UnityEngine.Object[] { camera, data }, "Enable HDR coastal effects");
            camera.allowHDR = true;
            data.renderPostProcessing = true;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.requiresColorOption = CameraOverrideOption.On;
            data.volumeLayerMask |= 1 << volume.gameObject.layer;

            // 一枚の共有メッシュへ8本の光線をまとめ、追加の毎フレーム処理を不要にする
            BuildShafts(camera, sun, shader);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log("OceanLookDevへ透き通る斜光を設定した — シーンを保存してGameビューで確認する");
        }

        /// <summary>確認用の素材を初回だけ複製し、再実行時は同じアセットを返す</summary>
        /// <param name="source">複製元の共有素材</param>
        /// <param name="filename">専用フォルダー内のファイル名</param>
        /// <returns>確認シーン専用の保存済み素材</returns>
        /// <example>CopyMaterial(waterMaterial, "M_TranslucentOcean.mat")</example>
        private static Material CopyMaterial(Material source, string filename)
        {
            string path = $"{Directory}/{filename}";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(source) { name = System.IO.Path.GetFileNameWithoutExtension(filename) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>専用VolumeProfileを取得し、初回はアセットとして生成する</summary>
        /// <returns>光のにじみと色調を保存するProfile</returns>
        /// <example>ConfigureProfile(GetProfile())</example>
        private static VolumeProfile GetProfile()
        {
            string path = $"{Directory}/TranslucentCoast.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Translucent coast";
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        /// <summary>Profile内の設定を取得し、存在しなければ子アセットとして追加する</summary>
        /// <typeparam name="T">設定するVolumeComponent型</typeparam>
        /// <param name="profile">設定先のProfile</param>
        /// <returns>有効になったVolumeComponent</returns>
        /// <example>Effect&lt;Bloom&gt;(profile).intensity.Override(0.5f)</example>
        private static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var effect))
            {
                effect = profile.Add<T>();
                AssetDatabase.AddObjectToAsset(effect, profile);
            }

            Undo.RecordObject(effect, "Tune translucent grading");
            effect.active = true;
            EditorUtility.SetDirty(effect);
            return effect;
        }

        /// <summary>高輝度のにじみと寒暖差を設定し、桟橋と波の輪郭を残す</summary>
        /// <param name="profile">確認シーン専用のProfile</param>
        /// <example>ConfigureProfile(GetProfile())</example>
        private static void ConfigureProfile(VolumeProfile profile)
        {
            var bloom = Effect<Bloom>(profile);
            bloom.intensity.Override(0.48f);
            bloom.threshold.Override(1.12f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.96f, 0.86f));
            bloom.highQualityFiltering.Override(true);
            bloom.clamp.Override(12f);
            Effect<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);

            // 明るい砂や波のディテールを残し、青い影と象牙色の光を分ける
            var color = Effect<ColorAdjustments>(profile);
            color.postExposure.Override(-0.08f);
            color.contrast.Override(12f);
            color.saturation.Override(-7f);
            var split = Effect<SplitToning>(profile);
            split.shadows.Override(new Color(0.45f, 0.49f, 0.55f));
            split.highlights.Override(new Color(0.55f, 0.52f, 0.47f));
            split.balance.Override(0f);

            var vignette = Effect<Vignette>(profile);
            vignette.intensity.Override(0.17f);
            vignette.smoothness.Override(0.55f);
            vignette.color.Override(new Color(0.035f, 0.055f, 0.08f));
            EditorUtility.SetDirty(profile);
        }

        /// <summary>太陽の進行方向に沿う薄い光線を少数の三角形で配置する</summary>
        /// <param name="camera">光線の横幅を向ける確認カメラ</param>
        /// <param name="sun">光の進行方向を与える太陽</param>
        /// <param name="shader">深度と水面で消える光線Shader</param>
        /// <example>BuildShafts(Camera.main, RenderSettings.sun, shaftShader)</example>
        private static void BuildShafts(Camera camera, Light sun, Shader shader)
        {
            string materialPath = $"{Directory}/M_CoastalLightShaft.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Coastal light shafts" };
                AssetDatabase.CreateAsset(material, materialPath);
            }

            Undo.RecordObject(material, "Tune coastal shafts");
            material.SetColor("_Tint", new Color(1.5f, 1.30f, 0.94f, 1f));
            material.SetFloat("_Intensity", 0.20f);
            material.SetFloat("_Softness", 0.48f);
            material.SetFloat("_DepthFade", 3f);
            material.SetFloat("_WaterHeight", -1f);
            material.SetFloat("_WaterFade", 2.3f);
            material.SetFloat("_NearFade", 5f);
            material.SetFloat("_NoiseStrength", 0.24f);
            material.SetFloat("_Speed", 0.035f);
            EditorUtility.SetDirty(material);

            Vector3[] ends =
            {
                new Vector3(-19f, -1.4f, -8f), new Vector3(-12f, -1.4f, -13f),
                new Vector3(-4f, -1.4f, -11f), new Vector3(2f, -1.4f, -15f),
                new Vector3(9f, -1.4f, -8f), new Vector3(17f, -1.4f, -13f),
                new Vector3(25f, -1.4f, -5f), new Vector3(-27f, -1.4f, -3f)
            };
            float[] widths = { 4.5f, 2.2f, 5.5f, 1.5f, 3.8f, 5f, 2.8f, 3.5f };
            float[] strengths = { 0.75f, 0.48f, 1f, 0.40f, 0.78f, 0.65f, 0.42f, 0.45f };
            var vertices = new Vector3[ends.Length * 4];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[ends.Length * 6];
            Vector3 towardSun = -sun.transform.forward;
            Vector3 side = Vector3.Cross(towardSun, camera.transform.forward).normalized;

            // 上ほど細くし、下端はShaderの高さフェードで海面へ自然に溶かす
            for (int i = 0; i < ends.Length; i++)
            {
                Vector3 top = ends[i] + towardSun * (29f + i % 3 * 4f);
                Vector3 halfWidth = side * widths[i] * 0.5f;
                int v = i * 4;
                vertices[v] = ends[i] - halfWidth;
                vertices[v + 1] = ends[i] + halfWidth;
                vertices[v + 2] = top - halfWidth * 0.55f;
                vertices[v + 3] = top + halfWidth * 0.55f;
                uv[v] = new Vector2(0, 0);
                uv[v + 1] = new Vector2(1, 0);
                uv[v + 2] = new Vector2(0, 1);
                uv[v + 3] = new Vector2(1, 1);
                for (int j = 0; j < 4; j++) colors[v + j] = new Color(1, 1, 1, strengths[i]);
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            string meshPath = $"{Directory}/CoastalLightShafts.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Eight soft sunlight ribbons" };
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            Undo.RegisterCompleteObjectUndo(mesh, "Rebuild sunlight ribbons");
            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            GameObject root = null;
            foreach (var candidate in SceneManager.GetActiveScene().GetRootGameObjects())
                if (candidate.name == RootName) { root = candidate; break; }
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create translucent sunlight");
            }

            var filter = root.GetComponent<MeshFilter>();
            if (filter == null) filter = Undo.AddComponent<MeshFilter>(root);
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(root);
            Undo.RecordObjects(new UnityEngine.Object[] { filter, renderer }, "Assign sunlight ribbons");
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}

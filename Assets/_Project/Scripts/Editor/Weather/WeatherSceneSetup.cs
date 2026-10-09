using System;
using System.Linq;
using Minge2026.Weather;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Minge2026.Editor.Weather
{
    /// <summary>既存の屋外ステージに天候設定と参照を保存する配置ツール</summary>
    public static class WeatherSceneSetup
    {
        private const string Directory = "Assets/_Project/Graphics/Weather";

        /// <summary>現在のシーンに天候を配置し、シーンの保存は呼び出し側へ委ねる</summary>
        /// <returns>配置または更新した天候コントローラー</returns>
        /// <example>Tools/Weather/Install in Active Sceneから実行する</example>
        [MenuItem("Tools/Weather/Install in Active Scene")]
        public static WeatherController Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("再生を止めてから天候を配置する");

            Scene scene = SceneManager.GetActiveScene();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).ToArray();
            // 平らな地形タイルだけを濡らし、木や建物、海の共有素材には触れない
            Renderer[] ground = all.Where(r => r is MeshRenderer && r.sharedMaterial != null &&
                (r.sharedMaterial.name.StartsWith("TX_Grass_") || r.sharedMaterial.name == "TX_Rock") &&
                r.GetComponent<Collider>() != null && r.bounds.max.y > 0.4f && r.bounds.max.y < 0.7f &&
                r.bounds.size.y > 0.1f).ToArray();
            if (ground.Length == 0) throw new InvalidOperationException("対象の地形タイルが見つからない");
            Bounds bounds = ground[0].bounds;
            foreach (Renderer renderer in ground) bounds.Encapsulate(renderer.bounds);

            System.IO.Directory.CreateDirectory(Directory);
            AssetDatabase.Refresh();
            WeatherSettings settings = AssetDatabase.LoadAssetAtPath<WeatherSettings>(Directory + "/WeatherSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WeatherSettings>();
                AssetDatabase.CreateAsset(settings, Directory + "/WeatherSettings.asset");
            }

            Material rain = MaterialAsset("Rain", "Minge/Weather/Rain Streak");
            Material lightning = MaterialAsset("Lightning", "Minge/Weather/Rain Streak");
            Material puddle = MaterialAsset("Puddle", "Minge/Weather/HD2D Puddle");
            rain.SetColor("_BaseColor", new Color(0.85f, 0.92f, 1f, 0.68f));
            lightning.SetColor("_BaseColor", new Color(5f, 6.2f, 8f, 1f));
            lightning.SetFloat("_NearFade", 0.01f);

            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WeatherController>(true)).FirstOrDefault();
            if (controller == null)
            {
                GameObject root = new GameObject("Weather System");
                Undo.RegisterCreatedObjectUndo(root, "Install weather");
                SceneManager.MoveGameObjectToScene(root, scene);
                controller = Undo.AddComponent<WeatherController>(root);
            }

            WeatherAtmosphere atmosphere = controller.GetComponent<WeatherAtmosphere>();
            if (atmosphere == null) atmosphere = Undo.AddComponent<WeatherAtmosphere>(controller.gameObject);
            Volume volume = controller.GetComponent<Volume>();
            if (volume == null) volume = Undo.AddComponent<Volume>(controller.gameObject);
            volume.isGlobal = true;
            volume.priority = 25f;
            volume.weight = 0f;
            volume.sharedProfile = StormProfile();
            Light sun = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>(true))
                .FirstOrDefault(l => l.type == LightType.Directional);
            atmosphere.Configure(sun, all.Where(r => r.sharedMaterial != null &&
                r.sharedMaterial.shader.name == "Minge/Environment/Stage Light Shaft").ToArray(), volume);

            // シーン固有の参照をシリアライズし、ビルド時のShader除去も防ぐ
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("_settings").objectReferenceValue = settings;
            serialized.FindProperty("_focus").objectReferenceValue = scene.GetRootGameObjects()
                .FirstOrDefault(g => g.CompareTag("Player"))?.transform;
            serialized.FindProperty("_groundBounds").boundsValue = bounds;
            serialized.FindProperty("_rainMaterial").objectReferenceValue = rain;
            serialized.FindProperty("_lightningMaterial").objectReferenceValue = lightning;
            serialized.FindProperty("_puddleMaterial").objectReferenceValue = puddle;
            serialized.FindProperty("_atmosphere").objectReferenceValue = atmosphere;
            serialized.FindProperty("_groundRoot").objectReferenceValue = ground[0].transform.root;
            Material[] groundMaterials = ground.Select(r => r.sharedMaterial).Distinct().ToArray();
            SerializedProperty array = serialized.FindProperty("_groundMaterials");
            array.arraySize = groundMaterials.Length;
            for (int i = 0; i < groundMaterials.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = groundMaterials[i];
            serialized.ApplyModifiedProperties();

            foreach (Camera camera in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true)))
            {
                var data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Enable weather grading");
                data.renderPostProcessing = true;
                data.volumeLayerMask |= 1 << controller.gameObject.layer;
                EditorUtility.SetDirty(data);
            }

            EditorUtility.SetDirty(atmosphere);
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(rain);
            EditorUtility.SetDirty(lightning);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = controller.gameObject;
            return controller;
        }

        /// <summary>天候用素材がなければ作成し、既存素材は再利用する</summary>
        /// <param name="name">天候素材の名前</param>
        /// <param name="shaderName">配置済みShaderの名前</param>
        /// <returns>アセットとして保存された素材</returns>
        /// <example>MaterialAsset("Rain", "Minge/Weather/Rain Streak")</example>
        private static Material MaterialAsset(string name, string shaderName)
        {
            string path = Directory + "/M_" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shaderが見つからない: " + shaderName);
            material = new Material(shader) { name = "M_" + name };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>晴天の被写界深度とBloomを保持する雨天専用の色調Profileを作る</summary>
        /// <returns>低彩度、青み、わずかな減光を持つProfile</returns>
        /// <example>volume.sharedProfile = StormProfile()</example>
        private static VolumeProfile StormProfile()
        {
            string path = Directory + "/RainAtmosphere.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(-0.18f);
            color.saturation.Override(-16f);
            color.colorFilter.Override(new Color(0.81f, 0.9f, 1f));
            WhiteBalance balance = profile.Add<WhiteBalance>();
            balance.temperature.Override(-12f);
            foreach (VolumeComponent component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }

    /// <summary>再生中に天候を切り替え、待ち時間なしで演出を確認するInspector</summary>
    [CustomEditor(typeof(WeatherController))]
    public sealed class WeatherControllerEditor : UnityEditor.Editor
    {
        /// <summary>設定参照と再生中専用の検証ボタンを表示する</summary>
        /// <example>Weather Systemを選択して雨と雷を確認する</example>
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (WeatherController)target;
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("確率と時間はWeather Settingsで調整\n以下の確認ボタンは再生中のみ使用可能", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || controller.Cycle == null))
            {
                if (GUILayout.Button("雨を降らせる")) controller.StartRain();
                if (GUILayout.Button("雨を止める → 水たまり")) controller.StopRain();
                if (GUILayout.Button("落雷を確認")) controller.TriggerLightning();
            }

            if (controller.Cycle != null)
                EditorGUILayout.LabelField($"{controller.Cycle.Phase} / 濡れ {controller.Cycle.Wetness:P0} / 水たまり {controller.Cycle.Puddles:P0}");
        }
    }
}

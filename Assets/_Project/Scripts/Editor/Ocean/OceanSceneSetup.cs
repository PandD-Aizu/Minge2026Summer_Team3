using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace _Project.Scripts.Editor.Ocean
{
    /// <summary>既存の海の当たり判定を残してHD-2D海面と浅瀬を配置するエディター専用ツール</summary>
    public static class OceanSceneSetup
    {
        public const string MaterialPath = "Assets/_Project/Materials/Ocean/M_HD2DOcean.mat";
        private const string MeshDirectory = "Assets/_Project/Graphics/Ocean";
        private const string VisualName = "Ocean Visuals";

        /// <summary>現在のシーンにあるseaへ海面を追加し、保存は利用者に委ねる</summary>
        /// <example>Tools/Ocean/Apply to Active Sceneから既存ステージへ適用する</example>
        [MenuItem("Tools/Ocean/Apply to Active Scene")]
        public static void ApplyToActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("海の配置は再生を停止してから実行する");

            Scene scene = SceneManager.GetActiveScene();
            GameObject source = FindSea(scene);
            if (source == null)
                throw new InvalidOperationException("BoxColliderとMeshRendererを持つseaが見つからない");

            // 同じシーンへの再実行で海面やメッシュを重複させない
            Transform parent = source.transform.parent;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Transform child in sceneRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.parent == parent && child.name == VisualName)
                    throw new InvalidOperationException("Ocean Visualsは配置済みなので既存のMaterialを調整する");
            }

            BoxCollider sourceCollider = source.GetComponent<BoxCollider>();
            if (!source.activeInHierarchy || !sourceCollider.enabled)
                throw new InvalidOperationException("seaとBoxColliderを有効にしてから適用する");
            Bounds bounds = sourceCollider.bounds;
            if (bounds.size.x < 0.1f || bounds.size.z < 0.1f)
                throw new InvalidOperationException("seaの幅と奥行きは0.1以上にする");

            EnsureDirectory(MeshDirectory);
            Material water = GetOrCreateWaterMaterial();
            Material sand = GetOrCreateSandMaterial();
            float surfaceY = bounds.max.y;
            List<Bounds> shores = CollectShoreBounds(scene, source, surfaceY);
            string stem = scene.name;
            string waterMeshPath = $"{MeshDirectory}/{stem}_Surface.asset";
            string bedMeshPath = $"{MeshDirectory}/{stem}_Seabed.asset";

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add HD-2D ocean");

            // 描画だけを分離し、地形のCollider・魚影・音源は現在の位置を維持する
            var root = new GameObject(VisualName);
            Undo.RegisterCreatedObjectUndo(root, "Add HD-2D ocean");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(bounds.center.x, surfaceY, bounds.center.z);
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Mesh surface = SaveGeneratedMesh(CreateGrid(bounds, shores, false), waterMeshPath);
            Mesh seabed = SaveGeneratedMesh(CreateGrid(bounds, shores, true), bedMeshPath);
            AddRenderer(root.transform, "Water Surface", surface, water, false);
            AddRenderer(root.transform, "Submerged Sand", seabed, sand, true);
            MeshRenderer oldRenderer = source.GetComponent<MeshRenderer>();
            Undo.RecordObject(oldRenderer, "Replace sea visuals");
            oldRenderer.enabled = false;

            // カメラ単位で要求し、品質設定を切り替えても水深と屈折を利用できるようにする
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Camera camera in sceneRoot.GetComponentsInChildren<Camera>(true))
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                Undo.RecordObject(data, "Enable ocean camera textures");
                data.requiresDepthTexture = true;
                data.requiresColorTexture = true;
                EditorUtility.SetDirty(data);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
        }

        /// <summary>生成メッシュを保存し、Undo後の再配置では同じアセットを再利用する</summary>
        /// <param name="generated">今回計算したメッシュ</param>
        /// <param name="path">このツールが管理する保存先</param>
        /// <returns>MeshFilterへ割り当てる保存済みメッシュ</returns>
        /// <example>SaveGeneratedMesh(grid, path)でGUIDを維持して形状を更新する</example>
        private static Mesh SaveGeneratedMesh(Mesh generated, string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            if (existing is not Mesh mesh || mesh.name != generated.name)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new InvalidOperationException($"生成メッシュ以外のアセットが存在する: {path}");
            }

            Undo.RegisterCompleteObjectUndo(mesh, "Rebuild ocean mesh");
            EditorUtility.CopySerialized(generated, mesh);
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>実行中シーンから既存の海の箱を探す</summary>
        /// <param name="scene">検索する読み込み済みシーン</param>
        /// <returns>小文字seaでColliderとRendererを持つオブジェクト、未検出時はnull</returns>
        /// <example>FindSea(SceneManager.GetActiveScene())で差し替え対象を取得する</example>
        private static GameObject FindSea(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate.name == "sea" && candidate.TryGetComponent(out BoxCollider _) &&
                    candidate.TryGetComponent(out MeshRenderer _)) return candidate.gameObject;
            }

            return null;
        }

        /// <summary>海面を横切る地形の範囲を収集し、浅瀬の形状に使う</summary>
        /// <param name="scene">配置先のシーン</param>
        /// <param name="sea">水底判定から除く既存の海</param>
        /// <param name="height">海面のワールドY座標</param>
        /// <returns>海面と交差する静的な地形BoxColliderの範囲</returns>
        /// <example>CollectShoreBounds(scene, sea, -1f)で岸と突堤を取得する</example>
        private static List<Bounds> CollectShoreBounds(Scene scene, GameObject sea, float height)
        {
            var result = new List<Bounds>();
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>())
            {
                if (collider.gameObject == sea || collider.isTrigger || !collider.enabled ||
                    collider.attachedRigidbody != null || !collider.TryGetComponent(out MeshRenderer renderer) ||
                    !renderer.enabled) continue;

                Bounds bounds = collider.bounds;
                if (bounds.min.y < height && bounds.max.y > height) result.Add(bounds);
            }

            return result;
        }

        /// <summary>海面または岸の距離に応じて沈む水底メッシュを生成する</summary>
        /// <param name="bounds">元の海Colliderのワールド範囲</param>
        /// <param name="shores">海面に接する地形の範囲</param>
        /// <param name="seabed">trueなら水底、falseなら水平な海面</param>
        /// <returns>中心を原点に持つ共有可能なメッシュ</returns>
        /// <example>CreateGrid(bounds, shores, false)をMeshFilterへ割り当てる</example>
        private static Mesh CreateGrid(Bounds bounds, List<Bounds> shores, bool seabed)
        {
            int columns = Mathf.CeilToInt(bounds.size.x / (seabed ? 1.5f : 0.8f));
            int rows = Mathf.CeilToInt(bounds.size.z / (seabed ? 1.5f : 0.8f));
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];

            for (int z = 0; z <= rows; z++)
            for (int x = 0; x <= columns; x++)
            {
                int index = z * (columns + 1) + x;
                float u = (float)x / columns;
                float v = (float)z / rows;
                float worldX = Mathf.Lerp(bounds.min.x, bounds.max.x, u);
                float worldZ = Mathf.Lerp(bounds.min.z, bounds.max.z, v);
                float height = 0f;
                if (seabed)
                {
                    float distance = 40f;
                    foreach (Bounds shore in shores)
                    {
                        float dx = Mathf.Max(shore.min.x - worldX, 0f, worldX - shore.max.x);
                        float dz = Mathf.Max(shore.min.z - worldZ, 0f, worldZ - shore.max.z);
                        distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dz * dz));
                    }

                    float ripples = Mathf.PerlinNoise(worldX * 0.19f + 30f, worldZ * 0.23f + 30f);
                    height = -0.28f - Mathf.Min(distance * 0.34f, 9f) - ripples * 0.13f;
                }

                vertices[index] = new Vector3(worldX - bounds.center.x, height, worldZ - bounds.center.z);
                uv[index] = new Vector2(u, v);
            }

            for (int z = 0, index = 0; z < rows; z++)
            for (int x = 0; x < columns; x++)
            {
                int a = z * (columns + 1) + x;
                triangles[index++] = a;
                triangles[index++] = a + columns + 1;
                triangles[index++] = a + 1;
                triangles[index++] = a + 1;
                triangles[index++] = a + columns + 1;
                triangles[index++] = a + columns + 2;
            }

            var mesh = new Mesh { name = seabed ? "Coastal Seabed" : "HD2D Ocean Grid" };
            if (vertices.Length > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Bounds meshBounds = mesh.bounds;
            meshBounds.Expand(new Vector3(0.5f, 1f, 0.5f));
            mesh.bounds = meshBounds;
            return mesh;
        }

        /// <summary>共有メッシュとマテリアルを使う描画専用の子を作る</summary>
        /// <param name="parent">海の描画ルート</param>
        /// <param name="objectName">Hierarchy上の名前</param>
        /// <param name="mesh">保存済みメッシュ</param>
        /// <param name="material">共有マテリアル</param>
        /// <param name="receiveShadows">水底で地形の影を受ける場合はtrue</param>
        /// <example>AddRenderer(root, "Water Surface", mesh, material, false)で海面を追加する</example>
        private static void AddRenderer(Transform parent, string objectName, Mesh mesh, Material material, bool receiveShadows)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }

        /// <summary>調整値を共有する海のMaterialを読み込み、未作成時だけ作る</summary>
        /// <returns>HD2DOceanシェーダーを利用するMaterial</returns>
        /// <example>別のステージにもGetOrCreateWaterMaterial()の結果を共有する</example>
        public static Material GetOrCreateWaterMaterial()
        {
            // 透明な海面の描画時点で岸と水底の深度を参照できる順番にする
            UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/URPDefaultResources/Default_Forward_Renderer.asset");
            if (renderer != null && renderer.copyDepthMode != CopyDepthMode.AfterOpaques)
            {
                renderer.copyDepthMode = CopyDepthMode.AfterOpaques;
                EditorUtility.SetDirty(renderer);
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null) return material;
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/Ocean/HD2DOcean.shader");
            if (shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("HD2DOceanシェーダーのインポートとコンパイルを確認する");

            EnsureDirectory(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            material = new Material(shader) { name = "M_HD2DOcean" };
            material.SetFloat("_ShoreZ", -7f);
            material.renderQueue = 2990;
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        /// <summary>浅瀬の屈折越しに見える砂底用Materialを用意する</summary>
        /// <returns>控えめな反射率を持つ暖色の砂Material</returns>
        /// <example>水底のMeshRendererへGetOrCreateSandMaterial()を割り当てる</example>
        private static Material GetOrCreateSandMaterial()
        {
            const string path = "Assets/_Project/Materials/Ocean/M_SubmergedSand.mat";
            Material sand = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (sand != null) return sand;
            sand = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_SubmergedSand" };
            sand.SetColor("_BaseColor", new Color(0.52f, 0.55f, 0.38f));
            sand.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(sand, path);
            return sand;
        }

        /// <summary>UnityのAssetDatabaseを使って必要な親フォルダーを順に作る</summary>
        /// <param name="path">Assetsから始まるフォルダーパス</param>
        /// <example>EnsureDirectory("Assets/_Project/Graphics/Ocean")で出力先を用意する</example>
        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

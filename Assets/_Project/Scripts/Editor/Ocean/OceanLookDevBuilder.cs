using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Random = System.Random;

namespace EditorTools.Ocean
{
    /// <summary>海の色、反射、浅瀬を確認する独立した海岸のデモシーンを生成する</summary>
    public static class OceanLookDevBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Develop/OceanLookDev.unity";
        private const string ArtDirectory = "Assets/_Project/Graphics/Ocean/LookDev";
        private const string OceanMaterialPath = "Assets/_Project/Materials/Ocean/M_HD2DOcean.mat";

        /// <summary>直前に生成したシーンのアセットパス</summary>
        public static string LastScenePath { get; private set; }

        /// <summary>海面、砂浜、岩、桟橋、照明を新しいシーンに生成して保存する</summary>
        /// <example>UnityのTools/Ocean/Create Coastal Look Development Sceneから実行する</example>
        [MenuItem("Tools/Ocean/Create Coastal Look Development Scene")]
        public static void Build()
        {
            var oceanMaterial = AssetDatabase.LoadAssetAtPath<Material>(OceanMaterialPath);
            if (oceanMaterial == null)
                throw new InvalidOperationException($"海のマテリアルが見つからない: {OceanMaterialPath}");

            // 既存シーンと手作業で調整したアセットを上書きせず、生成物だけを別フォルダーに保存する
            EnsureDirectory(ArtDirectory);
            string buildDirectory = AssetDatabase.GenerateUniqueAssetPath($"{ArtDirectory}/CoastalStudy");
            AssetDatabase.CreateFolder(ArtDirectory, System.IO.Path.GetFileName(buildDirectory));
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            try
            {
                var scenery = new GameObject("Coastal study — warm sand and cool water").transform;
                var random = new Random(7319);

                // 微細な素材感を付け、シルエットと色の対比で水面を引き立てる
                Material sand = CreateSurface(buildDirectory, "Warm sand", new Color(0.72f, 0.62f, 0.43f), 0.12f, 0.15f);
                Material rock = CreateSurface(buildDirectory, "Weathered coastal stone", new Color(0.28f, 0.31f, 0.29f), 0.16f, 0.24f);
                Material wetRock = CreateSurface(buildDirectory, "Wet basalt", new Color(0.12f, 0.19f, 0.18f), 0.46f, 0.12f);
                Material timber = CreateSurface(buildDirectory, "Sun bleached timber", new Color(0.37f, 0.24f, 0.12f), 0.18f, 0.26f);
                Material darkTimber = CreateSurface(buildDirectory, "Dark timber ends", new Color(0.19f, 0.13f, 0.08f), 0.23f, 0.18f);
                Material rope = CreateSurface(buildDirectory, "Hemp rope", new Color(0.58f, 0.48f, 0.28f), 0.1f, 0.05f);
                Material grass = CreateSurface(buildDirectory, "Salt grass", new Color(0.31f, 0.34f, 0.12f), 0.05f, 0.12f);
                Material iron = CreateSurface(buildDirectory, "Forged iron", new Color(0.11f, 0.13f, 0.13f), 0.45f, 0.03f);
                iron.SetFloat("_Metallic", 0.7f);

                // 海面は細分化して頂点波を受け、海底は実際の深度差で浅瀬を作る
                Mesh waterMesh = CreateGrid("Subdivided ocean", 224, 224, new Vector2(-65f, -85f), new Vector2(65f, 20f), false);
                waterMesh.bounds = new Bounds(new Vector3(0f, -1f, -32.5f), new Vector3(130f, 8f, 105f));
                SaveAsset(waterMesh, buildDirectory, "OceanGrid.asset");
                var water = CreateMeshObject("Ocean — depth, refraction, waves and foam", scenery, waterMesh, oceanMaterial);
                water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

                Mesh coastMesh = CreateGrid("Sculpted beach and seabed", 170, 150, new Vector2(-68f, -87f), new Vector2(68f, 45f), true);
                SaveAsset(coastMesh, buildDirectory, "BeachAndSeabed.asset");
                CreateMeshObject("Continuous sand beach and sloping seabed", scenery, coastMesh, sand);

                BuildRocks(scenery, buildDirectory, rock, wetRock, random);
                BuildJetty(scenery, timber, darkTimber, rope, iron);
                BuildGrass(scenery, buildDirectory, grass, random);
                BuildLighting(buildDirectory);
                BuildCamera();

                // 元のシーンを閉じずに保存し、生成シーンだけを閉じて二重のカメラや地形を残さない
                EnsureDirectory("Assets/_Project/Scenes/Develop");
                LastScenePath = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
                if (!EditorSceneManager.SaveScene(scene, LastScenePath))
                    throw new InvalidOperationException($"海のデモシーンを保存できなかった: {LastScenePath}");

                AssetDatabase.SaveAssets();
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(LastScenePath);
                Debug.Log($"Ocean look development scene created: {LastScenePath}");
            }
            catch
            {
                // 生成に失敗しても一時シーンを残さず、開いていたユーザーのシーンをアクティブに戻す
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                throw;
            }
        }

        /// <summary>海岸線の曲率から指定座標の砂地の高さを求める</summary>
        /// <param name="x">ワールドX座標</param>
        /// <param name="z">ワールドZ座標</param>
        /// <returns>海底または砂浜のワールドY座標</returns>
        /// <example>BeachHeight(0, -7)で中央の波打ち際の高さを調べる</example>
        private static float BeachHeight(float x, float z)
        {
            // 左右に小さな入江を作り、直線的な水際を避ける
            float shoreline = -6.8f + Mathf.Sin(x * 0.115f) * 1.3f + Mathf.Sin(x * 0.29f + 0.6f) * 0.55f;
            float inland = z - shoreline;
            float elevation = inland < 0f
                ? -1.04f + inland * 0.235f
                : -1.04f + Mathf.Min(inland, 16f) * 0.13f + Mathf.Max(inland - 16f, 0f) * 0.04f;
            float detail = (Mathf.PerlinNoise(x * 0.17f + 31.2f, z * 0.19f + 53.1f) - 0.5f) * 0.11f;
            return elevation + detail;
        }

        /// <summary>海面または連続した砂地の格子メッシュを生成する</summary>
        /// <param name="name">メッシュ名</param>
        /// <param name="columns">X方向の分割数</param>
        /// <param name="rows">Z方向の分割数</param>
        /// <param name="minimum">XZ範囲の最小値</param>
        /// <param name="maximum">XZ範囲の最大値</param>
        /// <param name="beach">砂地の高さを使う場合はtrue</param>
        /// <returns>保存前の格子メッシュ</returns>
        /// <example>CreateGrid("Ocean", 128, 128, new Vector2(-40, -50), new Vector2(40, 15), false)</example>
        private static Mesh CreateGrid(string name, int columns, int rows, Vector2 minimum, Vector2 maximum, bool beach)
        {
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];

            // UVはワールド単位で連続させ、異なる分割数でも素材の大きさを揃える
            for (int row = 0; row <= rows; row++)
            {
                for (int column = 0; column <= columns; column++)
                {
                    int index = row * (columns + 1) + column;
                    float x = Mathf.Lerp(minimum.x, maximum.x, column / (float)columns);
                    float z = Mathf.Lerp(minimum.y, maximum.y, row / (float)rows);
                    float height = beach ? BeachHeight(x, z) : -1f;
                    // 遠景だけ間隔を広げ、透視投影の画面端へ砂地の切れ目を出さない
                    if (beach)
                    {
                        if (x > 50f) x = 50f + (x - 50f) * 4f;
                        if (x < -50f) x = -50f + (x + 50f) * 4f;
                        if (z > 20f) z = 20f + (z - 20f) * 4f;
                    }

                    vertices[index] = new Vector3(x, height, z);
                    uv[index] = new Vector2(x, z) * 0.22f;
                }
            }

            // 上向きの法線になる順序で三角形を並べる
            int triangle = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int a = row * (columns + 1) + column;
                    int b = a + columns + 1;
                    triangles[triangle++] = a;
                    triangles[triangle++] = b;
                    triangles[triangle++] = a + 1;
                    triangles[triangle++] = a + 1;
                    triangles[triangle++] = b;
                    triangles[triangle++] = b + 1;
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>決まった乱数から面ごとに法線を持つ海岸の岩メッシュを作る</summary>
        /// <param name="seed">シルエットを再現する乱数シード</param>
        /// <returns>原点を中心とした半径約1の岩メッシュ</returns>
        /// <example>CreateRockMesh(42)で同じ形の岩を再生成する</example>
        private static Mesh CreateRockMesh(int seed)
        {
            var random = new Random(seed);
            const int rings = 6;
            const int segments = 11;
            var points = new Vector3[(rings + 1) * segments];

            // 各輪郭を少しずらし、球のような均一さを抑える
            for (int ring = 0; ring <= rings; ring++)
            {
                float latitude = Mathf.PI * ring / rings;
                for (int segment = 0; segment < segments; segment++)
                {
                    float longitude = Mathf.PI * 2f * segment / segments + (ring % 2) * 0.2f;
                    float radius = 0.78f + (float)random.NextDouble() * 0.3f;
                    points[ring * segments + segment] = new Vector3(
                        Mathf.Sin(latitude) * Mathf.Cos(longitude) * radius,
                        Mathf.Cos(latitude) * (0.8f + (float)random.NextDouble() * 0.13f),
                        Mathf.Sin(latitude) * Mathf.Sin(longitude) * radius);
                }
            }

            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int a = ring * segments + segment;
                    int b = ring * segments + (segment + 1) % segments;
                    int c = a + segments;
                    int d = b + segments;
                    AddRockTriangle(points[a], points[b], points[c], vertices, uv, triangles);
                    AddRockTriangle(points[b], points[d], points[c], vertices, uv, triangles);
                }
            }

            var mesh = new Mesh { name = $"Coastal rock {seed}" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>岩の中心から外を向く独立した三角形を追加する</summary>
        /// <param name="a">頂点A</param>
        /// <param name="b">頂点B</param>
        /// <param name="c">頂点C</param>
        /// <param name="vertices">追加先の頂点配列</param>
        /// <param name="uv">追加先のUV配列</param>
        /// <param name="triangles">追加先の三角形配列</param>
        /// <example>AddRockTriangle(a, b, c, vertices, uv, triangles)で面を1枚加える</example>
        private static void AddRockTriangle(Vector3 a, Vector3 b, Vector3 c, List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) < 0f)
                (b, c) = (c, b);

            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uv.Add(new Vector2(a.x + a.z * 0.3f, a.y));
            uv.Add(new Vector2(b.x + b.z * 0.3f, b.y));
            uv.Add(new Vector2(c.x + c.z * 0.3f, c.y));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        /// <summary>浅瀬に大小の岩を配置し、波と深度の交差を見やすくする</summary>
        /// <param name="parent">生成物の親</param>
        /// <param name="directory">メッシュの保存先</param>
        /// <param name="stone">乾いた岩の素材</param>
        /// <param name="wetStone">濡れた岩の素材</param>
        /// <param name="random">配置を再現する乱数生成器</param>
        /// <example>BuildRocks(root, folder, rockMaterial, wetMaterial, random)</example>
        private static void BuildRocks(Transform parent, string directory, Material stone, Material wetStone, Random random)
        {
            var rockRoot = new GameObject("Tide worn rock clusters").transform;
            rockRoot.SetParent(parent, false);
            var shapes = new Mesh[5];
            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = CreateRockMesh(810 + i * 131);
                SaveAsset(shapes[i], directory, $"CoastalRock{i}.asset");
            }

            // 岩の重なりを大、中、小の順に構成し、海面に自然な遮蔽と泡の輪郭を作る
            var centers = new[] { new Vector2(-21f, -3.5f), new Vector2(15f, -2f), new Vector2(6.8f, -8.5f), new Vector2(-25f, -13f) };
            for (int cluster = 0; cluster < centers.Length; cluster++)
            {
                int count = cluster == 2 ? 5 : 10;
                for (int i = 0; i < count; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = i == 0 ? 0f : 0.8f + (float)random.NextDouble() * 3.5f;
                    float size = i == 0 ? 3.2f : Mathf.Lerp(0.55f, 1.9f, (float)random.NextDouble());
                    if (cluster == 2) size *= 0.72f;
                    float x = centers[cluster].x + Mathf.Cos(angle) * distance;
                    float z = centers[cluster].y + Mathf.Sin(angle) * distance;
                    var instance = CreateMeshObject($"Rock {cluster + 1} / {i + 1}", rockRoot, shapes[i % shapes.Length], i % 3 == 0 ? wetStone : stone);
                    instance.transform.position = new Vector3(x, Mathf.Max(BeachHeight(x, z), -2.15f) + size * 0.2f, z);
                    instance.transform.localScale = new Vector3(size * 1.25f, size * 0.8f, size);
                    instance.transform.rotation = Quaternion.Euler(-7f + i * 3f, (float)random.NextDouble() * 360f, i % 2 == 0 ? -9f : 12f);
                }
            }

            // 小石は水際にのみ散らし、画面全体のノイズを増やしすぎない
            for (int i = 0; i < 54; i++)
            {
                float x = -33f + (float)random.NextDouble() * 66f;
                float z = -5f + (float)random.NextDouble() * 7f;
                float size = 0.05f + (float)random.NextDouble() * 0.2f;
                var pebble = CreateMeshObject($"Shore pebble {i + 1}", rockRoot, shapes[i % shapes.Length], stone);
                pebble.transform.position = new Vector3(x, BeachHeight(x, z), z);
                pebble.transform.localScale = new Vector3(size * 1.4f, size * 0.65f, size);
                pebble.transform.rotation = Quaternion.Euler(0f, i * 137.5f, 0f);
            }
        }

        /// <summary>板の隙間、杭、ロープを持つ小さな木製桟橋を生成する</summary>
        /// <param name="parent">生成物の親</param>
        /// <param name="timber">板の素材</param>
        /// <param name="darkTimber">端面と梁の素材</param>
        /// <param name="rope">ロープの素材</param>
        /// <param name="iron">釘と金具の素材</param>
        /// <example>BuildJetty(root, wood, darkWood, hemp, metal)</example>
        private static void BuildJetty(Transform parent, Material timber, Material darkTimber, Material rope, Material iron)
        {
            var jetty = new GameObject("Old fishing jetty").transform;
            jetty.SetParent(parent, false);
            jetty.position = new Vector3(-8.6f, 0f, 0f);
            jetty.rotation = Quaternion.Euler(0f, -9f, 0f);

            // 規則的な支持梁に、少しずれた板を載せる
            CreateBox("Left bearer", jetty, new Vector3(-0.91f, -0.16f, -8f), new Vector3(0.24f, 0.28f, 20f), darkTimber);
            CreateBox("Right bearer", jetty, new Vector3(0.91f, -0.16f, -8f), new Vector3(0.24f, 0.28f, 20f), darkTimber);
            for (int i = 0; i < 39; i++)
            {
                float z = 1.7f - i * 0.51f;
                var plank = CreateBox($"Deck plank {i + 1}", jetty, new Vector3(Mathf.Sin(i * 7f) * 0.018f, 0.015f + Mathf.Sin(i * 2.3f) * 0.012f, z), new Vector3(2.6f + Mathf.Sin(i * 8f) * 0.055f, 0.13f, 0.465f), i % 6 == 0 ? darkTimber : timber);
                plank.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(i * 3.1f) * 0.8f, 0f);

                for (int side = -1; side <= 1; side += 2)
                    CreateBox("Iron nail", jetty, new Vector3(side * 0.92f, 0.09f, z), new Vector3(0.035f, 0.012f, 0.035f), iron);
            }

            // 杭を海底まで伸ばし、水際で深度泡が発生する接点を作る
            for (int i = 0; i < 7; i++)
            {
                float z = 1.25f - i * 3.05f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 1.35f;
                    CreateBox("Timber pile", jetty, new Vector3(x, -1.55f, z), new Vector3(0.25f, 5.5f, 0.25f), darkTimber);
                    CreateBox("Sunlit pile cap", jetty, new Vector3(x, 1.215f, z), new Vector3(0.29f, 0.08f, 0.29f), timber);
                    CreateBox("Iron binding", jetty, new Vector3(x, 0.74f, z), new Vector3(0.263f, 0.085f, 0.263f), iron);

                    if (i < 6)
                    {
                        for (int part = 0; part < 8; part++)
                        {
                            float t0 = part / 8f;
                            float t1 = (part + 1) / 8f;
                            Vector3 start = new Vector3(x, 0.96f - Mathf.Sin(t0 * Mathf.PI) * 0.27f, z - t0 * 3.05f);
                            Vector3 end = new Vector3(x, 0.96f - Mathf.Sin(t1 * Mathf.PI) * 0.27f, z - t1 * 3.05f);
                            CreateRod("Sagging hemp rope", jetty, start, end, 0.022f, rope);
                        }
                    }
                }
            }

            // 岸側の荷箱でスケール感を付ける
            var crate = CreateBox("Weathered fishing crate", jetty, new Vector3(0.45f, 0.46f, 0.55f), new Vector3(0.78f, 0.76f, 0.9f), darkTimber);
            for (int i = 0; i < 4; i++)
            {
                CreateBox("Crate slat", crate.transform, new Vector3(0f, -0.34f + i * 0.225f, -0.515f), new Vector3(1.025f, 0.13f, 0.07f), timber);
                CreateBox("Crate lid board", crate.transform, new Vector3(-0.39f + i * 0.26f, 0.535f, 0f), new Vector3(0.225f, 0.07f, 1.03f), timber);
            }
        }

        /// <summary>砂丘の奥に小さな草の房を配置する</summary>
        /// <param name="parent">生成物の親</param>
        /// <param name="directory">メッシュの保存先</param>
        /// <param name="material">草の素材</param>
        /// <param name="random">配置を再現する乱数生成器</param>
        /// <example>BuildGrass(root, folder, grassMaterial, random)</example>
        private static void BuildGrass(Transform parent, string directory, Material material, Random random)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int blade = 0; blade < 9; blade++)
            {
                float angle = blade * 2.39996f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-direction.z, 0f, direction.x) * 0.055f;
                Vector3 origin = direction * (0.06f + blade * 0.01f);
                Vector3 tip = direction * 0.39f + Vector3.up * (0.48f + blade % 3 * 0.12f);
                int start = vertices.Count;
                vertices.Add(origin - side);
                vertices.Add(origin + side);
                vertices.Add(tip);

                // 表裏で頂点を分け、法線の再計算時に逆向きの面が相殺されるのを防ぐ
                vertices.Add(tip);
                vertices.Add(origin + side);
                vertices.Add(origin - side);
                triangles.AddRange(new[] { start, start + 1, start + 2, start + 3, start + 4, start + 5 });
            }

            var mesh = new Mesh { name = "Coastal grass tuft" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveAsset(mesh, directory, "CoastalGrass.asset");

            var grassRoot = new GameObject("Sparse dune grasses").transform;
            grassRoot.SetParent(parent, false);
            for (int i = 0; i < 180; i++)
            {
                float x = -48f + (float)random.NextDouble() * 96f;
                float z = 3f + (float)random.NextDouble() * 20f;
                if (Mathf.PerlinNoise(x * 0.14f + 81f, z * 0.14f) < 0.45f)
                    continue;

                var tuft = CreateMeshObject($"Salt grass {i + 1}", grassRoot, mesh, material);
                tuft.transform.position = new Vector3(x, BeachHeight(x, z), z);
                tuft.transform.localScale = Vector3.one * (0.6f + (float)random.NextDouble() * 0.8f);
                tuft.transform.rotation = Quaternion.Euler(0f, i * 137.5f, 0f);
            }
        }

        /// <summary>暖色の日光と控えめなブルームをデモシーンに設定する</summary>
        /// <param name="directory">空とポスト処理の保存先</param>
        /// <example>BuildLighting(folder)で海面のハイライトを確認する照明を生成する</example>
        private static void BuildLighting(string directory)
        {
            // カメラと太陽を水面法線の反対側に配置し、平らな水面にも鏡面反射が届くようにする
            var lightObject = new GameObject("Late afternoon sun");
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.88f, 0.70f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.035f;
            sun.shadowNormalBias = 0.22f;
            sun.transform.rotation = Quaternion.Euler(45f, 125f, 0f);
            RenderSettings.sun = sun;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader) { name = "Coastal afternoon sky" };
                sky.SetColor("_SkyTint", new Color(0.44f, 0.59f, 0.66f));
                sky.SetColor("_GroundColor", new Color(0.42f, 0.42f, 0.35f));
                sky.SetFloat("_AtmosphereThickness", 0.75f);
                sky.SetFloat("_Exposure", 1.12f);
                SaveAsset(sky, directory, "CoastalSky.mat");
                RenderSettings.skybox = sky;
            }

            // 影を真っ黒にせず、冷たい天空光を補う
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.60f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.35f, 0.35f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.17f, 0.14f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 0.75f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.56f, 0.67f, 0.67f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 210f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Coastal color and glints";
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.18f);
            bloom.threshold.Override(1.25f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(new Color(1f, 0.94f, 0.8f));
            var toneMapping = profile.Add<Tonemapping>(true);
            toneMapping.mode.Override(TonemappingMode.ACES);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.45f);
            SaveAsset(profile, directory, "CoastalVolume.asset");
            foreach (var component in profile.components)
                AssetDatabase.AddObjectToAsset(component, profile);

            var volume = new GameObject("Coastal atmosphere").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        /// <summary>海を主役にした斜め見下ろしの撮影用カメラを生成する</summary>
        /// <example>BuildCamera()で浅瀬と桟橋を一画面に収める</example>
        private static void BuildCamera()
        {
            var cameraObject = new GameObject("Ocean look development camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 220f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.transform.position = new Vector3(20f, 29f, -54f);
            camera.transform.LookAt(new Vector3(-1.5f, -1f, -16f));

            // 屈折と水深に必要なテクスチャを、このカメラにだけ明示的に要求する
            var data = camera.GetUniversalAdditionalCameraData();
            data.requiresColorOption = CameraOverrideOption.On;
            data.requiresDepthOption = CameraOverrideOption.On;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<FMODUnity.StudioListener>();
        }

        /// <summary>微細な色のばらつきを持つURP Lit素材を生成して保存する</summary>
        /// <param name="directory">保存先</param>
        /// <param name="name">素材名</param>
        /// <param name="color">基準色</param>
        /// <param name="smoothness">表面の滑らかさ</param>
        /// <param name="variation">明暗のばらつき</param>
        /// <returns>保存済みのマテリアル</returns>
        /// <example>CreateSurface(folder, "Sand", Color.yellow, 0.1f, 0.1f)</example>
        private static Material CreateSurface(string directory, string name, Color color, float smoothness, float variation)
        {
            const int resolution = 128;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, true, false)
            {
                name = name + " grain",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color[resolution * resolution];
            var random = new Random(163);
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float broad = Mathf.PerlinNoise(x / 16f + 17f, y / 16f + 41f) - 0.5f;
                    float grain = (float)random.NextDouble() - 0.5f;
                    pixels[y * resolution + x] = Color.Lerp(Color.white, new Color(0.5f, 0.5f, 0.5f), variation * (0.5f + broad * 0.7f + grain * 0.6f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true, true);
            SaveAsset(texture, directory, name.Replace(' ', '_') + "_Grain.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            SaveAsset(material, directory, name.Replace(' ', '_') + ".mat");
            return material;
        }

        /// <summary>メッシュと素材を持つ描画用オブジェクトを生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先の親</param>
        /// <param name="mesh">共有するメッシュ</param>
        /// <param name="material">共有する素材</param>
        /// <returns>生成したオブジェクト</returns>
        /// <example>CreateMeshObject("Rock", root, mesh, material)</example>
        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance.AddComponent<MeshRenderer>().sharedMaterial = material;
            return instance;
        }

        /// <summary>当たり判定を持たない装飾用の直方体を生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先の親</param>
        /// <param name="position">親を基準とした位置</param>
        /// <param name="size">各軸の大きさ</param>
        /// <param name="material">共有する素材</param>
        /// <returns>生成したオブジェクト</returns>
        /// <example>CreateBox("Plank", root, Vector3.zero, new Vector3(2, 0.1f, 0.5f), wood)</example>
        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = size;
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
            return instance;
        }

        /// <summary>指定した二点を結ぶ細い円柱を生成する</summary>
        /// <param name="name">オブジェクト名</param>
        /// <param name="parent">配置先の親</param>
        /// <param name="start">親を基準とした始点</param>
        /// <param name="end">親を基準とした終点</param>
        /// <param name="radius">円柱の半径</param>
        /// <param name="material">共有する素材</param>
        /// <example>CreateRod("Rope", root, a, b, 0.02f, hemp)</example>
        private static void CreateRod(string name, Transform parent, Vector3 start, Vector3 end, float radius, Material material)
        {
            var instance = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = (start + end) * 0.5f;
            instance.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            instance.transform.localScale = new Vector3(radius * 2f, (end - start).magnitude * 0.5f, radius * 2f);
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
        }

        /// <summary>既存ファイルを上書きせずに生成したアセットを保存する</summary>
        /// <param name="asset">保存するアセット</param>
        /// <param name="directory">保存先のフォルダー</param>
        /// <param name="filename">希望するファイル名</param>
        /// <example>SaveAsset(mesh, folder, "Beach.asset")で独立したアセットにする</example>
        private static void SaveAsset(UnityEngine.Object asset, string directory, string filename)
        {
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{directory}/{filename}"));
        }

        /// <summary>不足しているアセットフォルダーを親から順に作成する</summary>
        /// <param name="path">Assetsから始まるフォルダーパス</param>
        /// <example>EnsureDirectory("Assets/_Project/Graphics/Ocean")</example>
        private static void EnsureDirectory(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}

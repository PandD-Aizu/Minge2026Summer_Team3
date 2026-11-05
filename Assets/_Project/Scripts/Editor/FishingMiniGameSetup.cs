using System;
using System.Linq;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Fishing;
using LifetimeScopes;
using MiniGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>釣りの共通設定、魚別設定、Canvasを既存シーンへ追加する</summary>
public static class FishingMiniGameSetup
{
    private const string SettingsFolder = "Assets/_Project/SO/MiniGame/Gauge";
    private const string PrefabPath = "Assets/_Project/Prefab/Canvas/GaugeMiniGameCanvas.prefab";
    private const string FramePath = "Assets/_Project/Graphics/G_MiniGame/fishing_meter_frame.png";

    /// <summary>現在開いている釣りシーンに不足する設定と画面を追加する</summary>
    /// <returns>接続した魚の数</returns>
    /// <example>Tools/Fishing/Install Mini Gamesから実行してシーンを保存する</example>
    [MenuItem("Tools/Fishing/Install Mini Games")]
    public static int Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してから実行する");
        var scope = UnityEngine.Object.FindAnyObjectByType<FishingSceneLifetimeScope>();
        if (scope == null) throw new InvalidOperationException("FishingStageを開いてから実行する");
        if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets/_Project/SO/MiniGame", "Gauge");
        var bite = LoadOrCreate<FishingBiteSettings>("Assets/_Project/SO/MiniGame/SO_FishingBite.asset");
        var easy = Preset("Easy", 0.34f, 0.25f, 0.1f, 0.26f);
        var normal = Preset("Normal", 0.27f, 0.2f, 0.14f, 0.42f);
        var hard = Preset("Hard", 0.20f, 0.17f, 0.18f, 0.65f);
        var fishAssets = AssetDatabase.FindAssets("t:FishDefinition", new[] { "Assets/_Project/SO/Item/Fish" });
        foreach (string guid in fishAssets)
        {
            var fish = AssetDatabase.LoadAssetAtPath<FishDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            // 再実行しても、すでに調整した重みや魚の動きを上書きしない
            if (fish.MiniGameOptions != null && fish.MiniGameOptions.Length > 0) continue;
            string path = $"{SettingsFolder}/SO_Gauge_Fish_{fish.ItemId}.asset";
            var gauge = AssetDatabase.LoadAssetAtPath<GaugeMiniGameSettings>(path);
            if (gauge == null)
            {
                var source = fish.ItemId >= 1009 ? hard : fish.ItemId == 1001 ? easy : normal;
                gauge = UnityEngine.Object.Instantiate(source);
                gauge.name = $"SO_Gauge_Fish_{fish.ItemId}";
                gauge.Motion = fish.ItemId switch
                {
                    1001 => FishMotion.Calm,
                    1002 or 1007 or 1010 => FishMotion.Dart,
                    1003 or 1004 or 1005 => FishMotion.Bottom,
                    1008 or 1011 => FishMotion.Top,
                    _ => FishMotion.Smooth
                };
                AssetDatabase.CreateAsset(gauge, path);
            }

            var groups = (fish.MiniGameSettings ?? Array.Empty<MiniGameSettings>()).Where(s => s != null)
                .GroupBy(s => s.GameType).Select(g => new MiniGameOption { Type = g.Key, Settings = g.Distinct().ToArray() }).ToList();
            if (groups.All(g => g.Type != _Project.Scripts.Data.Enum.MiniGameType.Gauge))
                groups.Add(new MiniGameOption { Type = _Project.Scripts.Data.Enum.MiniGameType.Gauge, Settings = new MiniGameSettings[] { gauge } });
            var data = new SerializedObject(fish);
            var options = data.FindProperty("_miniGameOptions");
            options.arraySize = groups.Count;
            for (int i = 0; i < groups.Count; i++)
            {
                var option = options.GetArrayElementAtIndex(i);
                option.FindPropertyRelative("Type").intValue = (int)groups[i].Type;
                option.FindPropertyRelative("Weight").floatValue = 1;
                var settings = option.FindPropertyRelative("Settings");
                settings.arraySize = groups[i].Settings.Length;
                for (int j = 0; j < settings.arraySize; j++) settings.GetArrayElementAtIndex(j).objectReferenceValue = groups[i].Settings[j];
            }
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        var scopeData = new SerializedObject(scope);
        var view = scopeData.FindProperty("_gaugeMiniGameView").objectReferenceValue as GaugeMiniGameView;
        if (view == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = BuildCanvas();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                UnityEngine.Object.DestroyImmediate(root);
            }
            view = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scope.gameObject.scene)).GetComponent<GaugeMiniGameView>();
        }
        scopeData.FindProperty("_gaugeMiniGameView").objectReferenceValue = view;
        scopeData.FindProperty("_fishingBiteSettings").objectReferenceValue = bite;
        scopeData.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scope.gameObject.scene);
        AssetDatabase.SaveAssets();
        return fishAssets.Length;
    }

    /// <summary>未作成の設定だけを初期値で保存する</summary>
    /// <typeparam name="T">設定アセットの型</typeparam>
    /// <param name="path">保存先</param>
    /// <returns>既存または新規の設定</returns>
    /// <example>LoadOrCreate&lt;FishingBiteSettings&gt;(path)</example>
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    /// <summary>再利用できる難易度プリセットを初回だけ生成する</summary>
    /// <param name="name">難易度名</param>
    /// <param name="size">バーの高さ</param>
    /// <param name="gain">秒ごとのゲージ増加</param>
    /// <param name="loss">秒ごとのゲージ減少</param>
    /// <param name="speed">魚の速度</param>
    /// <returns>生成した設定</returns>
    /// <example>Preset("Easy", 0.34f, 0.25f, 0.1f, 0.26f)</example>
    private static GaugeMiniGameSettings Preset(string name, float size, float gain, float loss, float speed)
    {
        string path = $"{SettingsFolder}/SO_Gauge_{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<GaugeMiniGameSettings>(path);
        if (existing != null) return existing;
        var settings = LoadOrCreate<GaugeMiniGameSettings>(path);
        settings.BarSize = size; settings.GainPerSecond = gain; settings.LossPerSecond = loss; settings.FishSpeed = speed;
        EditorUtility.SetDirty(settings);
        return settings;
    }

    /// <summary>縦レーンと捕獲ゲージのCanvasを生成する</summary>
    /// <returns>保存前のCanvasルート</returns>
    /// <example>初回InstallでPrefab化してからシーンに配置する</example>
    private static GameObject BuildCanvas()
    {
        var root = new GameObject("GaugeMiniGameCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        canvas.enabled = false;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var view = root.AddComponent<GaugeMiniGameView>();
        var panel = Rect("Panel", root.transform, new Vector2(300, 650), new Vector2(490, 0));
        Image(panel, new Color(0.035f, 0.09f, 0.13f, 0.96f));
        var lane = Rect("Lane", panel, new Vector2(110, 390), new Vector2(-32, -8));
        Image(lane, new Color(0.07f, 0.19f, 0.24f));
        var bar = Rect("CatchBar", lane, new Vector2(104, 100), Vector2.zero);
        bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0);
        var barImage = Image(bar, new Color(0.35f, 0.88f, 0.65f, 0.9f));
        var fish = Rect("Fish", lane, new Vector2(104, 64), Vector2.zero);
        fish.anchorMin = fish.anchorMax = new Vector2(0.5f, 0);
        var fishImage = Image(fish, Color.white);
        fishImage.preserveAspect = true;
        var track = Rect("CaptureTrack", panel, new Vector2(22, 390), new Vector2(80, -8));
        Image(track, new Color(0.11f, 0.24f, 0.27f));
        var progress = Rect("CaptureFill", track, Vector2.zero, Vector2.zero);
        progress.anchorMin = Vector2.zero; progress.anchorMax = Vector2.one;
        progress.offsetMin = progress.offsetMax = Vector2.zero;
        Image(progress, new Color(0.83f, 0.73f, 0.43f));
        var data = new SerializedObject(view);
        Set(data, "_canvas", canvas); Set(data, "_lane", lane); Set(data, "_bar", bar);
        Set(data, "_fish", fish); Set(data, "_fishImage", fishImage); Set(data, "_barImage", barImage);
        Set(data, "_progress", progress);
        data.ApplyModifiedPropertiesWithoutUndo();
        ApplyPixelArt(root);
        return root;
    }

    /// <summary>既存のゲージをドット絵の木枠に合わせ、文字と旧装飾を取り除く</summary>
    /// <param name="root">生成直後またはPrefab編集用に開いたCanvas</param>
    /// <example>ApplyPixelArt(PrefabUtility.LoadPrefabContents(path))の後にPrefabを保存する</example>
    public static void ApplyPixelArt(GameObject root)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        if (sprite == null) throw new InvalidOperationException("釣りゲージの木枠をSpriteとしてインポートする");
        var panel = (RectTransform)root.transform.Find("Panel");

        // 1024×1536の原画を40%で表示し、透明な外周越しにゲーム画面を見せる
        panel.sizeDelta = new Vector2(409.6f, 614.4f);
        var frame = panel.GetComponent<UnityEngine.UI.Image>();
        frame.sprite = sprite;
        frame.color = Color.white;
        frame.preserveAspect = true;
        foreach (string name in new[] { "Accent", "Title", "Instruction", "Progress", "Controls" })
        {
            var old = panel.Find(name);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
        }

        // 原画の青い窓と右の細い溝に、動的な表示領域を重ねる
        var lane = (RectTransform)panel.Find("Lane");
        lane.sizeDelta = new Vector2(88, 496);
        lane.anchoredPosition = new Vector2(-5, 5.6f);
        lane.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        var bar = (RectTransform)lane.Find("CatchBar");
        bar.sizeDelta = new Vector2(84, bar.sizeDelta.y);
        var outline = bar.GetComponent<UnityEngine.UI.Outline>() ?? bar.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.16f, 0.32f, 0.04f);
        outline.effectDistance = new Vector2(2, -2);
        var fish = (RectTransform)lane.Find("Fish");
        fish.sizeDelta = new Vector2(80, 64);
        var track = (RectTransform)panel.Find("CaptureTrack");
        track.sizeDelta = new Vector2(14, 496);
        track.anchoredPosition = new Vector2(61.8f, 5.6f);
        track.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        track.Find("CaptureFill").GetComponent<UnityEngine.UI.Image>().color = new Color(0.49f, 0.94f, 0.12f);
    }

    /// <summary>中央基準のRectTransformを作成する</summary>
    /// <param name="name">オブジェクト名</param>
    /// <param name="parent">配置先</param>
    /// <param name="size">幅と高さ</param>
    /// <param name="position">中央からの位置</param>
    /// <returns>生成したRectTransform</returns>
    /// <example>Rect("Panel", canvas.transform, size, position)</example>
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    /// <summary>操作を遮らない表示用画像を付ける</summary>
    /// <param name="rect">対象</param>
    /// <param name="color">表示色</param>
    /// <returns>画像コンポーネント</returns>
    /// <example>Image(panel, Color.black)</example>
    private static UnityEngine.UI.Image Image(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color; image.raycastTarget = false;
        return image;
    }

    /// <summary>シリアライズされた参照を設定する</summary>
    /// <param name="data">編集対象</param>
    /// <param name="name">フィールド名</param>
    /// <param name="value">保存する参照</param>
    /// <example>Set(data, "_canvas", canvas)</example>
    private static void Set(SerializedObject data, string name, UnityEngine.Object value) => data.FindProperty(name).objectReferenceValue = value;
}

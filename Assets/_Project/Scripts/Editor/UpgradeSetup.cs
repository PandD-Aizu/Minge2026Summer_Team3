using System.IO;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Item;
using _Project.Scripts.Data.Tool;
using UnityEditor;
using UnityEngine;
using Upgrades;

/// <summary>強化と調合の初期アセットを作る 既存の調整値は上書きしない</summary>
public static class UpgradeSetup
{
    /// <summary>2種類の強化とアジから作る薬を登録する</summary>
    /// <example>Tools/Economy/Create Upgrade Assetsから初回だけ実行する</example>
    [MenuItem("Tools/Economy/Create Upgrade Assets")]
    public static void Run()
    {
        const string directory = "Assets/_Project/AddressableAssets/Economy/Upgrades";
        if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets/_Project/AddressableAssets/Economy", "Upgrades");
        CreateUpgrade(directory + "/MoveSpeed.asset", 1, "移動速度アップ", "元の移動速度に効果量を加算", UpgradeEffect.MoveSpeed,
            new[] { 0.05f, 0.05f, 0.10f, 0.10f, 0.15f });
        CreateUpgrade(directory + "/FishingGain.asset", 2, "釣りやすさの向上", "成功ゲージの増加速度に効果量を加算", UpgradeEffect.FishingGain,
            new[] { 0.10f, 0.10f, 0.15f, 0.15f, 0.20f });

        // 調合専用の道具として登録し、金銭購入の一覧には出さない
        const string potionPath = "Assets/_Project/AddressableAssets/Economy/StealthPotion.asset";
        var potion = AssetDatabase.LoadAssetAtPath<ToolDefinition>(potionPath);
        if (potion == null)
        {
            potion = ScriptableObject.CreateInstance<ToolDefinition>();
            var data = new SerializedObject(potion);
            data.FindProperty("itemId").intValue = 3000;
            data.FindProperty("itemName").stringValue = "気配消しのポーション";
            data.FindProperty("description").stringValue = "アジから調合したポーション\n使用すると次の明け方まで姿が透け、敵の追跡を振り切れる";
            data.FindProperty("_purchasePrice").intValue = 0;
            data.FindProperty("_toolImage").objectReferenceValue = CreatePotionIcon();
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(potion, potionPath);
        }

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/SO/Item/ItemCatalog.asset");
        if (catalog.FindById(potion.ItemId) == null)
        {
            var data = new SerializedObject(catalog);
            var items = data.FindProperty("_items");
            items.arraySize++;
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = potion;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        const string recipePath = "Assets/_Project/AddressableAssets/Economy/Exchange_Aji_StealthPotion.asset";
        if (AssetDatabase.LoadAssetAtPath<ExchangeRecipeDefinition>(recipePath) == null)
        {
            var recipe = ScriptableObject.CreateInstance<ExchangeRecipeDefinition>();
            var data = new SerializedObject(recipe);
            data.FindProperty("costFish").objectReferenceValue = catalog.FindById(1001);
            data.FindProperty("costAmount").intValue = 1;
            data.FindProperty("rewardItem").objectReferenceValue = potion;
            data.FindProperty("rewardAmount").intValue = 1;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(recipe, recipePath);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("UPGRADE ASSETS READY");
    }

    /// <summary>5段階の仮価格と追加割合を持つSOを作る</summary>
    /// <param name="path">保存先</param><param name="id">保存ID</param><param name="name">表示名</param>
    /// <param name="description">説明</param><param name="effect">対象能力</param><param name="amounts">段階ごとの追加割合</param>
    /// <example>初期生成後の価格調整はInspectorで行う</example>
    private static void CreateUpgrade(string path, int id, string name, string description, UpgradeEffect effect, float[] amounts)
    {
        var existing = AssetDatabase.LoadAssetAtPath<UpgradeDefinition>(path);
        if (existing != null)
        {
            // 追加したシリアライズ項目の既定値も記録し、調整済みの値は維持する
            EditorUtility.SetDirty(existing);
            return;
        }
        var definition = ScriptableObject.CreateInstance<UpgradeDefinition>();
        var data = new SerializedObject(definition);
        data.FindProperty("_id").intValue = id;
        data.FindProperty("_displayName").stringValue = name;
        data.FindProperty("_description").stringValue = description;
        data.FindProperty("_effect").enumValueIndex = (int)effect;
        data.FindProperty("_maxLevel").intValue = 5;
        var stages = data.FindProperty("_stages");
        stages.arraySize = 5;
        int[] prices = { 100, 200, 300, 500, 800 };
        for (int i = 0; i < stages.arraySize; i++)
        {
            var stage = stages.GetArrayElementAtIndex(i);
            stage.FindPropertyRelative("_price").intValue = prices[i];
            stage.FindPropertyRelative("_effectAmount").floatValue = amounts[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, path);
    }

    /// <summary>調合結果と所持欄で識別できる簡素な薬瓶アイコンを作る</summary>
    /// <returns>透明背景の薬瓶Sprite</returns>
    /// <example>ポーションSOの初回生成時に呼ぶ</example>
    private static Sprite CreatePotionIcon()
    {
        const string path = "Assets/_Project/AddressableAssets/Economy/StealthPotion.png";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            try
            {
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    bool body = (x - 32) * (x - 32) + (y - 25) * (y - 25) < 21 * 21;
                    bool neck = x >= 24 && x <= 40 && y >= 38 && y <= 53;
                    bool cork = x >= 23 && x <= 41 && y >= 52 && y <= 59;
                    Color color = Color.clear;
                    if (body || neck) color = new Color(0.18f, 0.23f, 0.29f);
                    if (body && (x - 32) * (x - 32) + (y - 25) * (y - 25) < 17 * 17)
                        color = y < 29 ? new Color(0.43f, 0.66f, 0.72f) : new Color(0.82f, 0.92f, 0.94f);
                    if (neck && x > 27 && x < 37 && y < 52) color = new Color(0.82f, 0.92f, 0.94f);
                    if (cork) color = new Color(0.50f, 0.34f, 0.22f);
                    texture.SetPixel(x, y, color);
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

using System;
using System.IO;
using System.Reflection;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;
using ExchangePoint;
using InventoryData;
using SaveSettings;
using UnityEditor;
using UnityEngine;

/// <summary>取引と保存の境界条件を実データで検証する</summary>
public static class EconomyChecks
{
    /// <summary>独立した一時セーブで検証し、実プレイヤーの保存には触れない</summary>
    /// <example>Unity -batchmode -executeMethod EconomyChecks.Run</example>
    [MenuItem("Tools/Validation/Economy")]
    public static void Run()
    {
        EnsureFont();
        var path = Path.Combine(Path.GetTempPath(), "economy-" + Guid.NewGuid() + ".json");
        var save = new SaveService(path);
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/SO/Item/ItemCatalog.asset");
        var fish = (FishDefinition)catalog.FindById(1001);
        var item = catalog.FindById(2000);
        var recipes = Resources.LoadAll<ExchangeRecipeDefinition>("Economy");
        try
        {
            var inventory = new Inventory(save);
            var wallet = new Wallet(inventory);
            var service = new ExchangePointService(inventory, catalog);
            Check(wallet.Balance == 100, "初期残高");
            Check(service.TryBuy(item, 1, out _), "購入成功");
            Check(wallet.Balance == 0 && inventory.GetCount(2000) == 1, "購入の増減");
            Check(new Inventory(save).Balance == 0, "残高0の復元");
            Check(!service.TryBuy(item, 1, out _), "残高不足");
            Check(!service.TrySell(fish, 1, out _), "魚不足");
            inventory.Add(1001, 3);
            Check(service.TrySell(fish, 2, out _), "複数売却");
            Check(wallet.Balance == 200 && inventory.GetCount(1001) == 1, "売却の増減");
            Check(!service.TrySell(fish, 0, out _) && !service.TrySell(fish, -1, out _), "不正数量");
            Check(!inventory.Remove(1001, -1), "負の消費");
            Check(recipes.Length == 2, "交換レシピ");
            Check(service.TryExchange(recipes[0], 1, out _), "直接交換");
            Check(!service.TryExchange(recipes[0], 1, out _), "連続交換の不足");
            Check(wallet.Balance == 200 && inventory.GetCount(1001) == 0, "直接交換は残高を保持");
            var restored = new Inventory(save);
            Check(restored.Balance == 200 && restored.GetCount(recipes[0].RewardItem.ItemId) == inventory.GetCount(recipes[0].RewardItem.ItemId), "所持品復元");
            Check(wallet.TryDeposit(int.MaxValue - 200), "最大残高");
            inventory.Add(1001);
            Check(!service.TrySell(fish, 1, out _) && inventory.GetCount(1001) == 1, "残高上限で魚を保持");
            var before = inventory.GetCount(2000);
            inventory.Add(2000, int.MaxValue - before);
            Check(!service.TryBuy(item, 1, out _) && wallet.Balance == int.MaxValue, "所持数上限で残高を保持");
            Check(!service.TryExchange(recipes[0], int.MaxValue, out _), "交換数量の乗算上限");
            Check(Resources.Load<Sprite>("Economy/ShellCurrency") != null, "貝殻Sprite");

            // 旧形式と破損セーブを別々に検証する
            save.WriteSaveData(new GameData { useDefaultAudioSettings = true });
            Check(new Inventory(save).Balance == 100, "旧形式の初期化");
            save.WriteSaveData(new GameData { hasEconomy = true, balance = -1 });
            Check(!new Wallet(new Inventory(save)).TryDeposit(1), "不正保存値の保護");
            Check(save.LoadSaveData().balance == -1, "不正セーブを上書きしない");
            Debug.Log("ECONOMY CHECKS PASSED");
        }
        finally
        {
            save.DeleteSaveData();
        }
    }

    /// <summary>既存の日本語フォントから取引UI専用の動的フォントを生成する</summary>
    /// <example>初回の検証前に呼ぶ</example>
    private static void EnsureFont()
    {
        const string path = "Assets/_Project/Resources/Economy/TradeFont.asset";
        if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(path) != null) return;
        var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/KleeOne-Regular.ttf");
        var font = TMPro.TMP_FontAsset.CreateFontAsset(source, 48, 5,
            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, TMPro.AtlasPopulationMode.Dynamic, true);
        font.name = "TradeFont";
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        AssetDatabase.SaveAssets();
    }

    /// <summary>条件が成立しない場合に検証を失敗させる</summary>
    /// <param name="condition">期待条件</param><param name="name">検証名</param>
    /// <example>Check(balance == 100, "初期残高")</example>
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Economy check failed: " + name);
        Debug.Log("Economy OK: " + name);
    }
}

using System;
using System.IO;
using System.Linq;
using _Project.Scripts.Data;
using _Project.Scripts.Data.Item;
using ExchangePoint;
using InventoryData;
using MiniGame;
using SaveSettings;
using UnityEditor;
using UnityEngine;
using Upgrades;

/// <summary>永久強化と魚の調合を一時セーブで検証する</summary>
public static class UpgradeChecks
{
    /// <summary>購入境界、保存失敗、実際の能力計算と旧セーブ互換を確認する</summary>
    /// <example>Tools/Validation/Upgrades And Alchemyから実行する</example>
    [MenuItem("Tools/Validation/Upgrades And Alchemy")]
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), "upgrades-" + Guid.NewGuid() + ".json");
        var save = new SaveService(path);
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/SO/Item/ItemCatalog.asset");
        try
        {
            Check(save.WriteSaveData(new GameData
            {
                hasEconomy = true, balance = 4000,
                audioSettings = new AudioSettingsData { masterVolume = 0.42f },
                shownDialogueIds = new System.Collections.Generic.List<string> { "preserve-me" }
            }), "検証用セーブ");
            var inventory = new Inventory(save);
            var service = new UpgradeService(inventory, EconomyChecks.Assets);
            Check(service.Definitions.Count == 2, "強化2種類");
            var movement = service.Definitions.Single(d => d.Effect == UpgradeEffect.MoveSpeed);
            var fishing = service.Definitions.Single(d => d.Effect == UpgradeEffect.FishingGain);
            Check(service.MoveSpeedMultiplier == 1 && service.FishingGainMultiplier == 1, "初期倍率");

            // 価格は購入する段階ごとに変化し、以前の段階の再送では引き落とさない
            Check(service.TryBuy(movement, 0, out _), "移動速度1段階目");
            Check(inventory.Balance == 3900 && inventory.Items.Count == 0, "強化は所持品に追加しない");
            Check(!service.TryBuy(movement, 0, out _) && inventory.Balance == 3900, "古い段階の二重購入を拒否");
            int[] balances = { 3700, 3400, 2900, 2100 };
            for (int level = 1; level < 5; level++)
                Check(service.TryBuy(movement, level, out _) && inventory.Balance == balances[level - 1], "段階別価格 " + level);
            Check(!service.TryBuy(movement, 5, out _) && inventory.Balance == 2100, "最大5回");
            Check(Mathf.Abs(service.MoveSpeedMultiplier - 1.45f) < 0.0001f, "移動速度累積45%");
            for (int level = 0; level < 5; level++) Check(service.TryBuy(fishing, level, out _), "釣り強化 " + level);
            Check(inventory.Balance == 200 && Mathf.Abs(service.FishingGainMultiplier - 1.7f) < 0.0001f, "釣り累積70%");
            var restored = new UpgradeService(new Inventory(save), EconomyChecks.Assets);
            Check(restored.GetLevel(movement) == 5 && restored.GetLevel(fishing) == 5, "再生成後の強化復元");
            Check(save.LoadSaveData().audioSettings.masterVolume == 0.42f
                && save.HasShownDialogue("preserve-me"), "音量と会話履歴の保持");

            VerifyEffects(service);

            // 調合は残高を変えず、同じ魚を2回消費できない
            var recipe = EconomyChecks.Assets.Recipes.Single(r => r.RewardItem.ItemId == 3000);
            var trade = new ExchangePointService(inventory, catalog);
            inventory.Add(1001);
            Check(trade.TryExchange(recipe, 1, out _), "アジから薬を調合");
            Check(inventory.GetCount(1001) == 0 && inventory.GetCount(3000) == 1 && inventory.Balance == 200, "調合の増減");
            Check(!trade.TryExchange(recipe, 1, out _) && !trade.TryExchange(recipe, 0, out _)
                && !trade.TryExchange(recipe, -1, out _), "材料不足と無効数量");
            Check(new Inventory(save).GetCount(3000) == 1, "ポーション保存復元");
            Check(!trade.TryBuy(catalog.FindById(3000), 1, out _), "ポーションは調合専用");

            // 保存先への書き込みを意図的に失敗させ、状態と既存ファイルの原子性を確認する
            Check(save.WriteSaveData(new GameData { hasEconomy = true, balance = 500 }), "失敗検証の初期化");
            inventory = new Inventory(save);
            service = new UpgradeService(inventory, EconomyChecks.Assets);
            inventory.Add(1001);
            string before = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            Debug.Log("UPGRADE EXPECTED SAVE FAILURES BEGIN");
            Check(!service.TryBuy(movement, 0, out _) && inventory.Balance == 500 && service.GetLevel(movement) == 0, "保存失敗で強化と残高を維持");
            Check(!new ExchangePointService(inventory, catalog).TryExchange(recipe, 1, out _)
                && inventory.GetCount(1001) == 1 && inventory.GetCount(3000) == 0, "保存失敗で材料と完成品を維持");
            Debug.Log("UPGRADE EXPECTED SAVE FAILURES END");
            Check(File.ReadAllText(path) == before, "保存失敗でも既存ファイルを維持");
            Directory.Delete(path + ".tmp");

            // 旧セーブのフィールド欠落は未強化として扱う
            File.WriteAllText(path, "{\"hasEconomy\":true,\"balance\":99,\"ownedItems\":[]}");
            inventory = new Inventory(save);
            service = new UpgradeService(inventory, EconomyChecks.Assets);
            Check(service.GetLevel(movement) == 0 && service.MoveSpeedMultiplier == 1, "旧セーブ互換");
            Check(!service.TryBuy(movement, 0, out _) && inventory.Balance == 99 && service.GetLevel(movement) == 0, "残高不足");
            File.WriteAllText(path, "{\"hasEconomy\":true,\"balance\":500,\"ownedItems\":[],\"upgradeLevels\":[{\"upgradeId\":1,\"level\":6}]}");
            inventory = new Inventory(save);
            Check(!new UpgradeService(inventory, EconomyChecks.Assets).TryBuy(movement, 0, out _) && save.LoadSaveData().upgradeLevels[0].level == 6,
                "不正な保存段階を上書きしない");
            Check(FishingMiniGameChecks.Run() != null, "既存釣り検証");
            Debug.Log("UPGRADE CHECKS PASSED");
        }
        finally
        {
            save.DeleteSaveData();
            if (Directory.Exists(path + ".tmp")) Directory.Delete(path + ".tmp");
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }
    }

    /// <summary>実際の移動コンポーネントと釣りモデルで倍率の反映を比較する</summary>
    /// <param name="service">最大強化済みの共有状態</param>
    /// <example>固定乱数で強化前後のゲージを比較する</example>
    private static void VerifyEffects(UpgradeService service)
    {
        var player = new GameObject("UpgradeMovementCheck");
        player.SetActive(false);
        var gauge = ScriptableObject.CreateInstance<GaugeMiniGameSettings>();
        try
        {
            var movement = player.AddComponent<PlayerMovement>();
            float original = movement.CurrentMoveSpeed;
            movement.Construct(service);
            Check(Mathf.Abs(movement.CurrentMoveSpeed - original * 1.45f) < 0.0001f, "移動への倍率反映");
            gauge.HeightRange = new Vector2(0.15f, 0.15f);
            gauge.BarSize = 0.3f;
            gauge.GraceSeconds = 0;
            gauge.GainPerSecond = 0.2f;
            var basic = new GaugeMiniGameModel(gauge, new System.Random(7));
            var upgraded = new GaugeMiniGameModel(gauge, new System.Random(7), service.FishingGainMultiplier);
            basic.Tick(0.5f, false);
            upgraded.Tick(0.5f, false);
            Check(Mathf.Abs((upgraded.Progress - gauge.InitialProgress) / (basic.Progress - gauge.InitialProgress) - 1.7f) < 0.001f,
                "釣りゲージの実増加量70%アップ");
            Check(gauge.GainPerSecond == 0.2f && basic.TimeProgress == upgraded.TimeProgress, "設定SOと制限時間を維持");
        }
        finally { UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(gauge); }
    }

    /// <summary>条件不成立ならバッチ検証を失敗させる</summary>
    /// <param name="condition">期待条件</param><param name="name">検証名</param>
    /// <example>Check(level == 5, "上限")</example>
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("Upgrade check failed: " + name);
        Debug.Log("Upgrade OK: " + name);
    }
}

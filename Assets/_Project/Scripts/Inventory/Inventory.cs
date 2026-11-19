using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SaveSettings;
using VContainer;

namespace InventoryData
{
    /// <summary>所持品と残高を同一スナップショットで保存する</summary>
    public class Inventory
    {
        private Dictionary<int, int> _items = new();
        // 強化はアイテム所持数と分離し、残高と同じスナップショットで保存する
        private Dictionary<int, int> _upgradeLevels = new();
        private readonly SaveService _save;
        private readonly bool _loadFailed;
        public IReadOnlyDictionary<int, int> Items => new ReadOnlyDictionary<int, int>(_items);
        public int Balance { get; private set; } = 100;

        /// <summary>保存先を持たない検証用の所持品を作る</summary>
        /// <example>new Inventory()でメモリ上だけの検証を行う</example>
        public Inventory() { }

        /// <summary>既存セーブから所持品と残高を復元する</summary>
        /// <param name="save">共有するセーブサービス</param>
        /// <example>Root Scopeから生成する</example>
        [Inject]
        public Inventory(SaveService save)
        {
            _save = save;
            var data = save.LoadSaveData();
            _loadFailed = data == null && save.Exists;
            var restoredLevels = new Dictionary<int, int>();
            if (data?.upgradeLevels != null)
            {
                foreach (var upgrade in data.upgradeLevels)
                {
                    if (upgrade == null || upgrade.upgradeId <= 0 || upgrade.level <= 0
                        || upgrade.level > Upgrades.UpgradeDefinition.MaximumLevel || restoredLevels.ContainsKey(upgrade.upgradeId))
                    { _loadFailed = true; return; }
                    restoredLevels.Add(upgrade.upgradeId, upgrade.level);
                }
            }
            if (data?.hasEconomy != true)
            {
                if (restoredLevels.Count > 0) _loadFailed = true;
                return;
            }

            // 不正な保存値は取引で上書きせず、読み込み失敗として扱う
            if (data.balance < 0 || data.ownedItems == null) { _loadFailed = true; return; }
            var restored = new Dictionary<int, int>();
            foreach (var item in data.ownedItems)
            {
                if (item == null || item.itemId <= 0 || item.count <= 0 || restored.ContainsKey(item.itemId))
                { _loadFailed = true; return; }
                restored.Add(item.itemId, item.count);
            }
            _items = restored;
            _upgradeLevels = restoredLevels;
            Balance = data.balance;
        }

        /// <summary>指定したアイテムの所持数を返す</summary>
        /// <param name="itemId">アイテムID</param>
        /// <returns>未所持なら0</returns>
        /// <example>GetCount(1001)</example>
        public int GetCount(int itemId) => _items.TryGetValue(itemId, out var count) ? count : 0;

        /// <summary>アイテム所持数と分離した強化段階を返す</summary>
        /// <param name="upgradeId">強化の保存ID</param><returns>未購入なら0</returns>
        /// <example>GetUpgradeLevel(1)で移動速度の段階を調べる</example>
        public int GetUpgradeLevel(int upgradeId) => _upgradeLevels.TryGetValue(upgradeId, out int level) ? level : 0;

        /// <summary>次の強化段階と支払いを同時に確定する</summary>
        /// <param name="upgradeId">強化ID</param><param name="expectedLevel">確認済みの現在段階</param>
        /// <param name="price">次の段階の価格</param><param name="error">失敗理由</param>
        /// <returns>保存成功時のみtrue</returns><example>UpgradeServiceから呼び出す</example>
        internal bool TryPurchaseUpgrade(int upgradeId, int expectedLevel, int price, out string error)
        {
            error = "強化段階が変わったか、上限に達している";
            if (upgradeId <= 0 || expectedLevel < 0 || expectedLevel >= Upgrades.UpgradeDefinition.MaximumLevel
                || GetUpgradeLevel(upgradeId) != expectedLevel || price <= 0) return false;
            var nextLevels = new Dictionary<int, int>(_upgradeLevels) { [upgradeId] = expectedLevel + 1 };
            return TryCommit(_items, (long)Balance - price, nextLevels, out error);
        }

        /// <summary>正の数量を所持しているか調べる</summary>
        /// <param name="itemId">アイテムID</param><param name="amount">必要数</param>
        /// <returns>必要数を満たすならtrue</returns>
        /// <example>Has(1001, 2)</example>
        public bool Has(int itemId, int amount = 1) => amount > 0 && GetCount(itemId) >= amount;

        /// <summary>所持品を追加して保存し、失敗時は例外で呼び出し元へ通知する</summary>
        /// <param name="itemId">アイテムID</param><param name="amount">追加する正の数量</param>
        /// <example>釣果の確定時にAdd(1001)を呼ぶ</example>
        public void Add(int itemId, int amount = 1)
        {
            if (amount <= 0 || !TryChange(itemId, amount, 0, 0, 0, out var error))
                throw new InvalidOperationException("所持品を追加できない");
        }

        /// <summary>所持品を減らして保存する</summary>
        /// <param name="itemId">アイテムID</param><param name="amount">消費する正の数量</param>
        /// <returns>保存まで成功した場合はtrue</returns>
        /// <example>Remove(1001, 2)</example>
        public bool Remove(int itemId, int amount = 1) => amount > 0 && TryChange(itemId, -amount, 0, 0, 0, out _);

        /// <summary>取引全体を検証し、保存成功後にメモリへ一括反映する</summary>
        /// <param name="firstId">増減対象ID、0なら変更なし</param><param name="firstDelta">増減量</param>
        /// <param name="secondId">もう一つの対象ID</param><param name="secondDelta">もう一つの増減量</param>
        /// <param name="moneyDelta">残高の増減量</param><param name="error">失敗理由</param>
        /// <returns>取引が確定した場合はtrue</returns>
        /// <example>売却はTryChange(1001, -1, 0, 0, 100, out error)</example>
        internal bool TryChange(int firstId, long firstDelta, int secondId, long secondDelta, long moneyDelta, out string error)
        {
            error = "保存データを読み込めない";
            if (_loadFailed) return false;
            var next = new Dictionary<int, int>(_items);
            if (!Apply(next, firstId, firstDelta) || !Apply(next, secondId, secondDelta))
            { error = "所持数が不足しているか、数量が上限を超えている"; return false; }
            long balance = Balance + moneyDelta;
            return TryCommit(next, balance, _upgradeLevels, out error);
        }

        /// <summary>所持品・残高・強化段階を保存し、成功後にだけ公開する</summary>
        /// <param name="next">確定予定の所持数</param><param name="balance">確定予定の残高</param>
        /// <param name="levels">確定予定の強化段階</param><param name="error">失敗理由</param>
        /// <returns>保存できた場合true</returns><example>購入と調合の共通確定処理に使う</example>
        private bool TryCommit(Dictionary<int, int> next, long balance, Dictionary<int, int> levels, out string error)
        {
            error = "保存データを読み込めない";
            if (_loadFailed) return false;
            if (balance < 0 || balance > int.MaxValue)
            { error = "残高が不足しているか、金額が上限を超えている"; return false; }

            // 保存に失敗しても現在の所持品と残高は維持する
            if (_save != null)
            {
                var data = _save.LoadSaveData();
                if (data == null && _save.Exists) return false;
                data ??= new GameData { useDefaultAudioSettings = true };
                data.hasEconomy = true;
                data.balance = (int)balance;
                data.ownedItems = new List<OwnedItemData>();
                foreach (var pair in next) data.ownedItems.Add(new OwnedItemData { itemId = pair.Key, count = pair.Value });
                data.upgradeLevels = new List<UpgradeLevelData>();
                foreach (var pair in levels) data.upgradeLevels.Add(new UpgradeLevelData { upgradeId = pair.Key, level = pair.Value });
                if (!_save.WriteSaveData(data)) { error = "保存に失敗したため取引を中止した"; return false; }
            }
            _items = next;
            _upgradeLevels = levels;
            Balance = (int)balance;
            error = null;
            return true;
        }

        /// <summary>作業用コピーへ数量変更を適用する</summary>
        /// <param name="items">作業用所持品</param><param name="id">対象ID</param><param name="delta">増減量</param>
        /// <returns>有効な範囲ならtrue</returns>
        /// <example>確定前の検証に使用する</example>
        private static bool Apply(Dictionary<int, int> items, int id, long delta)
        {
            if (delta == 0) return true;
            if (id <= 0) return false;
            long count = (items.TryGetValue(id, out var current) ? current : 0) + delta;
            if (count < 0 || count > int.MaxValue) return false;
            if (count == 0) items.Remove(id);
            else items[id] = (int)count;
            return true;
        }
    }
}

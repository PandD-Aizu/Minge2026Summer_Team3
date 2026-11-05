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
            if (data?.hasEconomy != true) return;

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
            Balance = data.balance;
        }

        /// <summary>指定したアイテムの所持数を返す</summary>
        /// <param name="itemId">アイテムID</param>
        /// <returns>未所持なら0</returns>
        /// <example>GetCount(1001)</example>
        public int GetCount(int itemId) => _items.TryGetValue(itemId, out var count) ? count : 0;

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
                if (!_save.WriteSaveData(data)) { error = "保存に失敗したため取引を中止した"; return false; }
            }
            _items = next;
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

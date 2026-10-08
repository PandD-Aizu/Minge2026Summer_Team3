// アイテムIDと所持数を管理する
using System.Collections.Generic;
using _Project.Scripts.Data.Item;

namespace InventoryData
{
    public class Inventory
    {
        private readonly Dictionary<int, int> _items = new();
        public IReadOnlyDictionary<int, int> Items => _items;

        /// <summary>
        /// itemIdを引数にアイテムの数を返すメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public int GetCount(int itemId)
        {
            return _items.TryGetValue(itemId, out var count) ? count : 0;
        }

        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個増やすメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="amount"></param>
        public void Add(int itemId, int amount = 1)
        {
            _items[itemId] = GetCount(itemId) + amount;
        }


        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個持っているか確認するメゾット
        /// </summary>
        public bool Has(int itemId, int amount = 1)
        {
            return GetCount(itemId) >= amount;
        }

        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個、減らすメゾット
        /// </summary>
        public bool Remove(int itemId, int amount = 1)
        {
            if (!Has(itemId, amount))
            {
                return false;
            }

            _items[itemId] -= amount;

            if (_items[itemId] <= 0)
            {
                _items.Remove(itemId);
            }

            return true;
        }

    }
}

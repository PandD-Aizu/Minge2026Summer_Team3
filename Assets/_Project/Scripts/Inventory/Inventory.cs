// アイテムIDと所持数を管理する
using System.Collections.Generic;
namespace _Project.Scripts.Inventory
{
    public class Inventory
    {
        private readonly Dictionary<int, int> items = new();

        /// <summary>
        /// itemIdを引数にアイテムの数を返すメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public int GetCount(int itemId)
        {
            return items.TryGetValue(itemId, out var count) ? count : 0;
        }
        
        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個増やすメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="amount"></param>
        public void Add(int itemId, int amount = 1)
        {
            items[itemId] += amount;
        }

        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個持っているか確認するメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public bool Has(int itemId, int amount = 1)
        {
            return GetCount(itemId) >= amount;
        }

        /// <summary>
        /// itemIdとamount(デフォルトは１)を引数にアイテム数をamount個、減らすメゾット
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public bool Remove(int itemId, int amount = 1)
        {
            if (!Has(itemId, amount))
            {
                return false;
            }
            
            items[itemId] -= amount;
            
            if (items[itemId] <= 0)
            {
                items.Remove(itemId);
            }

            return true;
        }

    }
}

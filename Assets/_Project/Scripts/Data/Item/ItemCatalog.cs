using UnityEngine;

namespace _Project.Scripts.Data.Item
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Game/Data/Item/Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] _items;

        public ItemDefinition[] Items => _items;

        /// <summary>
        /// ItmeIDからアイテムを返す
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns></returns>
        public ItemDefinition FindById(int itemId)
        {
            foreach (var item in _items)
            {
                if (item.ItemId == itemId)
                    return item;
            }

            return null;
        }

    }
}

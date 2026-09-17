using UnityEngine;

namespace _Project.Scripts.Data.Item
{
    [CreateAssetMenu(fileName = "ItemCatalog", menuName = "Game/Data/Item/Catalog")]
    public class ItemCatalog : ScriptableObject
    {
        [SerializeField] private ItemDefinition[] _items;

        public ItemDefinition[] Items => _items;
    }
}

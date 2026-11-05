using UnityEngine;

namespace _Project.Scripts.Data.Item
{
    public abstract class ItemDefinition : ScriptableObject
    {
        [SerializeField] private int itemId;
        [SerializeField] private string itemName;
        [TextArea] [SerializeField] private string description;

        [SerializeField, Min(0)] private int _purchasePrice = 100;
        public int PurchasePrice => _purchasePrice;

        public int ItemId => itemId;
        public string ItemName => itemName;
        public string Description => description;
        public abstract ItemType ItemType { get; }
        public abstract Sprite ItemImage { get; }
    }
}

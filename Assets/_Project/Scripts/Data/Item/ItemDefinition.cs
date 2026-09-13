using UnityEngine;

namespace _Project.Scripts.Data.Item
{
    [CreateAssetMenu(fileName = "ItemDefinition", menuName = "Game/Data/Item/Definition")]
    public class ItemDefinition : ScriptableObject
    {
        [SerializeField] private int itemId;
        [SerializeField] private string itemName;

        [TextArea] [SerializeField] private string description;

        [SerializeField] private ItemType type;
        [SerializeField] private Sprite itemImage;

        public int ItemId => itemId;
        public string ItemName => itemName;
        public string Description => description;
        public ItemType ItemType => type;
        public Sprite ItemImage => itemImage;
    }
}

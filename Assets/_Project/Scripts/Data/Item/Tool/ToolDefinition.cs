using _Project.Scripts.Data.Item;
using UnityEngine;

namespace _Project.Scripts.Data.Tool
{
    [CreateAssetMenu(
        fileName = "ToolDefinition",
        menuName = "Game/Data/Item/Tool Definition")]
    public class ToolDefinition : ItemDefinition
    {
        [SerializeField]
        private Sprite _toolImage;

        public override ItemType ItemType => ItemType.Tool;
        public override Sprite ItemImage => _toolImage;
    }
}

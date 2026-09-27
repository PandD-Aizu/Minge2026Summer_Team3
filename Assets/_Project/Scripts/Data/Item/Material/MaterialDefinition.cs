using _Project.Scripts.Data.Item;
using UnityEngine;

namespace _Project.Scripts.Data.Material
{
    [CreateAssetMenu(
        fileName = "MaterialDefinition",
        menuName = "Game/Data/Item/Material Definition")]
    public class MaterialDefinition : ItemDefinition
    {
        [SerializeField]
        private Sprite _materialImage;

        public override ItemType ItemType => ItemType.Material;
        public override Sprite ItemImage => _materialImage;
    }
}

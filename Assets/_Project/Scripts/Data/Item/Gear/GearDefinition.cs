using _Project.Scripts.Data.Item;
using UnityEngine;

namespace _Project.Scripts.Data.Gear
{
    [CreateAssetMenu(
        fileName = "GearDefinition",
        menuName = "Game/Data/Item/Gear Definition")]
    public class GearDefinition : ItemDefinition
    {
        [SerializeField]
        private Sprite _gearImage;

        public override ItemType ItemType => ItemType.Gear;
        public override Sprite ItemImage => _gearImage;
    }
}

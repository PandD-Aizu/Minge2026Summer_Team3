using _Project.Scripts.Data.Item;

namespace _Project.Scripts.Data
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "ExchangeRecipeDefinition",
        menuName = "Game/Data/Exchange/Definition")]
    public class ExchangeRecipeDefinition : ScriptableObject
    {
        [SerializeField]
        private ItemDefinition costItem;

        [SerializeField]
        private ItemDefinition rewardItem;

        public ItemDefinition CostItem => costItem;
        public ItemDefinition RewardItem => rewardItem;
    }
}
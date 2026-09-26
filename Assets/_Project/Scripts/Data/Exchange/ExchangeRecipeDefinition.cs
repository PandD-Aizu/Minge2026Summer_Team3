using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace _Project.Scripts.Data
{
    [CreateAssetMenu(
        fileName = "ExchangeRecipeDefinition",
        menuName = "Game/Data/Exchange/Definition")]
    public class ExchangeRecipeDefinition : ScriptableObject
    {
        [SerializeField]
        [FormerlySerializedAs("costItem")]
        private FishDefinition costFish;

        [SerializeField]
        [Min(1)]
        private int costAmount = 1;

        [SerializeField]
        private ItemDefinition rewardItem;

        [SerializeField]
        [Min(1)]
        private int rewardAmount = 1;

        public FishDefinition CostFish => costFish;
        public int CostAmount => costAmount;
        public ItemDefinition RewardItem => rewardItem;
        public int RewardAmount => rewardAmount;
    }
}

using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Item;

namespace _Project.Scripts.Data.Fish
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "FishDefinition",
        menuName = "Game/Data/Fish/Definition")]
    public class FishDefinition : ScriptableObject
    {
        [SerializeField]
        private int fishId;

        [SerializeField]
        private ItemDefinition item;

        [SerializeField]
        private MiniGameType gameType;

        [SerializeField]
        private float difficulty;

        [SerializeField]
        private FishingGimmickType gimmickType;

        public int FishId => fishId;
        public ItemDefinition Item => item;
        public MiniGameType GameType => gameType;
        public float Difficulty => difficulty;
        public FishingGimmickType GimmickType => gimmickType;
    }
}
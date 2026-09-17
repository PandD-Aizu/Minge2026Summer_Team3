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
        private int _fishId;

        [SerializeField]
        private ItemDefinition _item;

        [SerializeField]
        private MiniGameType _gameType;

        [SerializeField]
        private float _difficulty;

        [SerializeField]
        private FishingGimmickType _gimmickType;

        public int FishId => _fishId;
        public ItemDefinition Item => _item;
        public MiniGameType GameType => _gameType;
        public float Difficulty => _difficulty;
        public FishingGimmickType GimmickType => _gimmickType;
    }
}

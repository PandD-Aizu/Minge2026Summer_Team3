using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Item;
using _Project.Scripts.MiniGame;
using MiniGame;

namespace _Project.Scripts.Data.Fish
{
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "FishDefinition",
        menuName = "Game/Data/Item/Fish Definition")]
    public class FishDefinition : ItemDefinition
    {
        [SerializeField]
        private Sprite _fishImage;

        [SerializeField]
        private int _fishId;

        [SerializeField]
        private MiniGameSettings[] _miniGameSettings;

        [Header("表示用の難易度")]
        [SerializeField]
        private Difficulty _difficulty;

        [SerializeField]
        private FishingGimmickType _gimmickType;

        public override ItemType ItemType => ItemType.Fish;
        public override Sprite ItemImage => _fishImage;
        public int FishId => _fishId;
        public MiniGameSettings[] MiniGameSettings => _miniGameSettings;
        public Difficulty Difficulty => _difficulty;
        public FishingGimmickType GimmickType => _gimmickType;
    }
}

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

        [Header("魚の分類")]
        [SerializeField, Tooltip("チェックすると異形魚として扱う 未チェックなら普通の魚")]
        private bool _isAbnormal;

        [SerializeField]
        private MiniGameSettings[] _miniGameSettings;

        [Header("表示用の難易度")]
        [SerializeField]
        private Difficulty _difficulty;

        [SerializeField]
        private FishingGimmickType _gimmickType;

        public override ItemType ItemType => ItemType.Fish;
        public override Sprite ItemImage => _fishImage;
        /// <summary>異形魚ならtrue、普通の魚ならfalseを返す</summary>
        public bool IsAbnormal => _isAbnormal;
        public MiniGameSettings[] MiniGameSettings => _miniGameSettings;
        public Difficulty Difficulty => _difficulty;
        public FishingGimmickType GimmickType => _gimmickType;

        public MiniGameSettings SelectMiniGameRandom()
        {
            return _miniGameSettings[Random.Range(0, _miniGameSettings.Length)];
        }
    }
}

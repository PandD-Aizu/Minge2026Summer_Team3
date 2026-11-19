using System;
using UnityEngine;

namespace Upgrades
{
    public enum UpgradeEffect { MoveSpeed, FishingGain }

    /// <summary>購入段階ごとの価格と、元の能力に加算する割合を定義する</summary>
    [CreateAssetMenu(fileName = "Upgrade", menuName = "Game/Data/Upgrade")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public const int MaximumLevel = 5;

        [Serializable]
        public sealed class Stage
        {
            [SerializeField, Min(1)] private int _price;
            [SerializeField, Min(0f), Tooltip("0.05で元の能力の5%を追加する")]
            private float _effectAmount;
            public int Price => _price;
            public float EffectAmount => _effectAmount;
        }

        [SerializeField, Min(1)] private int _id;
        [SerializeField] private string _displayName;
        [SerializeField, TextArea] private string _description;
        [SerializeField] private UpgradeEffect _effect;
        [SerializeField] private Sprite _icon;
        [SerializeField, Range(1, MaximumLevel), Tooltip("購入できる最大回数、現在の2種類は5回")]
        private int _maxLevel = MaximumLevel;
        [SerializeField] private Stage[] _stages = new Stage[MaximumLevel];

        public int Id => _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public UpgradeEffect Effect => _effect;
        public Sprite Icon => _icon;
        public int MaxLevel => _maxLevel;

        /// <summary>次に購入する段階を取得する</summary>
        /// <param name="currentLevel">購入済み回数、0から4</param>
        /// <returns>次の段階、上限または設定不足ならnull</returns>
        /// <example>GetNextStage(0)で1回目の価格を調べる</example>
        public Stage GetNextStage(int currentLevel) => currentLevel >= 0 && currentLevel < MaxLevel
            && _stages != null && currentLevel < _stages.Length ? _stages[currentLevel] : null;

        /// <summary>購入済み段階の追加割合を合計する</summary>
        /// <param name="level">購入済み回数、0から5</param>
        /// <returns>元の能力に対する累積追加割合</returns>
        /// <example>倍率は1 + GetTotalEffect(level)で求める</example>
        public float GetTotalEffect(int level)
        {
            float total = 0f;
            for (int i = 0; i < Mathf.Clamp(level, 0, MaxLevel); i++)
                total += GetNextStage(i)?.EffectAmount ?? 0f;
            return total;
        }

        /// <summary>全5段階が有効な価格と有限の効果量を持つか確認する</summary>
        /// <returns>購入可能な定義ならtrue</returns>
        /// <example>購入前にIsValid()でSOの設定不足を拒否する</example>
        public bool IsValid()
        {
            if (_id <= 0 || string.IsNullOrWhiteSpace(_displayName) || !Enum.IsDefined(typeof(UpgradeEffect), _effect)
                || _maxLevel < 1 || _maxLevel > MaximumLevel || _stages == null || _stages.Length != MaxLevel) return false;
            double total = 0;
            foreach (var stage in _stages)
            {
                if (stage == null || stage.Price <= 0 || stage.EffectAmount < 0
                    || float.IsNaN(stage.EffectAmount) || float.IsInfinity(stage.EffectAmount)) return false;
                total += stage.EffectAmount;
            }
            return total <= float.MaxValue - 1d;
        }
    }
}

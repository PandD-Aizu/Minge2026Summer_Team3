using _Project.Scripts.Data.Fish;
using UnityEngine;

namespace Fishing
{
    /// <summary>
    /// 釣り場の魚候補と現在のFishDefinitionを保持する
    /// </summary>
    public class FishingTargetProvider : MonoBehaviour
    {
        [SerializeField] private FishDefinition[] _fishCandidates;

        private FishDefinition _fishDefinition;

        public FishDefinition FishDefinition => _fishDefinition;

        /// <summary>
        /// 魚候補から今回の魚を抽選する
        /// </summary>
        /// <example>FishingSpotで魚影を準備するときにSelectFish()を呼ぶ</example>
        public void SelectFish()
        {
            if (_fishCandidates == null || _fishCandidates.Length == 0)
            {
                _fishDefinition = null;
                Debug.LogError("FishingTargetProviderに魚候補を設定してください", this);
                return;
            }

            _fishDefinition = _fishCandidates[Random.Range(0, _fishCandidates.Length)];
        }
    }
}

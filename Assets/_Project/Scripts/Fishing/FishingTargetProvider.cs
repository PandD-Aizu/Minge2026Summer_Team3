using _Project.Scripts.Data.Fish;
using UnityEngine;
using _Project.Scripts.Core;
using R3;
using VContainer;

namespace Fishing
{
    /// <summary>
    /// 釣り場の魚候補と現在のFishDefinitionを保持する
    /// </summary>
    public class FishingTargetProvider : MonoBehaviour
    {
        [SerializeField] private FishDefinition[] _fishCandidates;

        private FishDefinition _fishDefinition;
        private GameProgress _gameProgress;
        private System.IDisposable _timeSubscription;

        public FishDefinition FishDefinition => _fishDefinition;

        /// <summary>時間帯を接続し、表示中の魚も時間帯変更時に抽選し直す 戻り値はなし</summary>
        /// <param name="gameProgress">現在の時間帯と変更通知を持つゲーム進行</param>
        /// <example>FishingSpotLifetimeScopeに登録したコンポーネントへVContainerが注入する</example>
        [Inject]
        public void Construct(GameProgress gameProgress)
        {
            _timeSubscription?.Dispose();
            _gameProgress = gameProgress;

            // 注入前に魚影が準備されていた場合も実際の時間帯に合わせる
            if (_fishDefinition != null) SelectFish();

            _timeSubscription = gameProgress.TimeOfDayChanged.Subscribe(_ =>
            {
                if (_fishDefinition != null) SelectFish();
            });
        }

        /// <summary>
        /// 夜は全種類、それ以外は普通の魚から今回の魚を抽選する 引数と戻り値はなし
        /// </summary>
        /// <example>FishingSpotで魚影を準備するときにSelectFish()を呼ぶ</example>
        public void SelectFish()
        {
            // 抽選できないときは以前の魚を残さない
            _fishDefinition = null;
            if (_fishCandidates == null || _fishCandidates.Length == 0)
            {
                _fishDefinition = null;
                Debug.LogError("FishingTargetProviderに魚候補を設定してください", this);
                return;
            }

            // 初期化前は昼として扱い、nullや昼の異形魚を候補から除く
            bool allowAbnormal = _gameProgress != null && _gameProgress.CurrentTimeOfDay == TimeOfDay.Night;
            int candidateCount = 0;
            foreach (var fish in _fishCandidates)
            {
                if (fish != null && (allowAbnormal || !fish.IsAbnormal)) candidateCount++;
            }

            if (candidateCount == 0)
            {
                Debug.LogError("現在の時間帯で釣れる魚候補がない 普通の魚を設定してほしい", this);
                return;
            }

            // 有効な候補だけを等確率で抽選する
            int selectedIndex = Random.Range(0, candidateCount);
            foreach (var fish in _fishCandidates)
            {
                if (fish == null || (!allowAbnormal && fish.IsAbnormal)) continue;
                if (selectedIndex-- != 0) continue;

                _fishDefinition = fish;
                return;
            }
        }

        /// <summary>時間帯通知の購読を解放する 引数と戻り値はなし</summary>
        /// <example>シーンのアンロード時にUnityが呼ぶ</example>
        private void OnDestroy() => _timeSubscription?.Dispose();
    }
}

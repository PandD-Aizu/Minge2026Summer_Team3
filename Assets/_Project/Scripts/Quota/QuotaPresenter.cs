using System;
using _Project.Scripts.Core;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.Data.Item;
using VContainer.Unity;
using Controller;
using R3;

namespace Quota
{
    public class QuotaPresenter : IInitializable, IDisposable
    {
        private TutorialController _tutorialController;
        private QuotaView _quotaView;
        private QuotaService _quotaService;
        private QuotaData[] _quotaData;
        private GameProgress _gameProgress;

        private QuotaData _currentQuota;

        private IDisposable _tutorialSubscription;
        private IDisposable _gameWeekSubscription;
        private IDisposable _fishCaughtSubscription;
        private IDisposable _quotaProgressSubscription;


        public QuotaPresenter(TutorialController tutorialController, QuotaView quotaView, QuotaService quotaService,
            QuotaData[]  quotaData,
            GameProgress gameProgress)
        {
            _tutorialController = tutorialController;
            _quotaView = quotaView;
            _quotaData = quotaData;
            _quotaService = quotaService;
            _gameProgress = gameProgress;
            _currentQuota = _quotaData[0];
        }

        /// <summary>チュートリアル段階と週の変更を購読し、現在の段階も反映する</summary>
        /// <example>VContainerのEntryPoint初期化時に呼ばれる</example>
        public void Initialize()
        {
            // チュートリアルの夜が来たことの通知を購読
            _tutorialSubscription = _tutorialController.OnStepChanged
                .Subscribe(OnTutorialStepChanged);

            // 週の変更を購読
            _gameWeekSubscription = _gameProgress.CurrentWeek.Subscribe(ShowOrdinalQuota);
            OnTutorialStepChanged(_tutorialController.CurrentStep);

            // ノルマの進捗の更新
            _quotaProgressSubscription = _quotaService.OnProgressChanged.Subscribe(AdvanceProgress);

            // 魚を釣ったとき
            _fishCaughtSubscription = _gameProgress.OnFishCaught.Subscribe(CheckQuotaFishCaught);
        }

        // 魚をつったことを記録する
        private void CheckQuotaFishCaught(int fishId)
        {
            _quotaService.CheckCanApplyQuotaProgress(fishId, 1);
        }


        /// <summary>夜釣り開始時に練習ノルマを表示し、完了時に本編表示へ切り替える</summary>
        /// <param name="step">変更後のチュートリアル段階</param>
        /// <example>OnStepChangedの購読から呼ぶ</example>
        private void OnTutorialStepChanged(TutorialStep step)
        {
            if (step == TutorialStep.GoNightFishing)
            {
                ShowTutorialNightQuota();
            }
            else if (step == TutorialStep.Completed)
            {
                ShowOrdinalQuota(_gameProgress.CurrentWeek.CurrentValue);

            }
        }

        /// <summary>チュートリアル用設定の目標と初期進捗を表示する</summary>
        /// <example>夜釣りへ出発する段階で呼ぶ</example>
        private void ShowTutorialNightQuota()
        {
            _currentQuota = _quotaData[0];
            _quotaService.ResetProgress();
            _quotaView.SetProgress(0,_currentQuota.QuotaAmount);
            _quotaView.Present($"{_currentQuota.QuotaExplain}", "明日の朝まで");
        }

        /// <summary>チュートリアル完了後、設定済みの週の目標と初期進捗を表示する</summary>
        /// <param name="currentWeek">本編ノルマ設定の配列番号に対応する現在の週</param>
        /// <example>CurrentWeekの購読から呼ぶ</example>
        private void ShowOrdinalQuota(int currentWeek)
        {
            // チュートリアル中はノルマを変更しない。
            if (!_tutorialController.IsCompleted) return;

            // 本編の未設定週では表示を閉じ、配列外アクセスを防ぐ
            if (currentWeek < 1 || currentWeek >= _quotaData.Length || _quotaData[currentWeek] == null)
            {
                _quotaView.Hide();
                return;
            }

            _currentQuota = _quotaData[currentWeek];
            _quotaService.ResetProgress();

            _quotaView.SetProgress(0, _currentQuota.QuotaAmount);
            _quotaView.Present($"{_currentQuota.QuotaExplain}", "1週間後の朝まで");
        }



        // 進捗を追加する
        private void AdvanceProgress(int progress)
        {
            _quotaView.SetProgress(progress, _currentQuota.QuotaAmount);
        }

        /// <summary>Rootの破棄時にチュートリアルと週の購読を解除する</summary>
        /// <example>VContainerがコンテナを破棄するときに呼ぶ</example>
        public void Dispose()
        {
            _tutorialSubscription?.Dispose();
            _gameWeekSubscription?.Dispose();
            _quotaProgressSubscription?.Dispose();
            _fishCaughtSubscription?.Dispose();
        }

    }
}

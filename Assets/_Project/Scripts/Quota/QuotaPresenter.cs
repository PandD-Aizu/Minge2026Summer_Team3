using System;
using _Project.Scripts.Core;
using VContainer.Unity;
using Controller;
using R3;
using UnityEngine;

namespace Quota
{
    public class QuotaPresenter : IInitializable, IDisposable
    {
        private readonly TutorialController _tutorialController;
        private readonly QuotaView _quotaView;
        private readonly QuotaService _quotaService;
        private readonly QuotaData[] _quotaData;
        private readonly GameProgress _gameProgress;

        private ReactiveProperty<QuotaData> _currentQuota = new(null);
        public ReactiveProperty<QuotaData> CurrentQuota => _currentQuota;

        private double _deadlineSeconds;
        public double DeadlineSeconds => _deadlineSeconds;
        private bool _isJudged;
        private bool _isGameOver;

        private IDisposable _tutorialSubscription;
        private IDisposable _gameTimeSubscription;
        private IDisposable _fishCaughtSubscription;
        private IDisposable _quotaProgressSubscription;



        /// <summary>ノルマの表示と進捗と朝の判定に使う依存を受け取る</summary>
        /// <param name="tutorialController">初回進行の通知元</param>
        /// <param name="quotaView">ノルマの表示先</param>
        /// <param name="quotaService">釣果による進捗の保持先</param>
        /// <param name="quotaData">0番が練習用、1番以降が本編の週番号に対応する設定</param>
        /// <param name="gameProgress">累積時間と釣果の通知元</param>
        /// <example>RootのEntryPointとして生成する</example>
        public QuotaPresenter(TutorialController tutorialController, QuotaView quotaView, QuotaService quotaService,
            QuotaData[]  quotaData,
            GameProgress gameProgress)
        {
            _tutorialController = tutorialController;
            _quotaView = quotaView;
            _quotaData = quotaData;
            _quotaService = quotaService;
            _gameProgress = gameProgress;
        }

        /// <summary>チュートリアル段階と時計と釣果を購読し、朝の期限を監視する</summary>
        /// <example>VContainerのEntryPoint初期化時に呼ばれる</example>
        public void Initialize()
        {
            // チュートリアルの夜が来たことの通知を購読
            _tutorialSubscription = _tutorialController.OnStepChanged
                .Subscribe(OnTutorialStepChanged);

            // 0時の週変更では進捗を消さず、朝7時の期限まで保持する
            _gameTimeSubscription = _gameProgress.GameSeconds.Subscribe(CheckMorningDeadline);
            OnTutorialStepChanged(_tutorialController.CurrentStep);

            // ノルマの進捗の更新
            _quotaProgressSubscription = _quotaService.OnProgressChanged.Subscribe(AdvanceProgress);

            // 魚を釣ったとき
            _fishCaughtSubscription = _gameProgress.OnFishCaught.Subscribe(CheckQuotaFishCaught);
        }

        /// <summary>発行済みで判定前のノルマに釣果を加算する</summary>
        /// <param name="fishId">釣り上げた魚のItemId</param>
        /// <example>OnFishCaughtの購読から呼ぶ</example>
        private void CheckQuotaFishCaught(int fishId)
        {
            if (_currentQuota.Value == null || _isJudged) return;

            _quotaService.CheckCanApplyQuotaProgress(fishId, 1);
        }


        private void CheckMorningDeadline(float seconds)
        {
            if (_currentQuota.Value == null || _isJudged || seconds < _deadlineSeconds) return;

            // 通知が続いても判定を繰り返さず、この時点の進捗で結果を固定する
            _isJudged = true;
            _isGameOver = _quotaService.Progress < _currentQuota.Value.QuotaAmount;
            Debug.Log(_isGameOver
                ? $"[Quota] GameOver: ノルマ未達成 ({_quotaService.Progress}/{_currentQuota.Value.QuotaAmount})"
                : $"[Quota] 継続可能: ノルマ達成 ({_quotaService.Progress}/{_currentQuota.Value.QuotaAmount})");

            if (_isGameOver)
            {
                // GameOverの処理を呼ぶ
            }
            else
            {
                ShowOrdinalQuota(_gameProgress.CurrentWeek.CurrentValue);
            }
        }



        private void OnTutorialStepChanged(TutorialStep step)
        {
            if (step == TutorialStep.GoNightFishing)
            {
                ShowTutorialNightQuota();
            }
            else if (step == TutorialStep.Completed && !_isGameOver)
            {
                ShowOrdinalQuota(_gameProgress.CurrentWeek.CurrentValue);
            }
        }

        /// <summary>チュートリアル用設定の目標と初期進捗を表示する</summary>
        /// <example>夜釣りへ出発する段階で呼ぶ</example>
        private void ShowTutorialNightQuota()
        {
            // 夜に発行した練習ノルマの期限は、翌暦日の朝7時
           IssueQuota(0, 1, "明日の朝まで");
        }



        private void ShowOrdinalQuota(int currentWeek)
        {
            // チュートリアル中はノルマを変更しない
            if (!_tutorialController.IsCompleted) return;

            // 本編の未設定週では表示を閉じ、配列外アクセスを防ぐ
            if (currentWeek < 1 || currentWeek >= _quotaData.Length || _quotaData[currentWeek] == null)
            {
                _currentQuota.Value = null;
                _quotaView.Hide();
                return;
            }

            IssueQuota(currentWeek, 7, "一週間後の朝まで");
        }

        private void IssueQuota(int week, int day, string explanation)
        {
            if(week < 0F || week >= _quotaData.Length|| _quotaData[week] == null) return;
            _currentQuota.Value = _quotaData[week];
            SetMorningDeadline(day); // 何日後が期日か
            _quotaService.ResetProgress();
            _quotaView.SetProgress(0,_currentQuota.Value.QuotaAmount);
            _quotaView.Present($"{_currentQuota.Value.QuotaExplain}", $"{explanation}");
            _isJudged = false;
        }

        /// <summary>現在の暦日を基準に、指定日数後の朝7時を期限に設定する</summary>
        /// <param name="daysLater">練習ノルマは1、本編の週ノルマは7</param>
        /// <example>本編開始の朝にSetMorningDeadline(7)を呼ぶ</example>
        private void SetMorningDeadline(int daysLater)
        {
            const double secondsPerDay = 24d * 3600d;
            double currentDay = Math.Floor(_gameProgress.GameSeconds.CurrentValue / secondsPerDay);
            _deadlineSeconds = (currentDay + daysLater) * secondsPerDay + 7d * 3600d;
            _isJudged = false;
        }



        /// <summary>発行済みノルマの進捗を表示へ反映する</summary>
        /// <param name="progress">現在の達成数</param>
        /// <example>OnProgressChangedの購読から呼ぶ</example>
        private void AdvanceProgress(int progress)
        {
            if (_currentQuota.Value == null) return;

            _quotaView.SetProgress(progress, _currentQuota.Value.QuotaAmount);
        }



        /// <summary>Rootの破棄時にチュートリアルと時計と進捗と釣果の購読を解除する</summary>
        /// <example>VContainerがコンテナを破棄するときに呼ぶ</example>
        public void Dispose()
        {
            _tutorialSubscription?.Dispose();
            _gameTimeSubscription?.Dispose();
            _quotaProgressSubscription?.Dispose();
            _fishCaughtSubscription?.Dispose();
        }

    }
}

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
        private TutorialController _tutorialController;
        private QuotaView _quotaView;
        private QuotaService _quotaService;
        private QuotaData[] _quotaData;
        private GameProgress _gameProgress;

        private QuotaData _currentQuota;
        private double _deadlineSeconds;
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
            if (_currentQuota == null || _isJudged) return;

            _quotaService.CheckCanApplyQuotaProgress(fishId, 1);
        }

        /// <summary>朝7時の期限に到達したノルマを一度だけ判定し、結果をログに出す</summary>
        /// <param name="seconds">更新後の累積ゲーム内秒数</param>
        /// <example>GameSecondsの購読から呼び、朝をまたぐ早送りでも判定する</example>
        private void CheckMorningDeadline(float seconds)
        {
            if (_currentQuota == null || _isJudged || seconds < _deadlineSeconds) return;

            // 通知が続いても判定を繰り返さず、この時点の進捗で結果を固定する
            _isJudged = true;
            _isGameOver = _quotaService.Progress < _currentQuota.QuotaAmount;
            Debug.Log(_isGameOver
                ? $"[Quota] GameOver: ノルマ未達成 ({_quotaService.Progress}/{_currentQuota.QuotaAmount})"
                : $"[Quota] 継続可能: ノルマ達成 ({_quotaService.Progress}/{_currentQuota.QuotaAmount})");
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
            else if (step == TutorialStep.Completed && !_isGameOver)
            {
                ShowOrdinalQuota(_gameProgress.CurrentWeek.CurrentValue);

            }
        }

        /// <summary>チュートリアル用設定の目標と初期進捗を表示する</summary>
        /// <example>夜釣りへ出発する段階で呼ぶ</example>
        private void ShowTutorialNightQuota()
        {
            _currentQuota = _quotaData[0];
            if (_currentQuota == null) return;

            // 夜に発行した練習ノルマの期限は、翌暦日の朝7時
            SetMorningDeadline(1);
            _quotaService.ResetProgress();
            _quotaView.SetProgress(0,_currentQuota.QuotaAmount);
            _quotaView.Present($"{_currentQuota.QuotaExplain}", "明日の朝まで");
        }

        /// <summary>チュートリアル完了後、設定済みの週の目標と初期進捗を表示する</summary>
        /// <param name="currentWeek">本編ノルマ設定の配列番号に対応する現在の週</param>
        /// <example>チュートリアル完了時に呼ぶ 次週の発行は期限判定後の処理として別途実装する</example>
        private void ShowOrdinalQuota(int currentWeek)
        {
            // チュートリアル中はノルマを変更しない
            if (!_tutorialController.IsCompleted) return;

            // 本編の未設定週では表示を閉じ、配列外アクセスを防ぐ
            if (currentWeek < 1 || currentWeek >= _quotaData.Length || _quotaData[currentWeek] == null)
            {
                _currentQuota = null;
                _quotaView.Hide();
                return;
            }

            _currentQuota = _quotaData[currentWeek];
            SetMorningDeadline(7);
            _quotaService.ResetProgress();

            _quotaView.SetProgress(0, _currentQuota.QuotaAmount);
            _quotaView.Present($"{_currentQuota.QuotaExplain}", "1週間後の朝まで");
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
            if (_currentQuota == null) return;

            _quotaView.SetProgress(progress, _currentQuota.QuotaAmount);
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

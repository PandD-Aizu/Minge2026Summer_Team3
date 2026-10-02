using System;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Controller;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace MiniGame
{
    public class MiniGameFlowPresenter : IDisposable
    {
        private readonly MiniGameResultPresenter _resultPresenter;
        private readonly TutorialController _tutorialController;
        private readonly IMiniGameController[] _controllers;

        private IMiniGameController _currentController;
        private IDisposable _currentCompletedSubscription;
        private IDisposable _cancelSubscription;
        private FishDefinition _currentFishDefinition; // 今後使う

        public MiniGameFlowPresenter(
            RotationMiniGameController rotationMiniGameController,
            MiniGameResultPresenter resultPresenter,
            TutorialController tutorialController)
        {
            _controllers = new IMiniGameController[] { rotationMiniGameController };
            _resultPresenter = resultPresenter;
            _tutorialController = tutorialController;
        }


        /// <summary>
        /// 魚に設定されたミニゲームから抽選して開始する
        /// </summary>
        public void StartMiniGame(FishDefinition fishDefinition)
        {
            if (fishDefinition == null)
            {
                Debug.LogError("FishDefinitionがnullなのでミニゲームを開始できません");
                return;
            }

            _currentFishDefinition = fishDefinition;
            StartMiniGame(fishDefinition.SelectMiniGameRandom());
        }

        /// <summary>
        /// 指定されたミニゲーム設定に対応するControllerを選んで開始する
        /// </summary>
        public void StartMiniGame(MiniGameSettings settings)
        {
            if (settings == null)
            {
                Debug.LogError("MiniGameSettingsがnullなのでミニゲームを開始できません");
                return;
            }

            _currentCompletedSubscription?.Dispose();
            _currentController = FindController(settings.GameType);

            if (_currentController == null)
            {
                Debug.LogError($"{settings.GameType} のControllerが登録されていません");
                return;
            }

            _currentCompletedSubscription = _currentController.Completed.Subscribe(OnMiniGameCompleted);
            _cancelSubscription = _currentController.Canceled.Subscribe(_ => OnMiniGameCancelled());
            _currentController.StartGame(settings);
        }

        private IMiniGameController FindController(MiniGameType gameType)
        {
            foreach (IMiniGameController controller in _controllers)
            {
                if (controller.GameType == gameType)
                    return controller;
            }

            return null;
        }

        private void OnMiniGameCompleted(MiniGameResult result)
        {
            RunResultFlowAsync(result).Forget();
        }

        private void OnMiniGameCancelled()
        {
            ClearCurrentMiniGame();
        }

        /// <summary>結果表示とミニゲームを終了してから釣果をチュートリアルへ通知する</summary>
        /// <param name="result">ミニゲームの成績</param>
        /// <example>ミニゲーム成功通知を受けたときに呼ぶ</example>
        private async UniTask RunResultFlowAsync(MiniGameResult result)
        {
            var fishAdded = await _resultPresenter.PlayResultAsync(result, _currentFishDefinition);

            _currentController?.EndGame();
            ClearCurrentMiniGame();

            // 結果UIとミニゲームを片付けてから進行変更を通知する
            if (fishAdded) _tutorialController.NotifyFishCaught();
        }

        private void ClearCurrentMiniGame()
        {
            _currentCompletedSubscription?.Dispose();
            _cancelSubscription?.Dispose();
            _currentCompletedSubscription = null;
            _currentController = null;
            _currentFishDefinition = null;
        }

        public void Dispose()
        {
            ClearCurrentMiniGame();
        }
    }
}

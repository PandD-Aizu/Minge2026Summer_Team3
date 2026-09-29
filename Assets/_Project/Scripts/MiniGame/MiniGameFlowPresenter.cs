using System;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace MiniGame
{
    public class MiniGameFlowPresenter : IDisposable
    {
        private readonly MiniGameResultPresenter _resultPresenter;
        private readonly IMiniGameController[] _controllers;

        private IMiniGameController _currentController;
        private IDisposable _currentCompletedSubscription;
        private IDisposable _cancelSubscription;
        private FishDefinition _currentFishDefinition; // 今後使う

        public MiniGameFlowPresenter(
            RotationMiniGameController rotationMiniGameController,
            MiniGameResultPresenter resultPresenter)
        {
            _controllers = new IMiniGameController[] { rotationMiniGameController };
            _resultPresenter = resultPresenter;
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

        private async UniTask RunResultFlowAsync(MiniGameResult result)
        {
            await _resultPresenter.PlayResultAsync(result, _currentFishDefinition);

            _currentController?.EndGame();
            ClearCurrentMiniGame();
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

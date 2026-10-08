using System;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Controller;
using Cysharp.Threading.Tasks;
using R3;
using FMODServices;
using FMODSettings;
using UnityEngine;

namespace MiniGame
{
    public class MiniGameFlowPresenter : IDisposable
    {
        private readonly MiniGameResultPresenter _resultPresenter;
        private readonly TutorialController _tutorialController;
        private readonly IDisposable _rodSwingSubscription;
        private readonly IMiniGameController[] _controllers;

        private IMiniGameController _currentController;
        private IDisposable _currentCompletedSubscription;
        private IDisposable _cancelSubscription;
        private FishDefinition _currentFishDefinition;

        /// <summary>釣りの開始、結果、進行通知と効果音の依存関係を受け取る</summary>
        /// <param name="rotationMiniGameController">回転ミニゲームの実行元</param>
        /// <param name="resultPresenter">釣果の表示</param>
        /// <param name="tutorialController">釣果に応じた進行通知先</param>
        /// <param name="se">竿を振る音を再生するサービス</param>
        /// <param name="inputReader">釣りシーンの操作入力と操作可否を取得する入力元</param>
        /// <example>VContainerの登録から生成する</example>
        public MiniGameFlowPresenter(
            RotationMiniGameController rotationMiniGameController,
            MiniGameResultPresenter resultPresenter,
            TutorialController tutorialController, FMODSEService se, PlayerInputReader inputReader)
        {
            _controllers = new IMiniGameController[] { rotationMiniGameController };
            _resultPresenter = resultPresenter;
            _tutorialController = tutorialController;

            // 魚の有無に関係なく、操作可能な状態でJキーを押すたびに竿を振る音を鳴らす
            _rodSwingSubscription = inputReader.OnInteractPressed
                .Where(_ => inputReader.CanStartGameplayAction)
                .Subscribe(_ => se.PlayOneShot(FMODEventPath.SE_FISH_ROD_SWING.Reference));
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
            bool fishAdded;
            try
            {
                fishAdded = await _resultPresenter.PlayResultAsync(result, _currentFishDefinition);
            }
            catch (OperationCanceledException)
            {
                // シーン終了時にはチュートリアルを進めず、演出だけを片付ける
                return;
            }
            finally
            {
                _currentController?.EndGame();
                ClearCurrentMiniGame();
            }

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

        /// <summary>竿を振る入力の購読と進行中のミニゲームの購読を解除する</summary>
        /// <example>釣りシーン終了時にVContainerから呼ぶ</example>
        public void Dispose()
        {
            _rodSwingSubscription.Dispose();
            ClearCurrentMiniGame();
        }
    }
}

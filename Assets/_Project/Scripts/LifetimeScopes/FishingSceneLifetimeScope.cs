using _Project.Scripts.Enemy;
using _Project.Scripts.View;
using Controller;
using Dialogue;
using Enemy;
using MiniGame;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class FishingSceneLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerInputReader _playerInputReader;
        [SerializeField] private RotationMiniGameView _rotationMiniGameView;
        [SerializeField] private MiniGameResultView _miniGameResultView;
        [SerializeField] private RotationMiniGameSettings _rotationMiniGameSettings;
        [SerializeField] private DialogueUIView _dialogueView;
        [SerializeField] private FishingMonologuePresenter _fishingMonologuePresenter;
        [SerializeField] private TimeOfDayView _timeOfDayView;

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上のViewを登録
            builder.RegisterComponent(_rotationMiniGameView);
            builder.RegisterComponent(_miniGameResultView);
            builder.RegisterComponent(_timeOfDayView);
            builder.RegisterDialogue(_dialogueView);
            builder.Register<GameplayDialogueController>(Lifetime.Scoped);

            // 設定用のScriptableObjectを登録
            builder.RegisterInstance(_rotationMiniGameSettings);

            // ミニゲーム単体のControllerを登録
            builder.Register<RotationMiniGameController>(Lifetime.Scoped)
                .AsSelf()
                .As<IMiniGameController>();

            // ミニゲーム全体の進行と結果表示を登録
            builder.Register<MiniGameResultPresenter>(Lifetime.Scoped);
            builder.Register<MiniGameFlowPresenter>(Lifetime.Scoped);

            // テスト用
            //builder.RegisterEntryPoint<RotationMiniGameTestEntryPoint>();

            // 入力の受付
            builder.RegisterComponent(_playerInputReader);

            // 敵の判断は通常のC#クラスで行い、Scene上の表示、移動、判定を渡す
            builder.RegisterComponentInHierarchy<PlayerMovement>().As<IPlayerPosition>();
            builder.RegisterComponentInHierarchy<VisionEnemyView>();
            builder.RegisterComponentInHierarchy<VisionEnemyDetectSensor>();
            builder.RegisterComponentInHierarchy<VisionEnemyDeathSensor>();
            builder.RegisterEntryPoint<VisionEnemyController>().AsSelf();
            builder.RegisterComponentInHierarchy<VisionEnemyNavigator>();

            // GameOverの演出関連
            builder.RegisterComponentInHierarchy<CameraShaker>();
            builder.RegisterComponentInHierarchy<GameOverCameraView>();
            builder.RegisterEntryPoint<GameOverPresenter>();


            // 釣り場への到着をチュートリアルへ通知する
            builder.RegisterEntryPoint<TutorialFishingStageEntryPoint>();
            builder.RegisterEntryPoint<TimeOfDayPresenter>();

            // シーンに置いた独り言Presenterへ会話と進行を注入する
            builder.RegisterBuildCallback(container => container.Inject(_fishingMonologuePresenter));
        }
    }
}

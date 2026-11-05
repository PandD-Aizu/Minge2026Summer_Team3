using _Project.Scripts.Enemy;
using _Project.Scripts.Fishing;
using _Project.Scripts.View;
using Controller;
using Dialogue;
using Enemy;
using MiniGame;
using PauseMenu;
using Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class FishingSceneLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerInputReader _playerInputReader;
        [SerializeField] private RotationMiniGameView _rotationMiniGameView;
        [SerializeField] private GaugeMiniGameView _gaugeMiniGameView;
        [SerializeField] private FishingBiteSettings _fishingBiteSettings;
        [SerializeField] private MiniGameResultView _miniGameResultView;
        [SerializeField] private RotationMiniGameSettings _rotationMiniGameSettings;
        [SerializeField] private DialogueUIView _dialogueView;
        [SerializeField] private FishingMonologuePresenter _fishingMonologuePresenter;
        [SerializeField] private TimeOfDayView _timeOfDayView;
        [SerializeField] private TimeOfDayLightingSettings _timeOfDayLighting = new();
        [SerializeField] private PauseMenuView _pauseMenuView;
        [SerializeField] private Transform _firstFishApproach;
        [SerializeField, Tooltip("初回釣果後に案内するCampStageへのワープ地点")]
        private Transform _firstCatchReturnTarget;
        [SerializeField] private View.NavigationArrowView _navigationView;

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上のViewを登録
            _timeOfDayView.ConfigureLighting(_timeOfDayLighting);
            builder.RegisterComponent(_rotationMiniGameView);
            builder.RegisterComponent(_gaugeMiniGameView);
            builder.RegisterInstance(_fishingBiteSettings);
            builder.RegisterComponent(_miniGameResultView);
            builder.RegisterComponent(_timeOfDayView);
            builder.RegisterDialogue(_dialogueView);
            builder.Register<GameplayDialogueController>(Lifetime.Scoped);

            // 設定用のScriptableObjectを登録
            builder.RegisterInstance(_rotationMiniGameSettings);

            // 子の釣り場Scopeから解決しても、シーン内で同じ実行元と進行管理を共有する
            builder.Register<RotationMiniGameController>(Lifetime.Singleton)
                .AsSelf()
                .As<IMiniGameController>();
            builder.Register<GaugeMiniGameController>(Lifetime.Singleton).AsSelf().As<IMiniGameController>();
            builder.Register<MiniGameRegistry>(Lifetime.Singleton);
            builder.Register<MiniGameSelector>(Lifetime.Singleton);
            builder.Register<FishingBiteController>(Lifetime.Singleton);
            builder.Register<FishingFlowPresenter>(Lifetime.Singleton);

            // ミニゲーム全体の進行と結果表示を登録
            builder.Register<MiniGameResultPresenter>(Lifetime.Singleton);
            builder.Register<MiniGameFlowPresenter>(Lifetime.Singleton);

            // テスト用
            //builder.RegisterEntryPoint<RotationMiniGameTestEntryPoint>();

            // 入力の受付
            builder.RegisterComponent(_playerInputReader);

            // 初回の魚へ、キャンプと同じ縦横の経路と光の粒で案内する
            if (_firstFishApproach != null && _navigationView != null)
            {
                builder.RegisterComponent(_navigationView);
                Vector3? returnPosition = _firstCatchReturnTarget != null ? _firstCatchReturnTarget.position : null;
                builder.RegisterInstance(new NavigationArrowServices.NavigationArrowService(
                    _playerInputReader.transform, _firstFishApproach.position, guideFirstFish: true,
                    firstCatchReturnPosition: returnPosition));
                builder.RegisterEntryPoint<NavigationArrowPresenter>();
            }

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

            if (_pauseMenuView != null)
            {
                builder.RegisterComponent(_pauseMenuView);
                builder.RegisterEntryPoint<PauseMenuPresenter>();
            }

            // シーンに置いた独り言Presenterへ会話と進行を注入する
            builder.RegisterBuildCallback(container => container.Inject(_fishingMonologuePresenter));
        }
    }
}

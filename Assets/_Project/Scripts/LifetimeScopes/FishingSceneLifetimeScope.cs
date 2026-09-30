using _Project.Scripts.View;
using Controller;
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

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上のViewを登録
            builder.RegisterComponent(_rotationMiniGameView);
            builder.RegisterComponent(_miniGameResultView);

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

            // 釣り場への到着をチュートリアルへ通知する
            builder.RegisterEntryPoint<TutorialFishingStageEntryPoint>();
        }
    }
}

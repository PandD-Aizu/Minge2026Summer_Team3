using UnityEngine;
using VContainer;
using VContainer.Unity;
using MiniGame;

namespace LifetimeScopes
{
    public class FishingSceneLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerInputReader _playerInputReader;

        [SerializeField] private RotationMiniGameView _rotationMiniGameView;

        [SerializeField] private RotationMiniGameSettings _rotationMiniGameSettings;

        protected override void Configure(IContainerBuilder builder)
        {
            // シーン上のViewを登録
            builder.RegisterComponent(_rotationMiniGameView);

            // 設定用のScriptableObjectを登録
            builder.RegisterInstance(_rotationMiniGameSettings);

            // ミニゲームの進行・入力・判定を担当
            builder.Register<RotationMiniGameController>(Lifetime.Scoped);

            // テスト用
            //builder.RegisterEntryPoint<RotationMiniGameTestEntryPoint>();


            // 入力の受付
            builder.RegisterComponent(_playerInputReader);
        }
    }
}

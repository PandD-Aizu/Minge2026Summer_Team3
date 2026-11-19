using FMODServices;
using FMODSettings;
using _Project.Scripts.Core;
using _Project.Scripts.Data;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Controller;
using GameOver;
using Input;
using SaveSettings;
using SceneLoadServices;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private InventoryView _inventoryPrefab;
        private AsyncOperationHandle<EconomyAssets> _economyHandle;
        [SerializeField] private GameOverView _gameOverPrefab;

        /// <summary>音声・入力・インベントリなどアプリ全体で共有する依存関係を登録する</summary>
        /// <param name="builder">Rootコンテナの登録先</param>
        /// <example>VContainerのRoot Prefabから自動実行される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 音声インスタンスの所有権とセーブアクセスをRootに集約する
            builder.RegisterEntryPoint<FMODBGMService>().AsSelf();
            builder.Register<FMODSEService>(Lifetime.Singleton);
            builder.Register<FMODVCAService>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);

            // シーンのロード関係のサービス
            builder.Register<SceneLoadService>(Lifetime.Singleton);

            // チュートリアル進行
            builder.Register<GameProgress>(Lifetime.Singleton);
            builder.Register<ClockController>(Lifetime.Singleton);
            builder.Register<TutorialController>(Lifetime.Singleton);

            // ゲーム内時計をすすめる
            builder.RegisterEntryPoint<GameClock>();

            // 設定画面がなくても保存済みの音量を適用する
            builder.RegisterEntryPoint<FMODAudioInitializer>().AsSelf();

            // 入力の設定
            builder.Register<PlayerInputAction>(Lifetime.Singleton);

            // 入力の切り替え
            builder.Register<InputModeService>(Lifetime.Singleton);
            builder.Register<MenuInputService>(Lifetime.Singleton);

            // 専用Prefabと入力を接続し、全ゲームシーンで同じインベントリを使う
            builder.RegisterComponentInNewPrefab(_inventoryPrefab, Lifetime.Singleton).UnderTransform(transform);
            builder.RegisterFactory<InventoryView>(resolver => () => resolver.Resolve<InventoryView>(), Lifetime.Singleton);
            builder.RegisterEntryPoint<_Project.Scripts.Inventory.InventoryPresenter>();
            builder.Register<InventoryData.StagePlacements>(Lifetime.Singleton);
            builder.Register<InventoryData.ConsumableEffects>(Lifetime.Singleton);
            builder.Register<InventoryData.Inventory>(Lifetime.Singleton);
            builder.Register<Upgrades.UpgradeService>(Lifetime.Singleton);
            builder.Register<ExchangePoint.Wallet>(Lifetime.Singleton);
            builder.Register<ExchangePoint.ExchangePointService>(Lifetime.Singleton);

            // Rootの初期化中にローカルの定義を揃え、最初のフレームから保存済みの強化を適用する
            _economyHandle = Addressables.LoadAssetAsync<EconomyAssets>(EconomyAssets.Address);
            var economy = _economyHandle.WaitForCompletion();
            if (economy == null) throw new System.InvalidOperationException("取引データの読み込みに失敗", _economyHandle.OperationException);
            builder.RegisterInstance(economy);
            builder.RegisterInstance(economy.Items);
            builder.RegisterComponentInNewPrefab(_gameOverPrefab, Lifetime.Singleton).UnderTransform(transform);
        }

        /// <summary>共有サービスの終了後に経済アセットを解放する</summary>
        /// <example>Rootの破棄時にUnityが呼ぶ</example>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_economyHandle.IsValid()) Addressables.Release(_economyHandle);
        }
    }
}

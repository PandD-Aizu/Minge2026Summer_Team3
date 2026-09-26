using FMODServices;
using FMODSettings;
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

        /// <summary>音声・入力・インベントリなどアプリ全体で共有する依存関係を登録する</summary>
        /// <param name="builder">Rootコンテナの登録先</param>
        /// <example>VContainerのRoot Prefabから自動実行される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 音声インスタンスの所有権とセーブアクセスをRootに集約する
            builder.Register<FMODBGMService>(Lifetime.Singleton);
            builder.Register<FMODSEService>(Lifetime.Singleton);
            builder.Register<FMODVCAService>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);

            // シーンのロード関係のサービス
            builder.Register<SceneLoadService>(Lifetime.Singleton);

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
        }
    }
}

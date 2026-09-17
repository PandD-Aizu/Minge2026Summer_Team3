using FMODServices;
using FMODSettings;
using SaveSettings;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class GameLifetimeScope : LifetimeScope
    {
        /// <summary>アプリ全体で共有する音声サービスを登録する</summary>
        /// <param name="builder">Rootコンテナの登録先</param>
        /// <example>VContainerのRoot Prefabから自動実行される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 音声インスタンスの所有権とセーブアクセスをRootに集約する
            builder.Register<FMODBGMService>(Lifetime.Singleton);
            builder.Register<FMODSEService>(Lifetime.Singleton);
            builder.Register<FMODVCAService>(Lifetime.Singleton);
            builder.Register<SaveService>(Lifetime.Singleton);

            // 設定画面がなくても保存済みの音量を適用する
            builder.RegisterEntryPoint<FMODAudioInitializer>().AsSelf();
        }
    }
}

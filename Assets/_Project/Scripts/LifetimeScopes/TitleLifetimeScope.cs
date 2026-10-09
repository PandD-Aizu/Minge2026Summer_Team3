using Presentation;
using FMODServices;
using FMODSettings;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class TitleLifetimeScope : LifetimeScope
    {
        /// <summary>タイトルの表示とシーン遷移を登録する</summary>
        /// <param name="builder">タイトルシーンのコンテナ</param>
        /// <example>Titleシーンの起動時にVContainerから呼ばれる</example>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<TitleUIPresenter>();
            builder.RegisterComponentInHierarchy<TitleUIView>();

            // ステージと同じ昼BGMをRootで再生し、イントロへの遷移後も継続する
            builder.RegisterBuildCallback(resolver => resolver.Resolve<FMODBGMService>()
                .PlayBGM(FMODEventPath.BGM_BENEATH_THE_WAVES.Reference));
        }
    }
}

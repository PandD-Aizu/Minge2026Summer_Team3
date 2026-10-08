using Presentation;
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
        }
    }
}

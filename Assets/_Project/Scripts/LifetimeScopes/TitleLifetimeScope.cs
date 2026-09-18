using Presentation;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class TitleLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // TitleUIのエントリーポイント
            builder.RegisterEntryPoint<TitleUIPresenter>();

            // TitleUIView
            builder.RegisterComponentInHierarchy<TitleUIView>();
        }
    }
}

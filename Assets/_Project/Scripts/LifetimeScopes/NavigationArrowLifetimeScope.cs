using NavigationArrowServices;
using Presentation;
using VContainer;
using VContainer.Unity;
using View;

namespace LifetimeScopes
{
    public class NavigationArrowLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // NavigationArrow関連のサービス
            builder.Register<NavigationArrowService>(Lifetime.Singleton);

            // NavigationArrow関連のPresenter
            builder.RegisterEntryPoint<NavigationArrowPresenter>();

            // NavigationArrow関連のView
            builder.RegisterComponentInHierarchy<NavigationArrowView>();
        }
    }
}

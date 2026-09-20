using _Project.Scripts.InteractableObject;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class InteractableObjectLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<InteractableObjectPresenter>();
        }
    }
}

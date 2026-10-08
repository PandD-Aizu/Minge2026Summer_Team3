using _Project.Scripts.Fishing;
using Fishing;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class FishingSpotLifetimeScope : LifetimeScope
    {
        //[SerializeField] private FishingSpot _fishingSpot;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(GetComponent<FishingSpot>());
            builder.RegisterComponent(GetComponent<FishingTargetProvider>());
            builder.RegisterEntryPoint<FishingInteractPresenter>().AsSelf();
        }
    }
}

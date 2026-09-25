using _Project.Scripts.InteractableObject;
using ExchangePoint;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class ExchangeLifetimeScope : LifetimeScope
    {
        [Tooltip("無人集荷場のInteractableConnectorを指定する")]
        [SerializeField] private InteractableConnector _exchangeInteractableConnector;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<ExchangePointService>(Lifetime.Singleton);

            builder.RegisterEntryPoint<ExchangePointPresenter>();

            builder.RegisterComponentInHierarchy<ExchangePointView>();
            builder.RegisterComponentInHierarchy<PlayerInputReader>();
            builder.RegisterComponent(_exchangeInteractableConnector);
        }
    }
}

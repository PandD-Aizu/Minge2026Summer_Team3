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

        /// <summary>集荷場のViewと入力を接続し、Presenterの寿命をこのScopeに合わせる</summary>
        /// <param name="builder">依存関係の登録先</param>
        /// <example>シーン内の集荷場に付けてVContainerから初期化する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 初期購読、非同期ロード、フレーム更新、破棄をVContainerへ委ねる
            builder.RegisterEntryPoint<ExchangePointPresenter>(Lifetime.Scoped);

            builder.RegisterComponentInHierarchy<ExchangePointView>();
            builder.RegisterComponentInHierarchy<PlayerInputReader>();
            builder.RegisterComponent(_exchangeInteractableConnector);
        }
    }
}

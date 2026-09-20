using _Project.Scripts.InteractableObject;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class InteractableObjectLifetimeScope : LifetimeScope
    {
        [SerializeField] private InteractableConnector _connector;
        [SerializeField] private InteractableObjectView _view;

        /// <summary>この操作対象に属する接近判定と表示を登録する</summary>
        /// <param name="builder">対象専用コンテナの登録先</param>
        /// <example>PrefabのConnectorとViewをInspectorで指定して使用する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 複数の対象でも、指定した対象とViewだけを接続する
            if (_connector == null || _view == null)
            {
                Debug.LogError("InteractionのConnectorとViewを設定してください", this);
                return;
            }

            builder.RegisterComponent(_connector);
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<InteractableObjectPresenter>();
        }
    }
}

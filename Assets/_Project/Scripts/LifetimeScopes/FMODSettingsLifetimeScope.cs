using System;
using FMODSettings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>設定画面のViewとPresenterを所有する子Scope</summary>
    public sealed class FMODSettingsLifetimeScope : LifetimeScope
    {
        [SerializeField] private FMODVCASettingsView _view;

        /// <summary>Inspectorで指定された画面と、その購読処理を登録する</summary>
        /// <param name="builder">設定画面のコンテナ登録先</param>
        /// <example>FMODSettingsシーンでViewを割り当てて使用する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            if (_view == null)
                throw new InvalidOperationException("FMODSettingsLifetimeScopeにViewを設定してください！！！");

            // 音声サービスはRootから解決し、このScopeでは再登録しない
            builder.RegisterComponent(_view);
            builder.RegisterEntryPoint<FMODSettingsPresenter>(Lifetime.Scoped);
        }
    }
}

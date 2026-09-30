using SceneLoadServices;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>出口のPrefabを共通のScene読み込みサービスへ接続する</summary>
    [RequireComponent(typeof(SceneTransitionTrigger))]
    public sealed class SceneTransitionLifetimeScope : LifetimeScope
    {
        /// <summary>同じGameObjectにある出口へ依存関係を注入する</summary>
        /// <param name="builder">出口専用のコンテナ登録先</param>
        /// <example>SceneTransitionTrigger Prefabを配置するだけでRootと接続される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(GetComponent<SceneTransitionTrigger>());
        }
    }
}

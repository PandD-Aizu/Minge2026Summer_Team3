using FMODServices;
using FMODUnity;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>シーンのBGMをRootの共有サービスで再生し、シーン破棄後も継続する</summary>
    public sealed class FMODBGMLifetimeScope : LifetimeScope
    {
        [SerializeField] private EventReference _bgm;

        /// <summary>シーン開始時にBGMを要求し、同じ曲の再生中はそのまま継続する</summary>
        /// <param name="builder">Rootを親とするシーンのコンテナ登録先</param>
        /// <example>CampStageとFishingStageに同じBGMを設定して配置する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // インスタンスの所有と停止はRootに任せ、シーン側では再登録しない
            builder.RegisterBuildCallback(resolver => resolver.Resolve<FMODBGMService>().PlayBGM(_bgm));
        }
    }
}

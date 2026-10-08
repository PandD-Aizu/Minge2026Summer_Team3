using FMODServices;
using FMODUnity;
using _Project.Scripts.Core;
using R3;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>シーンのBGMをRootの共有サービスで再生し、シーン破棄後も継続する</summary>
    public sealed class FMODBGMLifetimeScope : LifetimeScope
    {
        [SerializeField] private EventReference _bgm;

        /// <summary>昼はBGMを継続再生し、夜はフェードアウトするよう昼夜の変更を購読する</summary>
        /// <param name="builder">Rootを親とするシーンのコンテナ登録先</param>
        /// <example>CampStageとFishingStageに同じBGMを設定して配置する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // インスタンスの所有と停止はRootに任せ、シーン側では再登録しない
            builder.RegisterBuildCallback(resolver =>
            {
                var bgmService = resolver.Resolve<FMODBGMService>();
                var progress = resolver.Resolve<GameProgress>();

                // 夜のシーンに到着した場合も、昼BGMを開始せず現在の時刻を反映する
                ApplyTimeOfDay(bgmService, progress.CurrentTimeOfDay);
                progress.TimeOfDayChanged
                    .Subscribe(time => ApplyTimeOfDay(bgmService, time))
                    .AddTo(this);
            });
        }

        /// <summary>現在の昼夜に合わせてこのシーンのBGMを再生または停止する</summary>
        /// <param name="bgmService">Rootが所有するBGMサービス</param>
        /// <param name="time">反映する昼夜</param>
        /// <example>夜の開始通知で昼BGMをフェードアウトする</example>
        private void ApplyTimeOfDay(FMODBGMService bgmService, TimeOfDay time)
        {
            if (_bgm.IsNull) return;

            if (time == TimeOfDay.Day)
            {
                bgmService.PlayBGM(_bgm);
            }
            else
            {
                bgmService.FadeOutBGM(_bgm.Guid.ToString());
            }
        }
    }
}

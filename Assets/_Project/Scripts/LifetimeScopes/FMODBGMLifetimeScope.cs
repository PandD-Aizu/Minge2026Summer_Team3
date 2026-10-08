using FMODServices;
using FMODSettings;
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

        /// <summary>昼と夕方のBGMを切り替え、夜に停止するため時間帯の変更を購読する</summary>
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

        /// <summary>昼はシーンのBGM、夕方はMidnight Deepを再生し、夜は両方を停止する</summary>
        /// <param name="bgmService">Rootが所有するBGMサービス</param>
        /// <param name="time">反映する昼夜</param>
        /// <example>夕方の開始通知でMidnight Deepを開始し、夜の開始通知で停止する</example>
        private void ApplyTimeOfDay(FMODBGMService bgmService, TimeOfDay time)
        {
            var eveningBgm = FMODEventPath.BGM_MIDNIGHT_DEEP.Reference;
            if (time == TimeOfDay.Day)
            {
                bgmService.FadeOutBGM(eveningBgm.Guid.ToString());
                if (!_bgm.IsNull) bgmService.PlayBGM(_bgm);
            }
            else
            {
                if (!_bgm.IsNull) bgmService.FadeOutBGM(_bgm.Guid.ToString());

                // 環境音には触れず、夕方以外では夕方用BGMを終了する
                if (time == TimeOfDay.Evening) bgmService.PlayBGM(eveningBgm);
                else bgmService.FadeOutBGM(eveningBgm.Guid.ToString());
            }
        }
    }
}

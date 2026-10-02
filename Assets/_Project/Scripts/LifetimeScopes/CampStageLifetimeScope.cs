using Controller;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>CampStageの帰還通知と時間帯表示を登録する</summary>
    public sealed class CampStageLifetimeScope : LifetimeScope
    {
        [SerializeField] private TimeOfDayView _timeOfDayView;

        /// <summary>集荷所への帰還と時間帯表示をシーンの依存関係に登録する</summary>
        /// <param name="builder">CampStageの依存関係の登録先</param>
        /// <example>CampStageのLifetimeScopeから自動実行される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(_timeOfDayView);
            builder.RegisterEntryPoint<TutorialCollectionReturnEntryPoint>();
            builder.RegisterEntryPoint<TimeOfDayPresenter>();
        }
    }
}

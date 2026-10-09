using System;
using Controller;
using PauseMenu;
using Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>CampStageの帰還通知と時間帯表示を登録する</summary>
    public sealed class CampStageLifetimeScope : LifetimeScope
    {
        [SerializeField] private TimeOfDayView _timeOfDayView;
        [SerializeField] private TimeOfDayLightingSettings _timeOfDayLighting = new();
        [SerializeField] private PlayerInputReader _playerInputReader;
        [SerializeField] private PauseMenuView _pauseMenuView;

        /// <summary>集荷所への帰還と時間帯表示をシーンの依存関係に登録する</summary>
        /// <param name="builder">CampStageの依存関係の登録先</param>
        /// <example>CampStageのLifetimeScopeから自動実行される</example>
        protected override void Configure(IContainerBuilder builder)
        {
            _timeOfDayView.ConfigureLighting(_timeOfDayLighting);
            builder.RegisterComponent(_timeOfDayView);
            builder.RegisterEntryPoint<TutorialCollectionReturnEntryPoint>();
            builder.RegisterEntryPoint<TimeOfDayPresenter>();

            if (_pauseMenuView == null) return;
            if (_playerInputReader == null)
                throw new InvalidOperationException("CampStageLifetimeScopeのPlayer Input Readerを設定してください");

            builder.RegisterComponent(_playerInputReader);
            builder.RegisterComponent(_pauseMenuView);
            builder.RegisterEntryPoint<PauseMenuPresenter>();
        }
    }
}

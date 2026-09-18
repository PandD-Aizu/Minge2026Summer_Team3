using System;
using SceneLoadServices;
using UnityEngine;
using R3;
using VContainer.Unity;

namespace Presentation
{
    public class TitleUIPresenter : IInitializable, IDisposable
    {
        private readonly SceneLoadService _sceneLoadService;
        private readonly TitleUIView _titleUIView;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        public TitleUIPresenter(SceneLoadService sceneLoadService, TitleUIView titleUIView)
        {
            _sceneLoadService = sceneLoadService;
            _titleUIView = titleUIView;
        }

        public void Initialize()
        {
            // Startボタンが押されたときの処理
            _titleUIView.OnStartButtonClick
                .ThrottleFirst(TimeSpan.FromSeconds(1))
                .Subscribe(async _ =>
                {
                    _titleUIView.SetInteractable(false);
                    var isLoaded = await _sceneLoadService.LoadSceneAsync("CampStage");
                    if (!isLoaded)
                        _titleUIView.SetInteractable(true);
                })
                .AddTo(_disposables);

            // 終了ボタンが押されたときの処理
            _titleUIView.OnEndButtonClick
                .ThrottleFirst(TimeSpan.FromSeconds(1))
                .Subscribe(_ =>
                {
                    _titleUIView.SetInteractable(false);
                    Application.Quit();
                })
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}

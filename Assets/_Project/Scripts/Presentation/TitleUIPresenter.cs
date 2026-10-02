using System;
using Cysharp.Threading.Tasks;
using Input;
using R3;
using SceneLoadServices;
using UnityEngine;
using VContainer.Unity;

namespace Presentation
{
    public class TitleUIPresenter : IInitializable, IDisposable
    {
        private readonly SceneLoadService _sceneLoadService;
        private readonly TitleUIView _titleUIView;
        private readonly InputModeService _inputMode;
        private readonly CompositeDisposable _disposables = new();
        private bool _isLoading;

        /// <summary>シーン遷移、タイトル表示、ゲーム入力の切り替えを受け取る</summary>
        /// <param name="sceneLoadService">Addressablesのシーン読み込みサービス</param>
        /// <param name="titleUIView">タイトル画面のボタン</param>
        /// <param name="inputMode">背後のゲーム操作を停止するサービス</param>
        /// <example>TitleLifetimeScopeのEntryPointとして生成する</example>
        public TitleUIPresenter(SceneLoadService sceneLoadService, TitleUIView titleUIView, InputModeService inputMode)
        {
            _sceneLoadService = sceneLoadService;
            _titleUIView = titleUIView;
            _inputMode = inputMode;
        }

        /// <summary>初期表示を整え、各ボタンの操作を購読する</summary>
        /// <example>タイトルシーン起動時にVContainerから呼ばれる</example>
        public void Initialize()
        {
            // タイトル上でインベントリなどのゲーム操作を受け付けない
            _inputMode.DisableAllInputs();
            _titleUIView.SetInteractable(true);
            _titleUIView.StartButton.Select();

            _titleUIView.OnStartButtonClick.Subscribe(_ => LoadSceneAsync("Intro", _titleUIView.StartButton).Forget()).AddTo(_disposables);
            _titleUIView.OnSettingButtonClick.Subscribe(_ => LoadSceneAsync("Option", _titleUIView.SettingButton).Forget()).AddTo(_disposables);
            _titleUIView.OnEndButtonClick.Subscribe(_ => QuitGame()).AddTo(_disposables);
        }

        /// <summary>多重実行を防ぎながらシーンを開き、失敗時は元のボタンへ操作を戻す</summary>
        /// <param name="address">Addressablesに登録した遷移先のアドレス</param>
        /// <param name="sourceButton">失敗時に再選択するボタン</param>
        /// <returns>シーン読み込みの完了を表す非同期処理</returns>
        /// <example>スタートからIntro、オプションからOptionを指定する</example>
        private async UniTask LoadSceneAsync(string address, UnityEngine.UI.Button sourceButton)
        {
            if (_isLoading) return;
            _isLoading = true;
            _titleUIView.SetInteractable(false);

            var isLoaded = await _sceneLoadService.LoadSceneAsync(address);
            if (!isLoaded && _titleUIView != null)
            {
                _isLoading = false;
                _titleUIView.SetInteractable(true);
                sourceButton.Select();
            }
        }

        /// <summary>ビルドではアプリを終了し、Editorでは再生を停止する</summary>
        /// <example>ゲームをやめるボタンから呼ぶ</example>
        private void QuitGame()
        {
            _titleUIView.SetInteractable(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>タイトルの破棄時にボタンの購読を解除する</summary>
        /// <example>シーン遷移時にVContainerから呼ばれる</example>
        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}

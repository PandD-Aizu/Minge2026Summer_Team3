using System;
using Cysharp.Threading.Tasks;
using FMODSettings;
using Input;
using R3;
using SceneLoadServices;
using VContainer.Unity;

namespace Presentation
{
    /// <summary>Optionシーンからタイトルへ戻る操作を管理する</summary>
    public sealed class OptionUIPresenter : IInitializable, IDisposable
    {
        private readonly FMODVCASettingsView _view;
        private readonly SceneLoadService _sceneLoader;
        private readonly InputModeService _inputMode;
        private readonly CompositeDisposable _subscriptions = new();
        private bool _isReturning;
        private bool _isDisposed;

        /// <summary>設定画面とシーン遷移、ゲーム入力の切り替えを受け取る</summary>
        /// <param name="view">既存の音量設定画面と戻るボタン</param>
        /// <param name="sceneLoader">Addressablesのシーン読み込みサービス</param>
        /// <param name="inputMode">設定中のゲーム操作を停止するサービス</param>
        /// <example>FMODSettingsLifetimeScopeのEntryPointとして生成する</example>
        public OptionUIPresenter(FMODVCASettingsView view, SceneLoadService sceneLoader, InputModeService inputMode)
        {
            _view = view;
            _sceneLoader = sceneLoader;
            _inputMode = inputMode;
        }

        /// <summary>ゲーム操作を停止し、タイトルへ戻るボタンを購読する</summary>
        /// <example>Optionシーンの開始時にVContainerから呼ばれる</example>
        public void Initialize()
        {
            _inputMode.DisableAllInputs();
            _view.BackButton.OnClickAsObservable()
                .Subscribe(_ => ReturnToTitleAsync().Forget()).AddTo(_subscriptions);
            _view.BackButton.Select();
        }

        /// <summary>タイトルへ移動し、失敗した場合は戻る操作を再度受け付ける</summary>
        /// <returns>シーン遷移の完了を表す非同期処理</returns>
        /// <example>戻るボタンのクリックからForgetで実行する</example>
        private async UniTask ReturnToTitleAsync()
        {
            if (_isReturning || _isDisposed) return;
            _isReturning = true;
            _view.BackButton.interactable = false;

            if (!await _sceneLoader.LoadSceneAsync("Title") && !_isDisposed && _view != null)
            {
                _isReturning = false;
                _view.BackButton.interactable = true;
                _view.BackButton.Select();
            }
        }

        /// <summary>シーンの破棄時に戻るボタンの購読を解除する</summary>
        /// <example>タイトルへの遷移時にVContainerから呼ばれる</example>
        public void Dispose()
        {
            _isDisposed = true;
            _subscriptions.Dispose();
        }
    }
}

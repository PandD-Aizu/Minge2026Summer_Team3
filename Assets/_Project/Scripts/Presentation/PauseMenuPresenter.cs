using System;
using Cysharp.Threading.Tasks;
using Input;
using PauseMenu;
using R3;
using SceneLoadServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer.Unity;

namespace Presentation
{
    /// <summary>通常操作中のEscとポーズ画面、タイトル帰還を管理する</summary>
    public sealed class PauseMenuPresenter : IInitializable, IDisposable
    {
        private readonly PauseMenuView _view;
        private readonly PlayerInputReader _playerInput;
        private readonly PlayerInputAction _actions;
        private readonly MenuInputService _menuInput;
        private readonly SceneLoadService _sceneLoader;
        private readonly CompositeDisposable _subscriptions = new();

        private IDisposable _inputBlock;
        private float _timeScaleBeforePause;
        private bool _isOpen;
        private bool _isLoading;
        private bool _isDisposed;

        /// <summary>同じシーンの画面とPlayer、Rootの入力とScene読み込みを受け取る</summary>
        /// <param name="view">ポーズ画面とボタン</param>
        /// <param name="playerInput">ゲーム操作の一時停止</param>
        /// <param name="actions">Escの入力元</param>
        /// <param name="menuInput">ほかのメニューとの排他制御</param>
        /// <param name="sceneLoader">Titleシーンの読み込み</param>
        public PauseMenuPresenter(PauseMenuView view, PlayerInputReader playerInput,
            PlayerInputAction actions, MenuInputService menuInput, SceneLoadService sceneLoader)
        {
            _view = view;
            _playerInput = playerInput;
            _actions = actions;
            _menuInput = menuInput;
            _sceneLoader = sceneLoader;
        }

        /// <summary>Escと二つのボタンを購読する</summary>
        /// <example>ゲームシーンのLifetimeScopeのEntryPointから呼ばれる</example>
        public void Initialize()
        {
            if (!_view.IsConfigured)
            {
                Debug.LogError("PauseMenuViewのPanelと二つのButtonを設定してください", _view);
                return;
            }

            _view.Hide();
            _actions.Player.Cancel.OnPerformedAsObservable()
                .Subscribe(_ => OnCancelPressed()).AddTo(_subscriptions);
            _actions.Player.Interact.OnPerformedAsObservable()
                .Subscribe(_ => ConfirmSelectedButton()).AddTo(_subscriptions);
            _view.OnResumeClicked.Subscribe(_ => Close()).AddTo(_subscriptions);
            _view.OnReturnToTitleClicked.Subscribe(_ => ReturnToTitleAsync().Forget()).AddTo(_subscriptions);
        }

        /// <summary>Escで確認パネルを開閉する</summary>
        private void OnCancelPressed()
        {
            if (_isDisposed || _isLoading) return;
            if (_isOpen)
            {
                Close();
                return;
            }

            // インベントリを閉じた同じEscではポーズを開かない
            if (_menuInput.WasReleasedThisFrame || _sceneLoader.IsLoading ||
                !_playerInput.CanStartGameplayAction) return;
            Open();
        }

        /// <summary>メニュー入力を確保し、プレイヤー操作とゲーム内時間を止める</summary>
        private void Open()
        {
            if (!_menuInput.TryAcquire(this)) return;
            _timeScaleBeforePause = Time.timeScale;
            _inputBlock = _playerInput.BlockGameplayInput();
            Time.timeScale = 0f;
            _isOpen = true;
            _view.Show();
        }

        /// <summary>Jで現在選択中のポーズ画面のButtonを決定する</summary>
        private void ConfirmSelectedButton()
        {
            if (!_isOpen || _isLoading || EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.TryGetComponent<Button>(out var button) ||
                !button.IsActive() || !button.IsInteractable()) return;

            button.onClick.Invoke();
        }

        /// <summary>Titleを読み込み、失敗したら確認パネルへ戻す</summary>
        /// <returns>シーン読み込みの完了まで待つUniTask</returns>
        private async UniTask ReturnToTitleAsync()
        {
            if (!_isOpen || _isLoading || _isDisposed) return;
            if (_view.TitleScene == null || !_view.TitleScene.IsAssigned)
            {
                Debug.LogError("PauseMenuViewのTitle Sceneを設定してください", _view);
                return;
            }

            _isLoading = true;
            _view.SetInteractable(false);
            var loaded = false;
            try
            {
                loaded = await _sceneLoader.LoadSceneAsync(_view.TitleScene);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (!loaded && !_isDisposed && _view != null)
                {
                    Time.timeScale = 0f;
                    _isLoading = false;
                    _view.SetInteractable(true);
                    _view.Show();
                }
            }
        }

        /// <summary>画面と入力所有権を閉じ、元の時間速度を戻す</summary>
        /// <example>ゲームへ戻るボタン、Esc、Scope破棄時に呼ぶ</example>
        private void Close()
        {
            if (!_isOpen || (_isLoading && !_isDisposed)) return;
            if (_view != null) _view.Hide();
            Time.timeScale = _timeScaleBeforePause;
            _inputBlock?.Dispose();
            _inputBlock = null;
            _menuInput.Release(this);
            _isOpen = false;
        }

        /// <summary>シーン破棄時に購読、操作停止、時間停止を解放する</summary>
        /// <example>ゲームシーンのLifetimeScopeの破棄時に呼ばれる</example>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _subscriptions.Dispose();
            Close();
        }
    }
}

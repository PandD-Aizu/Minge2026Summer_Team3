using System;
using Input;
using R3;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using VContainer.Unity;

namespace _Project.Scripts.Inventory
{
    /// <summary>インベントリ専用Prefabの生成とF・Esc・J・WASD操作を管理する</summary>
    public sealed class InventoryPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly PlayerInputAction _input;
        private readonly MenuInputService _menuInput;
        private readonly Func<InventoryView> _viewFactory;
        private readonly CompositeDisposable _subscriptions = new();
        private InventoryView _view;

        /// <summary>共通入力とインベントリ用Prefabを受け取る</summary>
        /// <param name="input">ゲーム全体の入力アクション</param>
        /// <param name="menuInput">メニュー同士の入力の競合を防ぐサービス</param>
        /// <param name="viewFactory">VContainerに登録したインベントリを初回だけ生成するFactory</param>
        /// <example>GameLifetimeScopeのEntryPointとして登録する</example>
        public InventoryPresenter(PlayerInputAction input, MenuInputService menuInput, Func<InventoryView> viewFactory)
        {
            _input = input;
            _menuInput = menuInput;
            _viewFactory = viewFactory;
        }

        /// <summary>開閉・決定キーとシーン終了の通知を購読する</summary>
        /// <example>Root Scopeの起動時にVContainerが呼ぶ</example>
        public void Initialize()
        {
            _input.Player.Inventory.OnPerformedAsObservable()
                .Subscribe(_ => Toggle()).AddTo(_subscriptions);
            _input.Player.Cancel.OnPerformedAsObservable()
                .Subscribe(_ => Close()).AddTo(_subscriptions);
            _input.Player.Interact.OnPerformedAsObservable()
                .Subscribe(_ => Confirm()).AddTo(_subscriptions);

            // Unityのシーンイベントも購読の破棄と同時に解除する
            Observable.FromEvent<UnityAction<Scene, Scene>, (Scene Previous, Scene Next)>(
                    handler => (previous, next) => handler((previous, next)),
                    handler => SceneManager.activeSceneChanged += handler,
                    handler => SceneManager.activeSceneChanged -= handler)
                .Subscribe(_ => Close()).AddTo(_subscriptions);
            Observable.FromEvent<UnityAction<Scene>, Scene>(
                    handler => scene => handler(scene),
                    handler => SceneManager.sceneUnloaded += handler,
                    handler => SceneManager.sceneUnloaded -= handler)
                .Subscribe(_ => Close()).AddTo(_subscriptions);
        }

        /// <summary>Fで開閉し、初回だけ専用Prefabを生成する</summary>
        /// <example>集荷所を操作中なら開かず、その画面の操作を維持する</example>
        private void Toggle()
        {
            if (_view != null && _view.gameObject.activeSelf)
            {
                Close();
                return;
            }

            if (!_menuInput.TryAcquire(this)) return;
            try
            {
                if (_view == null)
                {
                    _view = _viewFactory();
                    _view.OnCloseClicked.Subscribe(_ => Close()).AddTo(_subscriptions);
                    _view.OnUseClicked.Subscribe(_ => UseSelectedItem()).AddTo(_subscriptions);
                }

                _view.Show();
            }
            catch
            {
                Close();
                throw;
            }
        }

        /// <summary>Jでタブを確定するか、選択アイテムの使用処理を呼ぶ</summary>
        /// <example>タブ上では画面の切り替えだけを行う</example>
        private void Confirm()
        {
            if (_view != null && _view.gameObject.activeSelf && _view.Navigation.ConfirmSelection())
                UseSelectedItem();
        }

        /// <summary>選択中のアイテムを使用するための拡張箇所</summary>
        /// <example>今後、Navigation.SelectedItemに対応するアイテムの効果を実装する</example>
        private void UseSelectedItem()
        {
        }

        /// <summary>表示中の選択枠を動かし、通常操作が終わったら閉じる</summary>
        /// <example>ミニゲームへの移行時はインベントリを閉じる</example>
        public void Tick()
        {
            if (_view == null || !_view.gameObject.activeSelf) return;
            if (!_input.Player.enabled)
            {
                Close();
                return;
            }

            _view.Navigation.Navigate(_input.Player.Move.ReadValue<Vector2>());
        }

        /// <summary>画面を閉じて、このメニューの入力所有権を解除する</summary>
        /// <example>F・Esc・閉じるボタンから共通で呼ぶ</example>
        private void Close()
        {
            if (_view != null) _view.Hide();
            _menuInput.Release(this);
        }

        /// <summary>購読と入力所有権を解除し、Viewの破棄は親のRoot Scopeへ委ねる</summary>
        /// <example>Root Scopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            _subscriptions.Dispose();
            Close();
        }
    }
}

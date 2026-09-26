using System;
using _Project.Scripts.InteractableObject;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using VContainer.Unity;

namespace ExchangePoint
{
    public class ExchangePointPresenter : IInitializable, IAsyncStartable, ITickable, IDisposable
    {
        private readonly ExchangePointView _view;
        private readonly InteractableConnector _connector;
        private readonly PlayerInputReader _inputReader;
        private readonly Input.MenuInputService _menuInput;

        private readonly CompositeDisposable _disposables = new();
        private readonly SerialDisposable _movementBlock = new();

        /// <summary>集荷場の表示、接近判定、入力の依存関係を受け取る</summary>
        /// <param name="view">交換UI</param>
        /// <param name="connector">集荷場への接近判定</param>
        /// <param name="inputReader">プレイヤーの入力元</param>
        /// <param name="menuInput">メニュー同士の入力を排他制御するサービス</param>
        /// <example>VContainerのEntryPoint登録から生成する</example>
        public ExchangePointPresenter(ExchangePointView view, InteractableConnector connector, PlayerInputReader inputReader,
            Input.MenuInputService menuInput)
        {
            _view = view;
            _connector = connector;
            _inputReader = inputReader;
            _menuInput = menuInput;
        }

        /// <summary>開閉キーと表示状態の変更を購読する</summary>
        /// <example>VContainerの初期化時に呼ばれる</example>
        public void Initialize()
        {
            // ReactivePropertyの初回通知で現在の表示状態も同期する
            _movementBlock.AddTo(_disposables);
            _view.OnVisibilityChanged
                .Subscribe(HandleVisibilityChanged)
                .AddTo(_disposables);

            // 接近状態が変わったときだけ閉じる
            _connector.OnPlayerNearbyChanged
                .Where(nearby => !nearby)
                .Subscribe(_ => _view.SetVisible(false))
                .AddTo(_disposables);

            _inputReader.OnInteractPressed
                .Where(_ => _connector.IsPlayerNearby)
                .Subscribe(_ =>
                {
                    // 一覧を開いた次のJ入力で詳細パネルを開く
                    if (_view.IsVisible) _view.ConfirmSelection();
                    else if (_menuInput.TryAcquire(this))
                    {
                        _view.SetVisible(true);
                        // 非同期ロードが終わる前の入力では所有権を残さない
                        if (!_view.IsVisible) _menuInput.Release(this);
                    }
                })
                .AddTo(_disposables);

            _inputReader.OnCancelPressed
                .Subscribe(_ =>
                {
                    // 詳細を優先して閉じ、一覧と選択位置を維持する
                    if (_view.IsPopupOpen)
                    {
                        _view.SetPopupVisible(false);
                    }
                    else if (_view.IsVisible)
                    {
                        _view.SetVisible(false);
                    }
                })
                .AddTo(_disposables);
        }

        /// <summary>Scopeの寿命に合わせて一覧を非同期で初期化する</summary>
        /// <param name="cancellation">VContainerがScope破棄時にキャンセルするトークン</param>
        /// <returns>一覧の初期化完了を待つUniTask</returns>
        /// <example>VContainerのIAsyncStartableから自動実行される</example>
        public UniTask StartAsync(CancellationToken cancellation = default) => _view.InitializeAsync(cancellation);

        /// <summary>表示中のWASD入力と長押しによる商品選択を処理する</summary>
        /// <example>VContainerが毎フレーム呼ぶ</example>
        public void Tick()
        {
            _view.Navigate(_inputReader.NavigationInput);
        }

        /// <summary>ショップの表示中だけ歩行を止め、閉じたら入力所有権を解除する</summary>
        /// <param name="visible">ショップが表示されていればtrue</param>
        /// <example>キャンセルやViewの無効化でも歩行停止を解除する</example>
        private void HandleVisibilityChanged(bool visible)
        {
            _movementBlock.Disposable = visible ? _inputReader.BlockMovement() : null;
            if (!visible) _menuInput.Release(this);
        }

        /// <summary>購読と、このショップが保持する歩行停止を解除する</summary>
        /// <example>集荷場のLifetimeScope破棄時に呼ばれる</example>
        public void Dispose()
        {
            _disposables.Dispose();
            if (_view != null) _view.SetVisible(false);
            _menuInput.Release(this);
        }
    }
}

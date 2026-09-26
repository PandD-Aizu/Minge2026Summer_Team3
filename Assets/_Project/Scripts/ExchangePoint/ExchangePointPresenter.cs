using System;
using _Project.Scripts.InteractableObject;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace ExchangePoint
{
    public class ExchangePointPresenter : IInitializable, IAsyncStartable, ITickable, IDisposable
    {
        private readonly ExchangePointView _view;
        private readonly InteractableConnector _connector;
        private readonly PlayerInputReader _inputReader;

        private readonly CompositeDisposable _disposables = new();
        private readonly SerialDisposable _movementBlock = new();
        private Vector2Int _heldDirection;
        private float _nextMoveTime;
        private const float InitialRepeatDelay = 0.35f;
        private const float RepeatInterval = 0.12f;

        /// <summary>集荷場の表示、接近判定、入力の依存関係を受け取る</summary>
        /// <param name="view">交換UI</param>
        /// <param name="connector">集荷場への接近判定</param>
        /// <param name="inputReader">プレイヤーの入力元</param>
        /// <example>VContainerのEntryPoint登録から生成する</example>
        public ExchangePointPresenter(ExchangePointView view, InteractableConnector connector, PlayerInputReader inputReader)
        {
            _view = view;
            _connector = connector;
            _inputReader = inputReader;
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
                    if (_view.IsVisible) _view.SetPopupVisible(true);
                    else _view.SetVisible(true);
                })
                .AddTo(_disposables);

            _inputReader.OnCancelPressed
                .Subscribe(_ =>
                {
                    // 詳細を優先して閉じ、一覧と選択位置を維持する
                    if (_view.IsPopupOpen)
                    {
                        _view.SetPopupVisible(false);
                        _heldDirection = Vector2Int.zero;
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
            if (!_view.IsVisible) return;

            // 詳細パネルの表示中・ロード中は背後の商品選択を動かさない
            if (_view.IsPopupOpen)
            {
                _heldDirection = Vector2Int.zero;
                return;
            }

            // 斜め入力は横方向を優先し、1回に1枠だけ移動する
            Vector2 input = _inputReader.NavigationInput;
            Vector2Int direction = input.sqrMagnitude < 0.25f ? Vector2Int.zero
                : Mathf.Abs(input.x) >= Mathf.Abs(input.y)
                    ? new Vector2Int(input.x > 0f ? 1 : -1, 0)
                    : new Vector2Int(0, input.y > 0f ? 1 : -1);

            if (direction == Vector2Int.zero)
            {
                _heldDirection = Vector2Int.zero;
                return;
            }

            // 押した瞬間は即移動し、長押しは一定間隔で繰り返す
            float now = Time.unscaledTime;
            bool directionChanged = direction != _heldDirection;
            if (!directionChanged && now < _nextMoveTime) return;

            _view.MoveSelection(direction);
            _heldDirection = direction;
            _nextMoveTime = now + (directionChanged ? InitialRepeatDelay : RepeatInterval);
        }

        /// <summary>ショップの表示中だけ歩行を止め、キーリピートを初期化する</summary>
        /// <param name="visible">ショップが表示されていればtrue</param>
        /// <example>キャンセルやViewの無効化でも歩行停止を解除する</example>
        private void HandleVisibilityChanged(bool visible)
        {
            _heldDirection = Vector2Int.zero;
            _nextMoveTime = 0f;
            _movementBlock.Disposable = visible ? _inputReader.BlockMovement() : null;
        }

        /// <summary>購読と、このショップが保持する歩行停止を解除する</summary>
        /// <example>集荷場のLifetimeScope破棄時に呼ばれる</example>
        public void Dispose()
        {
            _disposables.Dispose();
            if (_view != null) _view.SetVisible(false);
        }
    }
}

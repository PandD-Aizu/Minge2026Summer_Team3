using UnityEngine;
using Input;
using R3;
using VContainer;

public class PlayerInputReader : MonoBehaviour
{
    private PlayerInputAction _inputActions;
    private bool _isSubscribed;
    private Input.MenuInputService _menuInput;

    private int _movementBlockCount;

    public Vector2 NavigationInput { get; private set; }
    public bool IsMenuOpen => _menuInput != null && _menuInput.IsOpen;
    public Vector2 MoveInput => _movementBlockCount > 0 || IsMenuOpen ? Vector2.zero : NavigationInput;

    /// <summary>UI操作中の歩行を止め、解除用の購読オブジェクトを返す</summary>
    /// <returns>Disposeすると、この呼び出しによる歩行停止を解除する</returns>
    /// <example>ショップ表示中だけ保持し、閉じるときにDisposeする</example>
    public System.IDisposable BlockMovement()
    {
        _movementBlockCount++;
        return Disposable.Create(() => _movementBlockCount--);
    }

    private readonly Subject<Unit> _interactPressed = new();
    private readonly Subject<Unit> _inventoryPressed = new();
    private readonly Subject<Unit> _cancelPressed = new();

    public Observable<Unit> OnInteractPressed => _interactPressed;
    public Observable<Unit> OnInventoryPressed => _inventoryPressed;
    public Observable<Unit> OnCancelPressed => _cancelPressed;

    /// <summary>共通入力とメニューの状態を受け取り、キー入力を購読する</summary>
    /// <param name="inputActions">ゲーム全体で共有する入力アクション</param>
    /// <param name="menuInput">メニュー表示中の歩行を制限するサービス</param>
    /// <example>シーンのLifetimeScopeがコンポーネントを登録したときに呼ぶ</example>
    [Inject]
    public void Construct(PlayerInputAction inputActions, Input.MenuInputService menuInput)
    {
        _menuInput = menuInput;
        _inputActions = inputActions;
        if (_isSubscribed) return;
        _isSubscribed = true;

        // 押下と解放を同じストリームで受け取り、破棄時の購読解除はR3へ委ねる
        _inputActions.Player.Move.OnPerformedAsObservable()
            .Merge(_inputActions.Player.Move.OnCanceledAsObservable())
            .Subscribe(context => NavigationInput = context.ReadValue<Vector2>())
            .AddTo(this);

        _inputActions.Player.Interact.OnPerformedAsObservable()
            .Subscribe(_ => _interactPressed.OnNext(Unit.Default)).AddTo(this);
        _inputActions.Player.Inventory.OnPerformedAsObservable()
            .Subscribe(_ => _inventoryPressed.OnNext(Unit.Default)).AddTo(this);
        _inputActions.Player.Cancel.OnPerformedAsObservable()
            .Subscribe(_ => _cancelPressed.OnNext(Unit.Default)).AddTo(this);

        if (isActiveAndEnabled)
        {
            _inputActions.Player.Enable();
        }
    }

    private void OnEnable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        if (_inputActions == null) return;
        _inputActions.Player.Disable();
        NavigationInput = Vector2.zero;
    }

    /// <summary>入力通知の発行元を破棄する</summary>
    /// <example>シーン終了時にUnityが呼び、AddTo(this)の購読も破棄される</example>
    private void OnDestroy()
    {
        _interactPressed.Dispose();
        _inventoryPressed.Dispose();
        _cancelPressed.Dispose();
    }

}

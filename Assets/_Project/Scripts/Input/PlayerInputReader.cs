using UnityEngine;
using UnityEngine.InputSystem;
using R3;
using VContainer;

public class PlayerInputReader : MonoBehaviour
{
    private PlayerInputAction _inputActions;
    private bool _isSubscribed;

    public Vector2 MoveInput { get; private set; }

    private readonly Subject<Unit> _interactPressed = new();
    private readonly Subject<Unit> _inventoryPressed = new();
    private readonly Subject<Unit> _cancelPressed = new();

    public Observable<Unit> OnInteractPressed => _interactPressed;
    public Observable<Unit> OnInventoryPressed => _inventoryPressed;
    public Observable<Unit> OnCancelPressed => _cancelPressed;

    [Inject]
    public void Construct(PlayerInputAction inputActions)
    {
        _inputActions = inputActions;
        if (_isSubscribed) return;
        _isSubscribed = true;

        _inputActions.Player.Move.performed += HandleMove;
        _inputActions.Player.Move.canceled += HandleMove;

        _inputActions.Player.Interact.performed += HandleInteract;
        _inputActions.Player.Inventory.performed += HandleInventory;
        _inputActions.Player.Cancel.performed += HandleCancel;

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
        MoveInput = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (_inputActions != null)
        {
            _inputActions.Player.Move.performed -= HandleMove;
            _inputActions.Player.Move.canceled -= HandleMove;

            _inputActions.Player.Inventory.performed -= HandleInventory;
            _inputActions.Player.Interact.performed -= HandleInteract;
            _inputActions.Player.Cancel.performed -= HandleCancel;
        }

        _interactPressed.Dispose();
        _inventoryPressed.Dispose();
        _cancelPressed.Dispose();
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    private void HandleInventory(InputAction.CallbackContext context)
    {
        _inventoryPressed.OnNext(Unit.Default);
    }

    private void HandleInteract(InputAction.CallbackContext context)
    {
        _interactPressed.OnNext(Unit.Default);
    }

    private void HandleCancel(InputAction.CallbackContext context)
    {
        _cancelPressed.OnNext(Unit.Default);
    }
}

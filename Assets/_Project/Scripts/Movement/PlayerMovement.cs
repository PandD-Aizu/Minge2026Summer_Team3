using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour , IPlayerPosition
{
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _gravity = -9.81f;


    private CharacterController _characterController;
    private PlayerInputReader _inputReader;
    private float _verticalVelocity;
    private const float GroundedVerticalVelocity = -2f;
    private PlayerAnimation _playerAnimation;

    public Transform PlayerPosition => transform;

    private void Awake()
    {
        if (_characterController == null)
        {
            _characterController = GetComponent<CharacterController>();
        }

        if (_inputReader == null)
        {
            _inputReader = GetComponent<PlayerInputReader>();
        }

        _playerAnimation = GetComponentInChildren<PlayerAnimation>();

    }

    private void OnEnable()
    {
        _verticalVelocity = 0f;
    }

    private void Update()
    {
        // 入力の左右・上下を、地面のX・Z方向へ変換する
        Vector2 input = _inputReader.MoveInput;
        var direction = new Vector3(input.x, 0f, input.y);

        // 斜めの移動スピードを１までに抑える
        Vector3.ClampMagnitude(direction, 1f);

        // 接地中は落下速度をリセットする
        if (_characterController.isGrounded && _verticalVelocity < 0f)
        {
            _verticalVelocity =  GroundedVerticalVelocity;
        }

        // 重力を加算
        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = direction * _moveSpeed;
        velocity.y = _verticalVelocity;

        _characterController.Move(velocity * Time.deltaTime);


    }

    /// 会話演出などでプレイヤーの位置と向きを変更する
    /// CharacterControllerの物理状態もリセットする
    public void TeleportTo(Transform target)
    {
        if (target == null) return;

        bool wasEnabled = _characterController.enabled;

        try
        {
            // CharacterControllerを一時停止
            _characterController.enabled = false;

            // 位置と向きを変更
            transform.SetPositionAndRotation(
                target.position,
                target.rotation
            );

            // 落下速度をリセット
            _verticalVelocity = 0f;

            Physics.SyncTransforms();
        }
        finally
        {
            // 元の有効状態に戻す
            _characterController.enabled = wasEnabled;
        }
    }
    /// <summary>
    /// プレイヤーの立ち絵を右向きにする
    /// </summary>
    public void FaceRight()
    {
        if (_playerAnimation != null)
        {
            _playerAnimation.FaceRight();
        }
        else
        {
            Debug.LogWarning("PlayerAnimationが見つかりません");
        }
    }
}

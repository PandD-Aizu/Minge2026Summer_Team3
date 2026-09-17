using System;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _gravity = -9.81f;


    private CharacterController _characterController;
    private PlayerInputReader _inputReader;
    private float _verticalVelocity;
    private const float GroundedVerticalVelocity = -2f;

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
}

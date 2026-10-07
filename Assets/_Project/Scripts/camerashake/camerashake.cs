using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

/// <summary>CinemachineのImpulseを使ってカメラを一度揺らす</summary>
public class CameraShaker : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource _impulseSource;
    [SerializeField] private float _size = -3;
    [SerializeField] private bool _enableTestInput = true;

    /// <summary>開発用SceneではLキーかゲームパッド南ボタンで揺れを試す</summary>
    /// <example>FishingStageでは_enableTestInputをfalseにして自動入力を止める</example>
    private void Update()
    {
        if (!_enableTestInput) return;
        if ((Keyboard.current?.lKey.wasPressedThisFrame ?? false) ||
            (Gamepad.current?.buttonSouth.wasPressedThisFrame ?? false)) TriggerShake();
    }

    /// <summary>設定されたImpulseSourceから一度だけ揺れを発生させる</summary>
    /// <example>カメラ演出を再生したいときに呼ぶ</example>
    public void TriggerShake()
    {
        if (_impulseSource != null)
        {
            _impulseSource.GenerateImpulse(new Vector3(1, _size, 0));
        }
    }

    /// <summary>
    /// GameOver時のカメラの揺れ
    /// </summary>
    public void TriggerBigShake()
    {
        if (_impulseSource != null) _impulseSource.GenerateImpulse(new Vector3(0, -5, 0));
    }
}

using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem; // 新しいInput Systemを使用する場合

public class CameraShaker : MonoBehaviour
{
    [SerializeField] private CinemachineImpulseSource impulseSource;

    void Update()
    {
        // キーボードのAキー、またはゲームパッドのAボタン（南側ボタン）が押された時
        if (Keyboard.current.lKey.wasPressedThisFrame ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame))
        {
            TriggerShake();
        }
    }

    public void TriggerShake()
    {
        if (impulseSource != null)
        {
            // Y軸（縦方向）に「-1」の力で揺らします
            impulseSource.GenerateImpulse(new Vector3(0, -1, 0));
        }
    }
}





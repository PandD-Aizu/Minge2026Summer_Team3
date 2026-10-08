using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine; // Cinemachineの名前空間を追加

public class CameraSwitchController : MonoBehaviour
{
    [Header("Cameras")]
    // Camera から CinemachineCamera に変更
    [SerializeField] private CinemachineCamera followPlayerCamera;
    [SerializeField] private Camera zoomCamera;

    [Header("UI")]
    [SerializeField] private GameObject itemListPanel;

    private bool isZooming = false;

    void Start()
    {
        // 最初は通常カメラを有効に
        if (followPlayerCamera != null) followPlayerCamera.enabled = true;
        if (zoomCamera != null) zoomCamera.enabled = false;

        if (itemListPanel != null)
        {
            itemListPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Tキーが押されたとき
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            // 現在ズーム中ならアウト（解除）、そうでないならズームイン
            if (isZooming)
            {
                ExitZoomCamera();
            }
            else
            {
                EnterZoomCamera();
            }
        }
    }

    void EnterZoomCamera()
    {
        isZooming = true;

        // enabledの有効・無効で優先度を切り替えます
        if (followPlayerCamera != null) followPlayerCamera.enabled = false;
        if (zoomCamera != null) zoomCamera.enabled = true;

        if (itemListPanel != null)
        {
            itemListPanel.SetActive(false);
        }
    }

    void ExitZoomCamera()
    {
        isZooming = false;

        if (zoomCamera != null) zoomCamera.enabled = false;
        if (followPlayerCamera != null) followPlayerCamera.enabled = true;

        if (itemListPanel != null)
        {
            itemListPanel.SetActive(false);
        }
    }
}


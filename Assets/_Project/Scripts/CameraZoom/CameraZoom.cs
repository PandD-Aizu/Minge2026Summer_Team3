using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera followPlayerCamera;
    [SerializeField] private CinemachineCamera zoomCamera;

    [Header("UI")]
    [SerializeField] private GameObject itemListPanel;

    private bool isZooming;

    /// <summary>
    /// シーン開始時に通常表示を設定する Unityが自動で呼び出す 引数と戻り値はなし
    /// </summary>
    private void Start()
    {
        // 参照不足の状態ではカメラを切り替えない
        if (followPlayerCamera == null || zoomCamera == null || followPlayerCamera == zoomCamera)
        {
            Debug.LogError("通常用とズーム用に別々のCinemachineCameraを設定してほしい", this);
            enabled = false;
            return;
        }

        SetZoom(false);
    }

    /// <summary>
    /// Tキーでズーム表示を切り替える Unityが毎フレーム呼び出す 引数と戻り値はなし
    /// </summary>
    private void Update()
    {
        // キーボードが接続されている場合だけ入力を確認する
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            SetZoom(!isZooming);
        }
    }

    /// <summary>
    /// 表示するバーチャルカメラを設定する 戻り値はなし
    /// 例: SetZoom(true)でズーム表示に切り替える
    /// </summary>
    /// <param name="zooming">trueならズーム表示 falseなら通常表示</param>
    private void SetZoom(bool zooming)
    {
        // 描画用Cameraは有効のまま Brainが有効なバーチャルカメラへ切り替える
        isZooming = zooming;
        followPlayerCamera.enabled = !zooming;
        zoomCamera.enabled = zooming;

        // カメラの切り替え時はアイテム一覧を閉じる
        if (itemListPanel != null)
        {
            itemListPanel.SetActive(false);
        }
    }
}

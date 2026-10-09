using UnityEngine;
using Unity.Cinemachine;

public class CameraZoom : MonoBehaviour
{
    [Header("Cameras")]
    [SerializeField] private CinemachineCamera followPlayerCamera;
    [SerializeField] private CinemachineCamera zoomCamera;

    [Header("UI")]
    [SerializeField] private GameObject itemListPanel;

    /// <summary>通常表示で初期化する 引数と戻り値はなし</summary>
    /// <example>Unityが会話開始より前に呼び出す</example>
    private void Awake()
    {
        SetZoom(false);
    }

    /// <summary>表示するバーチャルカメラを設定する 戻り値はなし</summary>
    /// <param name="zooming">trueならズーム表示 falseなら通常表示</param>
    /// <example>ラジオ会話の開始時にSetZoom(true)、終了時にSetZoom(false)を呼ぶ</example>
    public void SetZoom(bool zooming)
    {
        // 参照不足や同じカメラの指定では切り替えない
        if (followPlayerCamera == null || zoomCamera == null || followPlayerCamera == zoomCamera)
        {
            Debug.LogError("通常用とズーム用に別々のCinemachineCameraを設定してほしい", this);
            return;
        }

        // 描画用Cameraは有効のまま Brainがバーチャルカメラを切り替える
        followPlayerCamera.enabled = !zooming;
        zoomCamera.enabled = zooming;

        // 切り替え時はアイテム一覧を閉じる
        if (itemListPanel != null)
        {
            itemListPanel.SetActive(false);
        }
    }

    /// <summary>無効化時に通常表示へ戻す 引数と戻り値はなし</summary>
    /// <example>会話中にカメラ管理オブジェクトを無効化した場合にUnityが呼ぶ</example>
    private void OnDisable()
    {
        if (followPlayerCamera != null && zoomCamera != null && followPlayerCamera != zoomCamera)
            SetZoom(false);
    }
}

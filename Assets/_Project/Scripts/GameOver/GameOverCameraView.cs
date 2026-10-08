using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineCamera))]
public sealed class GameOverCameraView : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private Renderer _playerVisual;
    [SerializeField] private int _inactivePriority = 0;
    [SerializeField] private int _activePriority = 20;
    [SerializeField, Min(0f)] private float _blendWaitSeconds = 2.1f;

    private bool _wasPlayerVisualEnabled;
    private bool _activated;

    /// <summary>同じオブジェクトの仮想カメラを取得する</summary>
    private void Awake()
    {
        if (_camera == null) _camera = GetComponent<CinemachineCamera>();
    }

    /// <summary>ゲームオーバー用カメラへ切り替える</summary>
    /// <param name="cancellation">シーン破棄時の中断トークン</param>
    /// <returns>カメラのブレンドが終わるまでの待機</returns>
    public async UniTask ActivateAsync(CancellationToken cancellation)
    {
        _activated = true;
        if (_playerVisual != null)
        {
            _wasPlayerVisualEnabled = _playerVisual.enabled;
            _playerVisual.enabled = false;
        }

        _camera.Priority = _activePriority;
        await UniTask.Delay(System.TimeSpan.FromSeconds(_blendWaitSeconds),
            DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellation);
    }

    /// <summary>通常の追従カメラへ戻す</summary>
    public void Deactivate()
    {
        if (!_activated) return;
        _activated = false;
        _camera.Priority = _inactivePriority;
        if (_playerVisual != null) _playerVisual.enabled = _wasPlayerVisualEnabled;
    }
}

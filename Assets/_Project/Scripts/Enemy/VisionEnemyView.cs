using System.Collections.Generic;
using System.Threading;
using _Project.Scripts.Data.Enemy;
using Cysharp.Threading.Tasks;
using FMODUnity;
using UnityEngine;

/// <summary>敵の見た目と移動を反映する</summary>
public sealed class VisionEnemyView : MonoBehaviour
{
    [SerializeField] private EnemyDefinition _enemyDefinition;
    [SerializeField] private EventReference _biteSound;
    [Header("敵の接近音")]
    [SerializeField, Min(0f)] private float _closeEnemyStartDistance = 5f;
    [SerializeField, Min(0f)] private float _closeEnemyStopDistance = 7f;
    [Header("発見中の心音")]
    [SerializeField, Min(0f)] private float _heartbeatNearDistance = 1f;
    [SerializeField, Min(0.01f)] private float _heartbeatFarDistance = 15f;
    [SerializeField] private Vector2 _heartbeatPitchRange = new(0.8f, 1.6f);
    [SerializeField] private Vector2 _heartbeatVolumeRange = new(0.25f, 1f);
    [SerializeField, Min(0f)] private float _captureStartOffset = 2.5f;
    [SerializeField, Min(0f)] private float _captureApproachDuration = 1.8f;
    [SerializeField, Min(0f)] private float _captureLungeDuration = 0.1f;

    private Renderer[] _renderers;
    private Transform[] _visualTransforms;
    private Vector3[] _originalPositions;
    private Vector3 _captureDirection;

    public Vector3 Position => transform.position;
    public float MoveSpeed => _enemyDefinition != null ? _enemyDefinition.MoveSpeed : 0f;
    public EventReference BiteSound => _biteSound;

    /// <summary>接近音の再生状態と距離から、再生を続けるか判定する</summary>
    /// <param name="distance">敵とプレイヤーの距離</param>
    /// <param name="isPlaying">接近音を再生中ならtrue</param>
    /// <returns>接近音を再生する範囲ならtrue</returns>
    /// <example>ShouldPlayCloseEnemy(distance, isPlaying)で境界付近の連続開閉を防ぐ</example>
    public bool ShouldPlayCloseEnemy(float distance, bool isPlaying)
    {
        var start = Mathf.Max(0f, _closeEnemyStartDistance);
        var stop = Mathf.Max(start + 0.01f, _closeEnemyStopDistance);
        return isPlaying ? distance < stop : distance <= start;
    }

    /// <summary>敵との距離から心音の速度兼ピッチと音量を求める</summary>
    /// <param name="distance">敵とプレイヤーの距離</param>
    /// <returns>xが速度兼ピッチ倍率、yが音量倍率</returns>
    /// <example>追跡中にGetHeartbeatLevels(distance)を呼ぶ</example>
    public Vector2 GetHeartbeatLevels(float distance)
    {
        var near = Mathf.Max(0f, _heartbeatNearDistance);
        var far = Mathf.Max(near + 0.01f, _heartbeatFarDistance);
        var proximity = 1f - Mathf.InverseLerp(near, far, distance);
        var minPitch = Mathf.Max(0.01f, _heartbeatPitchRange.x);
        var maxPitch = Mathf.Max(minPitch, _heartbeatPitchRange.y);
        var minVolume = Mathf.Clamp01(_heartbeatVolumeRange.x);
        var maxVolume = Mathf.Clamp(_heartbeatVolumeRange.y, minVolume, 1f);
        return new Vector2(Mathf.Lerp(minPitch, maxPitch, proximity),
            Mathf.Lerp(minVolume, maxVolume, proximity));
    }

    /// <summary>見た目のRendererを取得する</summary>
    /// <example>Unityがコンポーネント生成時に呼ぶ</example>
    private void Awake()
    {
        EnsureRenderers();
    }

    /// <summary>見た目の表示を切り替える</summary>
    /// <param name="visible">表示する場合はtrue</param>
    /// <example>昼はSetVisible(false)で姿を消す</example>
    public void SetVisible(bool visible)
    {
        EnsureRenderers();
        foreach (var renderer in _renderers) renderer.enabled = visible;
    }

    /// <summary>描画物だけを後方へ置き、NavMeshAgentと接触判定は動かさない</summary>
    /// <param name="cameraPosition">一人称カメラの位置</param>
    /// <example>暗転中に呼んで接近演出を準備する</example>
    public void PrepareCapture(Vector3 cameraPosition)
    {
        EnsureRenderers();
        Vector3 direction = transform.position - cameraPosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        _captureDirection = direction.normalized;

        // 同じTransform上の複数Rendererを一回だけ動かす
        var visuals = new HashSet<Transform>();
        foreach (var renderer in _renderers) visuals.Add(renderer.transform);
        _visualTransforms = new Transform[visuals.Count];
        visuals.CopyTo(_visualTransforms);
        _originalPositions = new Vector3[_visualTransforms.Length];
        for (var i = 0; i < _visualTransforms.Length; i++)
        {
            _originalPositions[i] = _visualTransforms[i].position;
        }

        SetCaptureOffset(_captureStartOffset);
    }

    /// <summary>敵の描画物をゆっくり近づける</summary>
    /// <param name="cancellation">演出の中断トークン</param>
    /// <returns>接近完了までの待機</returns>
    public UniTask ApproachCaptureAsync(CancellationToken cancellation)
    {
        return MoveCaptureVisualsAsync(_captureStartOffset, 2f, _captureApproachDuration, cancellation);
    }

    /// <summary>敵の描画物をカメラへ一気に近づける</summary>
    /// <param name="cancellation">演出の中断トークン</param>
    /// <returns>突進完了までの待機</returns>
    public UniTask LungeCaptureAsync(CancellationToken cancellation)
    {
        return MoveCaptureVisualsAsync(2f, -0.4f, _captureLungeDuration, cancellation);
    }

    /// <summary>シーン移動失敗時に描画物の位置を戻す</summary>
    /// <example>捕獲演出が中断されたときに呼ぶ</example>
    public void RestoreCaptureVisuals()
    {
        if (_visualTransforms == null) return;
        for (var i = 0; i < _visualTransforms.Length; i++)
        {
            if (_visualTransforms[i] != null) _visualTransforms[i].position = _originalPositions[i];
        }

        _visualTransforms = null;
        _originalPositions = null;
    }

    /// <summary>指定した奥行きへ描画物を補間する</summary>
    /// <param name="start">開始時の相対距離</param>
    /// <param name="end">終了時の相対距離</param>
    /// <param name="duration">補間にかける秒数</param>
    /// <param name="cancellation">演出の中断トークン</param>
    /// <returns>補間完了までの待機</returns>
    private async UniTask MoveCaptureVisualsAsync(float start, float end, float duration,
        CancellationToken cancellation)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
            elapsed += Time.unscaledDeltaTime;
            SetCaptureOffset(Mathf.Lerp(start, end, elapsed / duration));
        }

        SetCaptureOffset(end);
    }

    /// <summary>捕獲開始位置からの相対オフセットを描画物に反映する</summary>
    /// <param name="offset">敵の奥方向への移動距離</param>
    private void SetCaptureOffset(float offset)
    {
        for (var i = 0; i < _visualTransforms.Length; i++)
        {
            _visualTransforms[i].position = _originalPositions[i] + _captureDirection * offset;
        }
    }

    /// <summary>Awakeより先にVContainerから呼ばれてもRendererを取得する</summary>
    private void EnsureRenderers()
    {
        if (_renderers != null) return;
        _renderers = GetComponentsInChildren<Renderer>(true);
    }
}

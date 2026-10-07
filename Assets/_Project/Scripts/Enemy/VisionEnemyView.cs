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

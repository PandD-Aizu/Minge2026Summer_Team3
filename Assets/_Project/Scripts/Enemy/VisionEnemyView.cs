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
    [Header("発見中の砂嵐")]
    [SerializeField, Min(0f)] private float _staticNearDistance = 1f;
    [SerializeField, Min(0.01f)] private float _staticFarDistance = 15f;
    [SerializeField, Range(0f, 1f)] private float _staticMinIntensity = 0.08f;
    [SerializeField, Range(0f, 1f)] private float _staticMaxIntensity = 0.55f;
    [SerializeField, Min(0f)] private float _staticFadeOutDuration = 1.5f;
    [SerializeField, Range(0f, 1f)] private float _glitchStrength = 0.6f;
    [Header("逃げ切りと再発見")]
    [SerializeField, Min(0.1f)] private float _chaseDuration = 5f;
    [SerializeField, Min(0.1f)] private float _rediscoveryDelay = 4f;
    [Header("ステルス迷彩")]
    [SerializeField] private Shader _cloakShader;
    [SerializeField, Min(0.1f)] private float _cloakFadeDuration = 1.5f;
    [Header("発見中の心音")]
    [SerializeField, Range(0f, 0.05f)] private float _heartbeatRippleStrength = 0.025f;
    [SerializeField, Min(0.001f)] private float _heartbeatRippleThreshold = 0.025f;
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
    private Material[][] _originalMaterials;
    private Material[][] _cloakMaterials;
    private UnityEngine.Rendering.ShadowCastingMode[] _originalShadows;
    private bool _usingCloak;
    private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

    public Vector3 Position => transform.position;
    public float ChaseDuration => Mathf.Max(0.1f, _chaseDuration);
    public float RediscoveryDelay => Mathf.Max(0.1f, _rediscoveryDelay);
    public float CaptureLungeDuration => Mathf.Max(0f, _captureLungeDuration);
    public float CloakFadeDuration => Mathf.Max(0.1f, _cloakFadeDuration);
    public float MoveSpeed => _enemyDefinition != null ? _enemyDefinition.MoveSpeed : 0f;
    public EventReference BiteSound => _biteSound;
    public float StaticFadeOutDuration => Mathf.Max(0f, _staticFadeOutDuration);
    public float GlitchStrength => Mathf.Clamp01(_glitchStrength);
    public float HeartbeatRippleStrength => _heartbeatRippleStrength;
    public float HeartbeatRippleThreshold => _heartbeatRippleThreshold;

    /// <summary>敵に近づくほど強くなる砂嵐の濃さを求める</summary>
    /// <param name="distance">敵とプレイヤーの距離</param>
    /// <returns>0から1の濃さ</returns>
    /// <example>発見中にGetStaticIntensity(distance)を画面演出へ渡す</example>
    public float GetStaticIntensity(float distance)
    {
        var near = Mathf.Max(0f, _staticNearDistance);
        var far = Mathf.Max(near + 0.01f, _staticFarDistance);
        var minimum = Mathf.Clamp01(_staticMinIntensity);
        var maximum = Mathf.Clamp(_staticMaxIntensity, minimum, 1f);
        return Mathf.Lerp(maximum, minimum, Mathf.InverseLerp(near, far, distance));
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

    /// <summary>迷彩用マテリアルで輪郭を揺らし、不透明度を反映する</summary>
    /// <param name="opacity">0で透明、1で通常の濃さ</param>
    /// <example>消失中にSetCloakOpacity(1f - progress)を呼ぶ</example>
    public void SetCloakOpacity(float opacity)
    {
        EnsureRenderers();
        if (_cloakShader == null) return;
        if (_cloakMaterials == null) CreateCloakMaterials();
        for (var i = 0; i < _renderers.Length; i++)
        {
            if (!_usingCloak)
            {
                _renderers[i].sharedMaterials = _cloakMaterials[i];
                _renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            foreach (var material in _cloakMaterials[i])
                if (material != null) material.SetFloat(OpacityId, Mathf.Clamp01(opacity));
        }

        _usingCloak = true;
    }

    /// <summary>再出現完了または昼夜変更で元のマテリアルと影を戻す</summary>
    /// <example>迷彩演出を中断した場合にも呼ぶ</example>
    public void RestoreCloakMaterials()
    {
        if (!_usingCloak) return;
        for (var i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] == null) continue;
            _renderers[i].sharedMaterials = _originalMaterials[i];
            _renderers[i].shadowCastingMode = _originalShadows[i];
        }

        _usingCloak = false;
    }

    /// <summary>共有アセットを変更せず、敵専用の迷彩マテリアルを一度だけ作る</summary>
    /// <example>初回の透明化時に呼ぶ</example>
    private void CreateCloakMaterials()
    {
        _originalMaterials = new Material[_renderers.Length][];
        _cloakMaterials = new Material[_renderers.Length][];
        _originalShadows = new UnityEngine.Rendering.ShadowCastingMode[_renderers.Length];
        for (var i = 0; i < _renderers.Length; i++)
        {
            _originalMaterials[i] = _renderers[i].sharedMaterials;
            _originalShadows[i] = _renderers[i].shadowCastingMode;
            _cloakMaterials[i] = new Material[_originalMaterials[i].Length];
            for (var j = 0; j < _originalMaterials[i].Length; j++)
            {
                var source = _originalMaterials[i][j];
                if (source == null) continue;
                var material = new Material(_cloakShader) { name = source.name + " (Cloak)" };
                var textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                if (source.HasProperty(textureProperty))
                {
                    material.SetTexture("_BaseMap", source.GetTexture(textureProperty));
                    material.SetTextureScale("_BaseMap", source.GetTextureScale(textureProperty));
                    material.SetTextureOffset("_BaseMap", source.GetTextureOffset(textureProperty));
                }

                var color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                    : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Cutoff", source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0f
                    && source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.001f);
                _cloakMaterials[i][j] = material;
            }
        }
    }

    /// <summary>シーン破棄時に生成した迷彩マテリアルを解放する</summary>
    /// <example>Unityが敵オブジェクトの破棄時に呼ぶ</example>
    private void OnDestroy()
    {
        RestoreCloakMaterials();
        if (_cloakMaterials == null) return;
        foreach (var materials in _cloakMaterials)
            foreach (var material in materials)
                if (material != null) Destroy(material);
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

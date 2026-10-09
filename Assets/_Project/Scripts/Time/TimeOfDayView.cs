using _Project.Scripts.Core;
using UnityEngine;

/// <summary>LifetimeScopeから夕方と明け方の色、光線、遷移時間を調整する設定</summary>
[System.Serializable]
public sealed class TimeOfDayLightingSettings
{
    [Header("夕方")]
    public Color EveningLightColor = new Color(1f, 0.32f, 0.2f);
    [ColorUsage(false, true)] public Color EveningSkyTint = new Color(1.8f, 0.45f, 0.3f);
    [Range(0f, 1f)] public float EveningSunlightVisibility = 0.4f;

    [Header("明け方")]
    public Color DawnLightColor = new Color(1f, 0.48f, 0.42f);
    [ColorUsage(false, true)] public Color DawnSkyTint = new Color(1.5f, 0.65f, 0.6f);
    [Range(0f, 1f)] public float DawnSunlightVisibility = 0.6f;

    [Header("時間帯の切り替え")]
    [Min(0f)] public float TransitionDuration = 2f;
}

public class TimeOfDayView : MonoBehaviour
{
    [SerializeField] private Light _directionalLight;
    [SerializeField] private Color _dayLightColor = Color.cornsilk;
    [SerializeField] private Color _nightLightColor = Color.midnightBlue;
    [SerializeField] private Renderer _sunlightRenderer;
    private float _transitionDuration = 2f;

    private static readonly int SunlightFadeId = Shader.PropertyToID("_SunlightFade");
    private MaterialPropertyBlock _sunlightProperties;
    private bool _initialized;
    private bool _isTransitioning;
    private float _elapsed;
    private float _visibility = 1f;
    private float _startVisibility;
    private float _targetVisibility;
    private Color _startLightColor;
    private Color _targetLightColor;
    private TimeOfDayLightingSettings _settings = new();
    private Material _originalSkybox;
    private Material _runtimeSkybox;
    private readonly string[] _skyColorProperties = { "_Zenith", "_Horizon", "_CloudLight", "_CloudShadow", "_SkyTint", "_Tint", "_GroundColor" };
    private Color[] _originalSkyColors;
    private Color _skyTint = Color.white;
    private Color _startSkyTint;
    private Color _targetSkyTint;

    /// <summary>LifetimeScopeで指定した時間帯の演出設定を受け取る</summary>
    /// <param name="settings">夕方と明け方の色、光線の表示率、遷移秒数</param>
    /// <example>ConfigureLighting(_timeOfDayLighting)</example>
    public void ConfigureLighting(TimeOfDayLightingSettings settings)
    {
        _settings = settings ?? new TimeOfDayLightingSettings();
    }

    /// <summary>初回は時間帯を即時反映し、以降は太陽色と光の筋を滑らかに切り替える</summary>
    /// <param name="timeOfDay">適用する朝、夕方、夜、明け方の状態</param>
    /// <example>ApplyTimeToSky(TimeOfDay.Night)</example>
    public void ApplyTimeToSky(TimeOfDay timeOfDay)
    {
        bool night = timeOfDay == TimeOfDay.Night;
        Color targetColor = night ? _nightLightColor : _dayLightColor;
        float targetVisibility = night ? 0f : 1f;
        Color targetSkyTint = night ? new Color(0.08f, 0.1f, 0.22f) : Color.white;
        _transitionDuration = Mathf.Max(0f, _settings.TransitionDuration);

        // 夕方は暖色と弱い光線にし、夜の暗さとは区別する
        if (timeOfDay == TimeOfDay.Evening)
        {
            targetColor = _settings.EveningLightColor;
            targetVisibility = _settings.EveningSunlightVisibility;
            targetSkyTint = _settings.EveningSkyTint;
        }
        else if (timeOfDay == TimeOfDay.Dawn)
        {
            targetColor = _settings.DawnLightColor;
            targetVisibility = _settings.DawnSunlightVisibility;
            targetSkyTint = _settings.DawnSkyTint;
        }

        // 同じ時間帯の通知では、進行中のフェードを最初からやり直さない
        if (_initialized && _targetLightColor == targetColor && _targetVisibility == targetVisibility &&
            _targetSkyTint == targetSkyTint)
            return;

        _targetLightColor = targetColor;
        _targetVisibility = targetVisibility;
        _targetSkyTint = targetSkyTint;

        // 夜のセーブから開始するときは、昼の光を一瞬表示しない
        if (!_initialized || !isActiveAndEnabled || _transitionDuration <= 0f)
        {
            _initialized = true;
            _isTransitioning = false;
            ApplyLighting(_targetLightColor, _targetVisibility);
            ApplySkyTint(_targetSkyTint);
            return;
        }

        // 途中で昼夜が反転しても、その時点の明るさから連続して補間する
        _startLightColor = _directionalLight != null ? _directionalLight.color : _targetLightColor;
        _startSkyTint = _skyTint;
        _startVisibility = _visibility;
        _elapsed = 0f;
        _isTransitioning = true;
    }

    /// <summary>ゲーム時間に合わせて光を補間し、ポーズ中はフェードも停止する</summary>
    /// <example>夜への切替後、約2秒かけて光の筋が消える</example>
    private void Update()
    {
        if (!_isTransitioning) return;

        _elapsed += Time.deltaTime;
        float progress = _transitionDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _transitionDuration);
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        ApplyLighting(Color.Lerp(_startLightColor, _targetLightColor, eased),
            Mathf.Lerp(_startVisibility, _targetVisibility, eased));
        ApplySkyTint(Color.Lerp(_startSkyTint, _targetSkyTint, eased));

        if (progress >= 1f) _isTransitioning = false;
    }

    /// <summary>太陽色と光線の透明度を反映し、透明度が0になったら描画を止める</summary>
    /// <param name="lightColor">現在の太陽色</param>
    /// <param name="visibility">光線の表示率、0が非表示で1が通常の明るさ</param>
    /// <example>ApplyLighting(_nightLightColor, 0f)</example>
    private void ApplyLighting(Color lightColor, float visibility)
    {
        if (_directionalLight != null) _directionalLight.color = lightColor;
        _visibility = visibility;
        if (_sunlightRenderer == null) return;

        // 共有素材を変更せず、このシーンのRendererだけにフェード値を渡す
        _sunlightProperties ??= new MaterialPropertyBlock();
        _sunlightRenderer.GetPropertyBlock(_sunlightProperties);
        _sunlightProperties.SetFloat(SunlightFadeId, visibility);
        _sunlightRenderer.SetPropertyBlock(_sunlightProperties);
        _sunlightRenderer.enabled = visibility > 0f;
    }

    /// <summary>無効化時は切替先を確定し、再有効化時に中途半端な光が残るのを防ぐ</summary>
    /// <example>フェード中にTimeOfDayViewを無効化すると切替先の明るさになる</example>
    private void OnDisable()
    {
        if (!_initialized) return;

        _isTransitioning = false;
        ApplyLighting(_targetLightColor, _targetVisibility);
        ApplySkyTint(_targetSkyTint);
    }

    /// <summary>空と雲の元の色に時間帯の色を乗算し、共有マテリアルを保護する</summary>
    /// <param name="tint">朝を白とする空の色倍率</param>
    /// <example>ApplySkyTint(_settings.EveningSkyTint)</example>
    private void ApplySkyTint(Color tint)
    {
        _skyTint = tint;
        if (_runtimeSkybox == null)
        {
            if (RenderSettings.skybox == null) return;

            // シーン専用の複製だけを書き換え、アセットには色を保存しない
            _originalSkybox = RenderSettings.skybox;
            _runtimeSkybox = new Material(_originalSkybox);
            _originalSkyColors = new Color[_skyColorProperties.Length];
            for (int i = 0; i < _skyColorProperties.Length; i++)
            {
                if (_runtimeSkybox.HasProperty(_skyColorProperties[i]))
                    _originalSkyColors[i] = _runtimeSkybox.GetColor(_skyColorProperties[i]);
            }
            RenderSettings.skybox = _runtimeSkybox;
        }

        for (int i = 0; i < _skyColorProperties.Length; i++)
        {
            if (_runtimeSkybox.HasProperty(_skyColorProperties[i]))
                _runtimeSkybox.SetColor(_skyColorProperties[i], _originalSkyColors[i] * tint);
        }
    }

    /// <summary>シーン専用の空マテリアルを解放し、使用中なら元の空へ戻す</summary>
    /// <example>シーン遷移でTimeOfDayViewが破棄されるときに呼ばれる</example>
    private void OnDestroy()
    {
        if (_runtimeSkybox == null) return;

        if (RenderSettings.skybox == _runtimeSkybox) RenderSettings.skybox = _originalSkybox;
        Destroy(_runtimeSkybox);
    }
}

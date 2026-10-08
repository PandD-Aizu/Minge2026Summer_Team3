using _Project.Scripts.Core;
using UnityEngine;

public class TimeOfDayView : MonoBehaviour
{
    [SerializeField] private Light _directionalLight;
    [SerializeField] private Color _dayLightColor = Color.cornsilk;
    [SerializeField] private Color _nightLightColor = Color.midnightBlue;
    [SerializeField] private Renderer _sunlightRenderer;
    [SerializeField, Min(0f)] private float _transitionDuration = 2f;

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

    /// <summary>初回は時間帯を即時反映し、以降は太陽色と光の筋を滑らかに切り替える</summary>
    /// <param name="timeOfDay">適用する昼夜の状態</param>
    /// <example>ApplyTimeToSky(TimeOfDay.Night)</example>
    public void ApplyTimeToSky(TimeOfDay timeOfDay)
    {
        bool night = timeOfDay == TimeOfDay.Night;
        Color targetColor = night ? _nightLightColor : _dayLightColor;
        float targetVisibility = night ? 0f : 1f;

        // 同じ時間帯の通知では、進行中のフェードを最初からやり直さない
        if (_initialized && _targetLightColor == targetColor && _targetVisibility == targetVisibility)
            return;

        _targetLightColor = targetColor;
        _targetVisibility = targetVisibility;

        // 夜のセーブから開始するときは、昼の光を一瞬表示しない
        if (!_initialized || !isActiveAndEnabled || _transitionDuration <= 0f)
        {
            _initialized = true;
            _isTransitioning = false;
            ApplyLighting(_targetLightColor, _targetVisibility);
            return;
        }

        // 途中で昼夜が反転しても、その時点の明るさから連続して補間する
        _startLightColor = _directionalLight.color;
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

        if (progress >= 1f) _isTransitioning = false;
    }

    /// <summary>太陽色と光線の透明度を反映し、透明度が0になったら描画を止める</summary>
    /// <param name="lightColor">現在の太陽色</param>
    /// <param name="visibility">光線の表示率、0が非表示で1が通常の明るさ</param>
    /// <example>ApplyLighting(_nightLightColor, 0f)</example>
    private void ApplyLighting(Color lightColor, float visibility)
    {
        _directionalLight.color = lightColor;
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
        if (_directionalLight != null) ApplyLighting(_targetLightColor, _targetVisibility);
    }
}

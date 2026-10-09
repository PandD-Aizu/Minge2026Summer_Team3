using UnityEngine;
using UnityEngine.Rendering;

namespace Minge2026.Weather
{
    /// <summary>昼夜の色を維持したまま雨雲による減光と色調補正を重ねる</summary>
    public sealed class WeatherAtmosphere : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Renderer[] _shafts;
        [SerializeField] private Volume _stormVolume;
        private float _sunIntensity;
        private float[] _shaftIntensities;
        private MaterialPropertyBlock _properties;
        private float _intensity;
        private bool _initialized;
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        /// <summary>配置時に太陽、光線、専用Volumeを関連付ける</summary>
        /// <param name="sun">昼夜システムが色を制御する太陽</param>
        /// <param name="shafts">雨で弱める光線Renderer</param>
        /// <param name="stormVolume">雨色のみを上書きするVolume</param>
        /// <example>atmosphere.Configure(sun, shafts, volume)</example>
        public void Configure(Light sun, Renderer[] shafts, Volume stormVolume)
        {
            _sun = sun;
            _shafts = shafts;
            _stormVolume = stormVolume;
        }

        /// <summary>天候のフェード値を受け取る</summary>
        /// <param name="intensity">0が晴天、1が雨天</param>
        /// <example>atmosphere.SetIntensity(cycle.RainIntensity)</example>
        public void SetIntensity(float intensity) => _intensity = Mathf.Clamp01(intensity);

        /// <summary>他の昼夜処理の後で明るさだけを乗算する</summary>
        /// <example>太陽の朝夕の色と光線の夜間非表示はそのまま残る</example>
        private void LateUpdate()
        {
            if (!_initialized)
            {
                _sunIntensity = _sun != null ? _sun.intensity : 0f;
                _properties = new MaterialPropertyBlock();
                _shafts ??= System.Array.Empty<Renderer>();
                _shaftIntensities = new float[_shafts.Length];
                for (int i = 0; i < _shafts.Length; i++)
                    if (_shafts[i] != null && _shafts[i].sharedMaterial != null)
                        _shaftIntensities[i] = _shafts[i].sharedMaterial.GetFloat(IntensityId);
                _initialized = true;
            }

            Apply(_intensity);
        }

        /// <summary>保存した基準値から減光し、毎フレームの乗算累積を防ぐ</summary>
        /// <param name="intensity">雨雲の強さ</param>
        /// <example>Apply(0f)で元の明るさへ戻す</example>
        private void Apply(float intensity)
        {
            if (_sun != null) _sun.intensity = _sunIntensity * Mathf.Lerp(1f, 0.58f, intensity);
            if (_stormVolume != null) _stormVolume.weight = intensity;

            // TimeOfDayViewが使う_SunlightFadeを上書きせずに雨雲の影響を加える
            for (int i = 0; i < _shafts.Length; i++)
            {
                if (_shafts[i] == null) continue;
                _shafts[i].GetPropertyBlock(_properties);
                _properties.SetFloat(IntensityId, _shaftIntensities[i] * Mathf.Lerp(1f, 0.08f, intensity));
                _shafts[i].SetPropertyBlock(_properties);
            }
        }

        /// <summary>無効化やシーン終了で基準の照明へ戻す</summary>
        /// <example>天候オブジェクトを無効にしても雨天の減光が残らない</example>
        private void OnDisable()
        {
            if (_initialized) Apply(0f);
        }
    }
}

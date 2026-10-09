using System;
using UnityEngine;

namespace Minge2026.Weather
{
    public enum WeatherPhase { Clear, Raining, AfterRain }

    /// <summary>描画や音から独立して天候の抽選、濡れ、乾燥を進める</summary>
    public sealed class WeatherCycle
    {
        private readonly WeatherSettings _settings;
        private readonly System.Random _random;
        private float _checkRemaining;
        private float _rainRemaining;
        private float _rainElapsed;
        private float _afterRainElapsed;
        private float _wetnessAtStop;

        public WeatherPhase Phase { get; private set; }
        public float RainIntensity { get; private set; }
        public float Wetness { get; private set; }
        public float Puddles { get; private set; }

        /// <summary>設定と独立した乱数列を使って晴天から開始する</summary>
        /// <param name="settings">共有する天候設定</param>
        /// <param name="random">ゲーム全体の乱数を消費しない抽選元</param>
        /// <example>new WeatherCycle(settings, new System.Random())</example>
        public WeatherCycle(WeatherSettings settings, System.Random random)
        {
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _checkRemaining = Mathf.Max(1f, settings.FirstCheckDelay);
        }

        /// <summary>ゲーム内の経過秒数だけ天候を進め、ポーズ中は状態を維持する</summary>
        /// <param name="deltaTime">0以上の経過秒数</param>
        /// <example>cycle.Tick(Time.deltaTime)</example>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;

            // 雨量の変化をフェードし、開始と終了で音や粒子が跳ねないようにする
            RainIntensity = Mathf.MoveTowards(RainIntensity, Phase == WeatherPhase.Raining ? 1f : 0f,
                deltaTime / Mathf.Max(0.1f, _settings.RainFadeSeconds));

            switch (Phase)
            {
                case WeatherPhase.Clear:
                    _checkRemaining -= deltaTime;
                    if (_checkRemaining > 0f) break;
                    _checkRemaining = Mathf.Max(1f, _settings.RainCheckInterval);
                    if (_random.NextDouble() < Mathf.Clamp01(_settings.RainProbability)) StartRain();
                    break;

                case WeatherPhase.Raining:
                    float previousElapsed = _rainElapsed;
                    _rainElapsed += deltaTime;
                    _rainRemaining -= deltaTime;
                    float wetSeconds = Mathf.Max(0f, _rainElapsed - Mathf.Max(0f, _settings.WettingDelay)) -
                                       Mathf.Max(0f, previousElapsed - Mathf.Max(0f, _settings.WettingDelay));
                    Wetness = Mathf.Clamp01(Wetness + wetSeconds / Mathf.Max(0.1f, _settings.WettingSeconds));
                    Puddles = Mathf.MoveTowards(Puddles, 0f, deltaTime / Mathf.Max(0.1f, _settings.RainFadeSeconds));
                    if (_rainRemaining <= 0f) StopRain();
                    break;

                case WeatherPhase.AfterRain:
                    _afterRainElapsed += deltaTime;
                    // 最後の雨粒が消えた後に水たまりを作り、それから地面を乾かす
                    float formation = Mathf.Max(0.1f, _settings.PuddleFormationSeconds);
                    float sinceFade = Mathf.Max(0f, _afterRainElapsed - Mathf.Max(0.1f, _settings.RainFadeSeconds));
                    float drying = Mathf.Clamp01((sinceFade - formation) / Mathf.Max(1f, _settings.DryingSeconds));
                    Wetness = _wetnessAtStop * (1f - drying);
                    Puddles = Wetness * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sinceFade / formation));
                    if (drying < 1f) break;
                    Phase = WeatherPhase.Clear;
                    _checkRemaining = Mathf.Max(1f, _settings.RainCheckInterval);
                    break;
            }
        }

        /// <summary>継続時間を抽選して降雨を開始し、既に雨なら継続する</summary>
        /// <example>デバッグボタンからcycle.StartRain()を呼ぶ</example>
        public void StartRain()
        {
            if (Phase == WeatherPhase.Raining) return;
            float minimum = Mathf.Max(1f, _settings.RainDuration.x);
            float maximum = Mathf.Max(minimum, _settings.RainDuration.y);
            _rainRemaining = Mathf.Lerp(minimum, maximum, (float)_random.NextDouble());
            _rainElapsed = 0f;
            Phase = WeatherPhase.Raining;
        }

        /// <summary>雨を止め、その時点の濡れ具合を雨上がりへ引き継ぐ</summary>
        /// <example>デバッグボタンからcycle.StopRain()を呼ぶ</example>
        public void StopRain()
        {
            if (Phase != WeatherPhase.Raining) return;
            _afterRainElapsed = 0f;
            _wetnessAtStop = Wetness;
            Phase = WeatherPhase.AfterRain;
        }
    }
}

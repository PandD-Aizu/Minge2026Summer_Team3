using System;
using UnityEngine;

namespace MiniGame
{
    /// <summary>表示や入力デバイスに依存せず、固定刻みで釣りの物理と成績を計算する</summary>
    public sealed class GaugeMiniGameModel
    {
        private const float Step = 1f / 120f;
        private readonly GaugeMiniGameSettings _settings;
        private readonly System.Random _random;
        private readonly float _timeLimit;
        private float _accumulator, _elapsed, _barVelocity, _fishVelocity, _target, _changeTime, _dartTime;
        private int _elapsedSteps;
        public float BarPosition { get; private set; }
        public float FishPosition { get; private set; }
        public float Progress { get; private set; }
        /// <summary>開始からの制限時間消費率、0で開始、1で時間切れ</summary>
        public float TimeProgress => Mathf.Clamp01(_elapsed / _timeLimit);
        public float BarSize => Mathf.Clamp(_settings.BarSize, 0.08f, 0.8f);
        public bool IsInside => Mathf.Abs(FishPosition - BarPosition) <= BarSize * 0.5f;
        public bool IsFinished { get; private set; }
        public MiniGameResult Result => Progress >= 1f && TimeProgress < 1f
            ? (TimeProgress < 0.7f ? MiniGameResult.Great : MiniGameResult.Good) : MiniGameResult.Miss;

        /// <summary>魚とバーを重ねて開始し、乱数の種を固定できるようにする</summary>
        /// <param name="settings">魚ごとの読み取り専用設定</param>
        /// <param name="random">動きに使用する乱数</param>
        /// <example>new GaugeMiniGameModel(settings, new System.Random(seed))</example>
        public GaugeMiniGameModel(GaugeMiniGameSettings settings, System.Random random)
        {
            _settings = settings;
            _random = random;
            _timeLimit = Mathf.Max(0.1f, settings.TimeLimitSeconds);
            FishPosition = Mathf.Clamp(0.25f, settings.HeightRange.x, settings.HeightRange.y);
            BarPosition = Mathf.Clamp(FishPosition, BarSize * 0.5f, 1f - BarSize * 0.5f);
            Progress = Mathf.Clamp(settings.InitialProgress, 0.05f, 0.95f);
            PickTarget();
        }

        /// <summary>経過時間を固定刻みに分割し、バー、魚、ゲージを更新する</summary>
        /// <param name="deltaTime">ポーズを反映した秒数</param>
        /// <param name="held">上昇操作の押下状態</param>
        /// <example>model.Tick(Time.deltaTime, gate.Held)</example>
        public void Tick(float deltaTime, bool held)
        {
            if (IsFinished || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            _accumulator += deltaTime;
            while (_accumulator + 0.000001f >= Step && !IsFinished)
            {
                _accumulator -= Step;
                UpdateStep(held);
            }
        }

        /// <summary>1刻み分の移動と捕獲を更新する</summary>
        /// <param name="held">上昇操作中ならtrue</param>
        /// <example>Tickの固定刻みループから呼ぶ</example>
        private void UpdateStep(bool held)
        {
            // 時間は固定刻みの回数から求め、累積加算による70%境界のずれを避ける
            _elapsed = ++_elapsedSteps * Step;
            if (_elapsed >= _timeLimit)
            {
                // 同じ刻みで捕獲も満タンになる場合は時間切れを優先する
                IsFinished = true;
                return;
            }
            _barVelocity = Mathf.Clamp(_barVelocity + (held ? _settings.LiftAcceleration : -_settings.Gravity) * Step,
                -_settings.MaxBarSpeed, _settings.MaxBarSpeed);
            float next = BarPosition + _barVelocity * Step;
            BarPosition = Mathf.Clamp(next, BarSize * 0.5f, 1f - BarSize * 0.5f);
            if (next != BarPosition) _barVelocity *= -Mathf.Clamp01(_settings.Bounce);

            _changeTime -= Step;
            _dartTime -= Step;
            if (_changeTime <= 0) PickTarget();
            float speed = _settings.FishSpeed * (_dartTime > 0 ? _settings.DartMultiplier : 1f);
            float desired = Mathf.Clamp((_target - FishPosition) * 4f, -speed, speed);
            _fishVelocity = Mathf.MoveTowards(_fishVelocity, desired, _settings.FishAcceleration * Step);
            FishPosition = Mathf.Clamp(FishPosition + _fishVelocity * Step, _settings.HeightRange.x, _settings.HeightRange.y);

            // 開始猶予中は捕獲ゲージだけを止め、赤い時間ゲージは進める
            if (_elapsed < _settings.GraceSeconds) return;
            Progress = Mathf.Clamp01(Progress + (IsInside ? _settings.GainPerSecond : -_settings.LossPerSecond) * Step);
            IsFinished = Progress <= 0f || Progress >= 1f;
        }

        /// <summary>魚の性格に応じて次の高さと移動時間を決める</summary>
        /// <example>初期化時と方向転換時に呼ぶ</example>
        private void PickTarget()
        {
            float t = (float)_random.NextDouble();
            switch (_settings.Motion)
            {
                case FishMotion.Calm: t = Mathf.Clamp01(Mathf.InverseLerp(_settings.HeightRange.x, _settings.HeightRange.y, FishPosition) + (t - 0.5f) * 0.4f); break;
                case FishMotion.Smooth: t = _target < (_settings.HeightRange.x + _settings.HeightRange.y) * 0.5f ? 0.9f : 0.1f; break;
                case FishMotion.Top: t = Mathf.Sqrt(t); break;
                case FishMotion.Bottom: t *= t; break;
                case FishMotion.Dart: _dartTime = _settings.DartSeconds; break;
            }
            _target = Mathf.Lerp(_settings.HeightRange.x, _settings.HeightRange.y, t);
            _changeTime = Mathf.Max(0.05f, Mathf.Lerp(_settings.ChangeInterval.x, _settings.ChangeInterval.y, (float)_random.NextDouble()));
        }
    }
}

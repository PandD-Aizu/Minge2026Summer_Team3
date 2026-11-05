using _Project.Scripts.Data.Enum;
using UnityEngine;

namespace MiniGame
{
    public enum FishMotion { Calm, Dart, Smooth, Top, Bottom }

    /// <summary>魚ごとの動きと捕獲しやすさを定義する 読み取り専用で実行する</summary>
    [CreateAssetMenu(menuName = "Data/MiniGame/Gauge Settings")]
    public sealed class GaugeMiniGameSettings : MiniGameSettings
    {
        public override MiniGameType GameType => MiniGameType.Gauge;
        [Header("捕獲 高さとゲージは0から1")]
        [Range(0.08f, 0.8f)] public float BarSize = 0.28f;
        [Range(0.05f, 0.95f)] public float InitialProgress = 0.35f;
        [Min(0.01f)] public float GainPerSecond = 0.2f;
        [Min(0.01f)] public float LossPerSecond = 0.14f;
        [Min(0f)] public float GraceSeconds = 0.7f;
        [Header("制限時間")]
        [Min(0.1f), Tooltip("開始から赤ゲージが満タンになるまでの秒数、開始猶予も含む")]
        public float TimeLimitSeconds = 15f;
        [Header("魚の動き")]
        public FishMotion Motion;
        public Vector2 HeightRange = new(0.1f, 0.85f);
        [Min(0.01f)] public float FishSpeed = 0.4f;
        [Min(0.01f)] public float FishAcceleration = 1.8f;
        public Vector2 ChangeInterval = new(0.7f, 1.6f);
        [Min(1f)] public float DartMultiplier = 2f;
        [Min(0.01f)] public float DartSeconds = 0.25f;
        [Header("バーの操作感")]
        [Min(0.01f)] public float LiftAcceleration = 2.8f;
        [Min(0.01f)] public float Gravity = 1.8f;
        [Min(0.01f)] public float MaxBarSpeed = 1.1f;
        [Range(0f, 1f)] public float Bounce = 0.2f;

        /// <summary>Inspectorで入力した範囲と速度を安全な値へ補正する</summary>
        /// <example>設定を編集した際にUnityが呼ぶ</example>
        private void OnValidate()
        {
            BarSize = Mathf.Clamp(BarSize, 0.08f, 0.8f);
            InitialProgress = Mathf.Clamp(InitialProgress, 0.05f, 0.95f);
            GainPerSecond = Mathf.Max(0.01f, GainPerSecond);
            LossPerSecond = Mathf.Max(0.01f, LossPerSecond);
            GraceSeconds = Mathf.Max(0, GraceSeconds);
            TimeLimitSeconds = Mathf.Max(0.1f, TimeLimitSeconds);
            HeightRange = new Vector2(Mathf.Clamp01(Mathf.Min(HeightRange.x, HeightRange.y)),
                Mathf.Clamp01(Mathf.Max(HeightRange.x, HeightRange.y)));
            ChangeInterval = new Vector2(Mathf.Max(0.05f, Mathf.Min(ChangeInterval.x, ChangeInterval.y)),
                Mathf.Max(0.05f, Mathf.Max(ChangeInterval.x, ChangeInterval.y)));
            FishSpeed = Mathf.Max(0.01f, FishSpeed);
            FishAcceleration = Mathf.Max(0.01f, FishAcceleration);
            LiftAcceleration = Mathf.Max(0.01f, LiftAcceleration);
            Gravity = Mathf.Max(0.01f, Gravity);
            MaxBarSpeed = Mathf.Max(0.01f, MaxBarSpeed);
            DartMultiplier = Mathf.Max(1, DartMultiplier);
            DartSeconds = Mathf.Max(0.01f, DartSeconds);
            Bounce = Mathf.Clamp01(Bounce);
        }
    }
}

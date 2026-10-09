using UnityEngine;

namespace Minge2026.Weather
{
    /// <summary>天候の抽選頻度と演出時間をステージ間で共有する設定</summary>
    [CreateAssetMenu(menuName = "Minge/Weather Settings")]
    public sealed class WeatherSettings : ScriptableObject
    {
        [Header("晴天と雨の抽選")]
        [Min(1f)] public float FirstCheckDelay = 20f;
        [Min(1f)] public float RainCheckInterval = 30f;
        [Range(0f, 1f)] public float RainProbability = 0.35f;
        public Vector2 RainDuration = new Vector2(40f, 75f);
        [Min(0.1f)] public float RainFadeSeconds = 3f;

        [Header("地面と水たまり")]
        [Min(0f)] public float WettingDelay = 8f;
        [Min(0.1f)] public float WettingSeconds = 20f;
        [Min(0.1f)] public float PuddleFormationSeconds = 6f;
        [Min(1f)] public float DryingSeconds = 90f;

        [Header("雨天中の落雷")]
        [Min(1f)] public float LightningCheckInterval = 8f;
        [Range(0f, 1f)] public float LightningProbability = 0.08f;

        [Header("FMOD")]
        public string RainEventPath = "event:/Environment/Rain";
    }
}

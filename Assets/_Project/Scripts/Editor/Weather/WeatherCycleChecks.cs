using System;
using Minge2026.Weather;
using UnityEditor;
using UnityEngine;

namespace Minge2026.Editor.Weather
{
    /// <summary>乱数の境界値と天候の時間順序をEditorから再現可能に検証する</summary>
    public static class WeatherCycleChecks
    {
        /// <summary>描画やBankを使わず、確率、濡れの待ち時間、雨後の乾燥を検証する</summary>
        /// <returns>全条件を満たした場合の結果文字列</returns>
        /// <example>Tools/Weather/Validate Weather Cycleから実行する</example>
        [MenuItem("Tools/Weather/Validate Weather Cycle")]
        public static string Run()
        {
            var settings = ScriptableObject.CreateInstance<WeatherSettings>();
            try
            {
                // 0パーセントと100パーセントが境界で逆転しないことを確認する
                settings.FirstCheckDelay = 1f;
                settings.RainCheckInterval = 1f;
                settings.RainProbability = 0f;
                var clear = new WeatherCycle(settings, new System.Random(10));
                for (int i = 0; i < 100; i++) clear.Tick(1f);
                Check(clear.Phase == WeatherPhase.Clear, "確率0でも雨になった");
                settings.RainProbability = 1f;
                clear.Tick(1f);
                Check(clear.Phase == WeatherPhase.Raining, "確率1で雨にならなかった");

                // 遅延を跨ぐフレームでも実際に濡れる秒数だけを加算する
                settings.RainDuration = new Vector2(40f, 40f);
                settings.RainProbability = 0f;
                var cycle = new WeatherCycle(settings, new System.Random(20));
                cycle.StartRain();
                cycle.Tick(7.5f);
                Check(cycle.Wetness == 0f, "待ち時間の前に地面が濡れた");
                cycle.Tick(1f);
                Check(Mathf.Abs(cycle.Wetness - 0.025f) < 0.0001f, "遅延境界の濡れ時間が不正");
                cycle.Tick(19.5f);
                Check(cycle.Wetness == 1f && cycle.Puddles == 0f, "雨中の濡れと水たまりの順序が不正");
                cycle.Tick(0f);
                Check(cycle.Wetness == 1f, "ポーズ中に状態が進んだ");
                cycle.Tick(12f);
                Check(cycle.Phase == WeatherPhase.AfterRain, "継続時間後も雨が止まらない");
                cycle.Tick(settings.RainFadeSeconds);
                Check(cycle.RainIntensity == 0f && cycle.Puddles == 0f, "残雨と水たまりが重なった");
                cycle.Tick(settings.PuddleFormationSeconds);
                Check(cycle.Puddles == 1f, "雨後に水たまりができない");
                cycle.Tick(settings.DryingSeconds);
                Check(cycle.Phase == WeatherPhase.Clear && cycle.Puddles == 0f && cycle.Wetness == 0f,
                    "乾燥後に水たまりまたは濡れが残る");

                // 地面が濡れる前に中断した小雨では水たまりを作らない
                cycle.StartRain();
                cycle.Tick(2f);
                cycle.StopRain();
                cycle.Tick(10f);
                Check(cycle.Puddles == 0f, "短時間の雨で水たまりができた");
                return "PASS: 確率0/1、濡れ遅延、境界時間、雨中の水たまり抑止、ポーズ、自動終了、雨後形成、乾燥、小雨";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        /// <summary>条件を満たさなければ検証を失敗させる</summary>
        /// <param name="condition">検証する条件</param>
        /// <param name="message">不成立時に表示する原因</param>
        /// <example>Check(cycle.Wetness == 0f, "濡れが残る")</example>
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;

namespace MiniGame
{
    /// <summary>設定の個数とは独立に、種類の重みで抽選する</summary>
    public sealed class MiniGameSelector
    {
        private readonly MiniGameRegistry _registry;

        /// <summary>実行可能な種類を判別する登録一覧を受け取る</summary>
        /// <param name="registry">DIの登録一覧</param>
        /// <example>VContainerから生成する</example>
        public MiniGameSelector(MiniGameRegistry registry) => _registry = registry;

        /// <summary>魚の候補を検証して種類を抽選し、その中の設定を選ぶ</summary>
        /// <param name="fish">対象魚</param>
        /// <param name="random">0以上1未満の値を返す乱数源</param>
        /// <returns>実行可能な設定</returns>
        /// <example>Select(fish, () => UnityEngine.Random.value)</example>
        public MiniGameSettings Select(FishDefinition fish, Func<float> random)
        {
            if (fish == null) throw new ArgumentNullException(nameof(fish));
            var options = fish.MiniGameOptions;
            // 未移行の魚も旧設定を種類単位にまとめて使用できる
            if (options == null || options.Length == 0)
                options = (fish.MiniGameSettings ?? Array.Empty<MiniGameSettings>())
                    .Where(s => s != null).GroupBy(s => s.GameType)
                    .Select(g => new MiniGameOption { Type = g.Key, Settings = g.Distinct().ToArray() }).ToArray();

            var seen = new HashSet<MiniGameType>();
            var candidates = new List<(MiniGameOption option, MiniGameSettings[] settings)>();
            double total = 0;
            foreach (var option in options)
            {
                if (option == null || !seen.Add(option.Type))
                    throw new InvalidOperationException($"{fish.name}: ミニゲームの種類が空または重複");
                if (float.IsNaN(option.Weight) || float.IsInfinity(option.Weight) || option.Weight < 0)
                    throw new InvalidOperationException($"{fish.name}: 抽選重みが不正");
                if (option.Weight == 0) continue;
                var settings = option.Settings?.Where(s => s != null && s.GameType == option.Type
                    && _registry.TryGet(s, out _)).Distinct().ToArray();
                if (settings == null || settings.Length == 0)
                    throw new InvalidOperationException($"{fish.name}: {option.Type} の実行可能な設定がない");
                candidates.Add((option, settings));
                total += option.Weight;
            }
            if (candidates.Count == 0) throw new InvalidOperationException($"{fish.name}: 抽選候補がない");

            double roll = UnityEngine.Mathf.Clamp01(random()) * total;
            var selected = candidates[candidates.Count - 1];
            foreach (var candidate in candidates)
            {
                roll -= candidate.option.Weight;
                if (roll < 0) { selected = candidate; break; }
            }
            int index = Math.Min((int)(UnityEngine.Mathf.Clamp01(random()) * selected.settings.Length), selected.settings.Length - 1);
            return selected.settings[index];
        }
    }
}

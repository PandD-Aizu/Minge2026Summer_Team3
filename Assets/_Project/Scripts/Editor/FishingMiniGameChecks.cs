using System;
using System.Linq;
using System.Threading;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;
using MiniGame;
using UnityEditor;
using UnityEngine;

/// <summary>抽選、入力境界、魚の動きとゲージを描画なしで再現検証する</summary>
public static class FishingMiniGameChecks
{
    /// <summary>固定乱数と時間刻みで釣りの主要な境界条件を検証する</summary>
    /// <returns>すべて成功した場合の検証結果</returns>
    /// <example>Tools/Fishing/Validate Mini Gamesから実行する</example>
    [MenuItem("Tools/Fishing/Validate Mini Games")]
    public static string Run()
    {
        var fish = ScriptableObject.CreateInstance<FishDefinition>();
        var rotation = ScriptableObject.CreateInstance<RotationMiniGameSettings>();
        var gauge = ScriptableObject.CreateInstance<GaugeMiniGameSettings>();
        try
        {
            var registry = new MiniGameRegistry(new IMiniGameController[] { new Stub(MiniGameType.Rotation), new Stub(MiniGameType.Gauge) });
            var selector = new MiniGameSelector(registry);
            var data = new SerializedObject(fish);
            var legacy = data.FindProperty("_miniGameSettings");
            legacy.arraySize = 5;
            for (int i = 0; i < 5; i++) legacy.GetArrayElementAtIndex(i).objectReferenceValue = i < 4 ? rotation : gauge;
            data.ApplyModifiedPropertiesWithoutUndo();
            Check(selector.Select(fish, () => 0.499f) == rotation, "目押し4個とゲージ1個でも前半は目押し");
            Check(selector.Select(fish, () => 0.5f) == gauge, "設定数に依存せず50%の境界でゲージになる");
            Check(selector.Select(fish, () => 1f) == gauge, "乱数の上限で配列外参照しない");

            var options = data.FindProperty("_miniGameOptions");
            options.arraySize = 2;
            SetOption(options.GetArrayElementAtIndex(0), rotation, 3);
            SetOption(options.GetArrayElementAtIndex(1), gauge, 1);
            data.ApplyModifiedPropertiesWithoutUndo();
            Check(selector.Select(fish, () => 0.749f) == rotation && selector.Select(fish, () => 0.75f) == gauge, "重み3対1の境界");
            options.GetArrayElementAtIndex(0).FindPropertyRelative("Weight").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            Check(selector.Select(fish, () => 0f) == gauge, "重み0のゲームが選ばれない");
            options.GetArrayElementAtIndex(1).FindPropertyRelative("Settings").arraySize = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            ExpectFailure(() => selector.Select(fish, () => 0), "空の設定を開始しない");
            ExpectFailure(() => new MiniGameRegistry(new[] { new Stub(MiniGameType.Gauge), new Stub(MiniGameType.Gauge) }), "重複登録を検出する");

            var gate = new MiniGameInputGate();
            Check(!gate.Sample(true) && !gate.Held && !gate.Sample(true), "前段階の長押しを捨てる");
            Check(!gate.Sample(false) && gate.Sample(true) && gate.Held, "解放後の新しい押下を受け付ける");
            Check(!gate.Sample(true), "押しっぱなしで再入力しない");

            // 同じ時間を30fpsと144fpsで進めても物理と乱数の状態は一致する
            gauge.GainPerSecond = gauge.LossPerSecond = 0.01f;
            var slow = new GaugeMiniGameModel(gauge, new System.Random(7));
            var fast = new GaugeMiniGameModel(gauge, new System.Random(7));
            for (int i = 0; i < 60; i++) slow.Tick(1f / 30f, true);
            for (int i = 0; i < 288; i++) fast.Tick(1f / 144f, true);
            Check(Mathf.Abs(slow.FishPosition - fast.FishPosition) < 0.0001f && Mathf.Abs(slow.Progress - fast.Progress) < 0.0001f, "fpsによる進行差がない");
            Check(Mathf.Abs(slow.TimeProgress - fast.TimeProgress) < 0.0001f, "fpsによる赤ゲージの進行差がない");
            Check(slow.BarPosition <= 1f - slow.BarSize * 0.5f + 0.000001f, "バーが上端からはみ出さない");
            float before = slow.Progress;
            float timeBefore = slow.TimeProgress;
            slow.Tick(0, false);
            Check(slow.Progress == before && slow.TimeProgress == timeBefore, "ポーズ中は両ゲージとも進まない");

            gauge.HeightRange = new Vector2(0.15f, 0.15f);
            gauge.BarSize = 0.3f; gauge.GainPerSecond = 1; gauge.GraceSeconds = 0;
            var success = new GaugeMiniGameModel(gauge, new System.Random(1));
            success.Tick(1, false);
            Check(success.IsFinished && success.Result == MiniGameResult.Great && success.Progress == 1, "赤70%未満で捕獲するとGreat");
            gauge.HeightRange = new Vector2(0.9f, 0.9f);
            gauge.LossPerSecond = 1; gauge.GainPerSecond = 0.01f;
            var miss = new GaugeMiniGameModel(gauge, new System.Random(1));
            miss.Tick(3, false);
            Check(miss.IsFinished && miss.Result == MiniGameResult.Miss && miss.Progress == 0, "魚を外すとMiss");
            miss.Tick(2, true);
            Check(miss.Progress == 0, "終了後の更新で結果を変えない");

            // 捕獲する刻みを開始猶予で指定し、70%と時間切れの境界を再現する
            gauge.TimeLimitSeconds = 1f;
            gauge.HeightRange = new Vector2(0.15f, 0.15f);
            gauge.GainPerSecond = 120f;
            foreach (float captureTime in new[] { 0.69f, 0.7f, 0.71f, 1f })
            {
                gauge.GraceSeconds = captureTime;
                var timed = new GaugeMiniGameModel(gauge, new System.Random(1));
                timed.Tick(2f, false);
                var expected = captureTime < 0.7f ? MiniGameResult.Great
                    : captureTime < 1f ? MiniGameResult.Good : MiniGameResult.Miss;
                Check(timed.IsFinished && timed.Result == expected, $"赤{captureTime:P0}での捕獲判定は{expected}");
                if (captureTime == 1f)
                    Check(timed.TimeProgress == 1f && timed.Progress < 1f, "時間切れは同時の捕獲より優先する");
                float stoppedTime = timed.TimeProgress;
                timed.Tick(2f, true);
                Check(timed.TimeProgress == stoppedTime && timed.Result == expected, "終了後は時間と成績を固定する");
            }

            // すべての魚の設定と種類が実際に解決できることを検証する
            foreach (string guid in AssetDatabase.FindAssets("t:FishDefinition", new[] { "Assets/_Project/SO/Item/Fish" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<FishDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                Check(asset.MiniGameOptions?.Length >= 2, $"{asset.name}: 両方のゲームを登録する");
                selector.Select(asset, () => 0);
                selector.Select(asset, () => 0.999f);
            }
            return "PASS: 抽選50%/重み/空設定/重複登録/入力持ち越し/30・144fps/ポーズ/上下端/Great/Good/Miss/赤70%境界/時間切れ/全魚設定";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fish);
            UnityEngine.Object.DestroyImmediate(rotation);
            UnityEngine.Object.DestroyImmediate(gauge);
        }
    }

    /// <summary>検証用の抽選設定を保存する</summary>
    /// <param name="option">配列要素</param>
    /// <param name="settings">ゲーム設定</param>
    /// <param name="weight">抽選重み</param>
    /// <example>SetOption(element, gauge, 1)</example>
    private static void SetOption(SerializedProperty option, MiniGameSettings settings, float weight)
    {
        option.FindPropertyRelative("Type").intValue = (int)settings.GameType;
        option.FindPropertyRelative("Weight").floatValue = weight;
        var list = option.FindPropertyRelative("Settings");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = settings;
    }

    /// <summary>不正設定が明示的に拒否されることを確認する</summary>
    /// <param name="action">不正設定を渡す操作</param>
    /// <param name="message">期待する条件</param>
    /// <example>ExpectFailure(() => selector.Select(fish, random), "空設定")</example>
    private static void ExpectFailure(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        catch (ArgumentException) { return; }
        throw new Exception(message);
    }

    /// <summary>条件不成立なら検証を停止する</summary>
    /// <param name="condition">検証条件</param>
    /// <param name="message">条件の説明</param>
    /// <example>Check(model.Progress == 0, "失敗")</example>
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }

    private sealed class Stub : IMiniGameController
    {
        public MiniGameType GameType { get; }
        /// <summary>検証で対応させる種類を指定する</summary>
        /// <param name="type">登録する種類</param>
        /// <example>new Stub(MiniGameType.Gauge)</example>
        public Stub(MiniGameType type) => GameType = type;
        /// <inheritdoc />
        public bool CanPlay(MiniGameSettings settings) => settings.GameType == GameType;
        /// <inheritdoc />
        public UniTask<MiniGameResult> PlayAsync(MiniGameSettings settings, FishDefinition fish, CancellationToken cancellation)
            => throw new NotSupportedException();
    }
}

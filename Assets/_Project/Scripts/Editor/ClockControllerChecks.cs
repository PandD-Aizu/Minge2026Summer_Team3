using System;
using _Project.Scripts.Core;
using Controller;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>時計の停止所有権、チュートリアル段階、実フレームの進行を検証する</summary>
public static class ClockControllerChecks
{
    public static string PlayResult { get; private set; } = "未実行";

    /// <summary>描画や実セーブを使わず、停止の重複と段階変更と明示的な時間更新を検証する</summary>
    /// <returns>全条件を満たした場合の結果文字列</returns>
    /// <example>Tools/Clock/Validate Clock Controllerから実行する</example>
    [MenuItem("Tools/Clock/Validate Clock Controller")]
    public static string Run()
    {
        ValidateOwnership();
        ValidateTutorial();
        return "PASS: 停止の重複、二重解除、解除順序、チュートリアル全段階の停止、出来事による時刻更新、重複通知、完了時再開、破棄時解除";
    }

    /// <summary>停止の解除順序と二重Disposeが他の所有者に影響しないことを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateOwnership()
    {
        var clock = new ClockController();
        float originalTimeScale = Time.timeScale;
        Check(!clock.IsPaused, "初期状態で時計が停止している");

        // 先に得た停止を複数回解除しても、後の停止は維持する
        using IDisposable first = clock.Pause();
        using IDisposable second = clock.Pause();
        Check(clock.IsPaused && Time.timeScale == originalTimeScale, "時計停止がtimeScaleを変更した");
        first.Dispose();
        first.Dispose();
        Check(clock.IsPaused, "二重解除で他の停止要求が消えた");
        second.Dispose();
        Check(!clock.IsPaused, "全要求の解除後も停止している");

        // 別の解除順序と、解除済みの権利が新しい停止を消さないことを確認する
        using IDisposable third = clock.Pause();
        second.Dispose();
        Check(clock.IsPaused, "古い権利が新しい停止を解除した");
        using (clock.Pause()) { }
        Check(clock.IsPaused, "後に得た停止の解除で先の停止が消えた");
        third.Dispose();
        Check(!clock.IsPaused, "再停止から復帰できない");

        // 停止するのは自動進行だけで、イベントによる日付越えは許可する
        using var progress = new GameProgress();
        using (clock.Pause())
        {
            progress.AdvanceGameTimeSeconds(24f * 3600f);
            Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 7,
                "停止中に明示的な時間更新ができない");
        }
    }

    /// <summary>現行チュートリアルを公開APIで一周し、時刻の固定と出来事による更新を検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateTutorial()
    {
        var clock = new ClockController();
        using var progress = new GameProgress();
        using var tutorial = new TutorialController(progress, clock);
        Check(clock.IsPaused, "最初のラジオ案内待ちで時間が進む");

        using (clock.Pause())
        {
            tutorial.NotifyRadioInteracted();
            Check(clock.IsPaused, "釣り開始で他の停止要求を解除した");
        }
        Check(clock.IsPaused && progress.GameSeconds.Value == 7f * 3600f,
            "昼釣り中の時計が7時で固定されていない");
        tutorial.NotifyFishCaught();
        Check(clock.IsPaused && progress.GameHours.CurrentValue == 17
            && progress.CurrentTimeOfDay == TimeOfDay.Evening, "昼釣り完了後の時刻が17時で固定されていない");
        float eveningSeconds = progress.GameSeconds.Value;
        Check(!tutorial.NotifyFishCaught() && progress.GameSeconds.Value == eveningSeconds,
            "昼釣り完了の重複通知で時刻が変わった");
        tutorial.NotifyReturnedToCollection();
        float nightSeconds = progress.GameSeconds.Value;
        Check(clock.IsPaused && nightSeconds == 19f * 3600f
            && progress.CurrentTimeOfDay == TimeOfDay.Night, "昼釣り帰還後の時刻が19時で固定されていない");
        Check(!tutorial.NotifyReturnedToCollection() && progress.GameSeconds.Value == nightSeconds,
            "昼釣り帰還の重複通知で時刻が変わった");
        tutorial.NotifyRadioInteracted();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "交換案内中に時刻が変わった");
        tutorial.NotifyExchangeCompleted();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "交換後の案内待ちで時刻が変わった");
        tutorial.NotifyRadioInteracted();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "夜釣りへの移動中に時刻が変わった");
        tutorial.NotifyEnteredFishingStage();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "初回遭遇中に時刻が変わった");
        tutorial.NotifyUpperBeingSeen();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "夜釣り中に時刻が変わった");
        tutorial.NotifyFishCaught();
        Check(clock.IsPaused && progress.GameSeconds.Value == nightSeconds, "夜釣り後の帰還中に時刻が変わった");
        tutorial.NotifyReturnedToCollection();
        float dawnSeconds = progress.GameSeconds.Value;
        Check(clock.IsPaused && dawnSeconds == 28f * 3600f && progress.CurrentDay.CurrentValue == 2
            && progress.GameHours.CurrentValue == 4 && progress.CurrentTimeOfDay == TimeOfDay.Dawn,
            "夜釣り帰還後の時計・日付・時間帯が翌4時で一致していない");
        Check(!tutorial.NotifyReturnedToCollection() && progress.GameSeconds.Value == dawnSeconds,
            "夜釣り帰還の重複通知で時刻が変わった");

        // 完了時も、会話やショップなど別の所有者による停止を勝手に解除しない
        using (clock.Pause())
        {
            tutorial.NotifyRadioInteracted();
            Check(tutorial.IsCompleted && clock.IsPaused, "完了時に外部の停止要求を解除した");
        }
        Check(tutorial.IsCompleted && !clock.IsPaused, "完了後もチュートリアルの停止が残る");
        Check(!tutorial.NotifyRadioInteracted() && progress.GameSeconds.Value == dawnSeconds,
            "完了の重複通知で時刻が変わった");

        // Root終了時に残る案内用の停止を解放し、他の所有者の停止は保つ
        using (clock.Pause())
        {
            var pending = new TutorialController(progress, clock);
            pending.Dispose();
            Check(clock.IsPaused, "チュートリアル破棄で外部の停止要求を解除した");
        }
        Check(!clock.IsPaused, "チュートリアル破棄後に停止が残る");
    }

    /// <summary>Play中の実フレームで60倍速、停止・再開、timeScaleによるポーズ停止を検証する</summary>
    /// <example>Play中にTools/Clock/Validate Clock In Play Modeから実行し、PlayResultで結果を読む</example>
    [MenuItem("Tools/Clock/Validate Clock In Play Mode")]
    public static void StartPlayChecks()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play Modeで実行する");
        if (PlayResult == "RUNNING") throw new InvalidOperationException("時計の検証は既に実行中");
        PlayResult = "RUNNING";
        ValidatePlayAsync().Forget(exception => PlayResult = "FAIL: " + exception);
    }

    /// <summary>実フレームのdeltaTimeを使って時計を検証し、終了理由にかかわらず元のtimeScaleへ戻す</summary>
    /// <returns>全検証とtimeScale復元まで待つUniTask</returns>
    /// <example>StartPlayChecksから一度だけ呼ぶ</example>
    private static async UniTask ValidatePlayAsync()
    {
        float originalTimeScale = Time.timeScale;
        using var progress = new GameProgress();
        var clock = new ClockController();
        var ticker = new GameClock(progress, clock);
        try
        {
            Time.timeScale = 1f;
            int firstFrame = Time.frameCount;
            await UniTask.NextFrame();
            Check(Time.frameCount > firstFrame && Time.deltaTime > 0f, "Play中のフレームが進んでいない");
            float before = progress.GameSeconds.Value;
            float deltaTime = Time.deltaTime;
            ticker.Tick();
            Check(Mathf.Abs(progress.GameSeconds.Value - before - deltaTime * 60f) < 0.01f,
                "時計がdeltaTimeの60倍で進まない");

            before = progress.GameSeconds.Value;
            using (clock.Pause())
            {
                await UniTask.NextFrame();
                ticker.Tick();
                Check(progress.GameSeconds.Value == before, "停止要求中に時計が進んだ");
            }
            await UniTask.NextFrame();
            ticker.Tick();
            Check(progress.GameSeconds.Value > before, "停止解除後に時計が再開しない");

            await ValidateFrozenTutorialAsync();

            Time.timeScale = 0f;
            await UniTask.NextFrame();
            before = progress.GameSeconds.Value;
            ticker.Tick();
            Check(Time.deltaTime == 0f && progress.GameSeconds.Value == before, "ポーズ中に時計が進んだ");
            PlayResult = "PASS: 実フレーム進行、60倍速、チュートリアル中の時刻固定、完了後再開、timeScale=0で停止";
        }
        finally
        {
            Time.timeScale = originalTimeScale;
        }
    }

    /// <summary>実フレームが進んでも昼釣りの時刻を保ち、完了後に他の停止がなければ再開することを検証する</summary>
    /// <returns>停止中と完了後の複数フレームの検証まで待つUniTask</returns>
    /// <example>ValidatePlayAsyncからtimeScale=1の状態で呼ぶ</example>
    private static async UniTask ValidateFrozenTutorialAsync()
    {
        using var progress = new GameProgress();
        var clock = new ClockController();
        using var tutorial = new TutorialController(progress, clock);
        var ticker = new GameClock(progress, clock);
        tutorial.NotifyRadioInteracted();

        // 操作に時間をかけても、昼釣り中の7時を維持する
        for (int i = 0; i < 5; i++)
        {
            await UniTask.NextFrame();
            ticker.Tick();
            Check(progress.GameSeconds.Value == 7f * 3600f, "昼釣り中に自然進行で時刻が変わった");
        }

        tutorial.NotifyFishCaught();
        tutorial.NotifyReturnedToCollection();
        tutorial.NotifyRadioInteracted();
        tutorial.NotifyExchangeCompleted();
        tutorial.NotifyRadioInteracted();
        tutorial.NotifyEnteredFishingStage();
        tutorial.NotifyUpperBeingSeen();
        tutorial.NotifyFishCaught();
        tutorial.NotifyReturnedToCollection();

        using (clock.Pause())
        {
            tutorial.NotifyRadioInteracted();
            float before = progress.GameSeconds.Value;
            await UniTask.NextFrame();
            ticker.Tick();
            Check(progress.GameSeconds.Value == before, "チュートリアル完了で会話側の停止が無効になった");
        }

        float completedSeconds = progress.GameSeconds.Value;
        await UniTask.NextFrame();
        ticker.Tick();
        Check(progress.GameSeconds.Value > completedSeconds, "チュートリアル完了後に自然進行が再開しない");
    }

    /// <summary>条件を満たさない場合に検証を失敗させる</summary>
    /// <param name="condition">成立すべき条件</param>
    /// <param name="message">失敗時に報告する内容</param>
    /// <example>Check(clock.IsPaused, "停止していない")</example>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

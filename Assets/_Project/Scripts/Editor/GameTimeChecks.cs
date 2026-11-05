using System;
using _Project.Scripts.Core;
using Cysharp.Threading.Tasks;
using R3;
using UnityEditor;

/// <summary>時刻更新APIの境界、通知、失敗時の状態維持を検証する</summary>
public static class GameTimeChecks
{
    public static string SceneResult { get; private set; } = "未実行";

    /// <summary>実セーブやシーンを変更せず、時計の時刻更新を検証する</summary>
    /// <returns>すべての条件を満たした場合の結果文字列</returns>
    /// <example>Tools/Clock/Validate Game Timeから実行する</example>
    [MenuItem("Tools/Clock/Validate Game Time")]
    public static string Run()
    {
        ValidateTargetTimes();
        ValidateBoundaries();
        ValidateInvalidSeconds();
        ValidateTimeOfDaySwitches();
        ValidateNotificationState();
        ValidateDurationAndNextDay();
        ValidateInvalidTargetsAndDisposal();
        return "PASS: 同時間帯の時刻変更、同時刻、翌日指定、経過時間指定、日週境界、不正入力、時間帯切替、通知時の状態一致、破棄後更新の拒否";
    }

    /// <summary>同時間帯の別時刻と、同時刻と、過ぎた指定時刻を検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateTargetTimes()
    {
        using var progress = new GameProgress();
        progress.AdvanceGameTimeHours(8);
        Check(progress.GameSeconds.CurrentValue == 8f * 3600f, "同じ昼の7時から8時へ進まない");
        progress.AdvanceGameTimeHours(12);
        Check(progress.GameSeconds.CurrentValue == 12f * 3600f, "同じ昼の8時から12時へ進まない");

        // 到達済みの同時刻を再指定しても翌日へ進めない
        progress.AdvanceGameTimeHours(12);
        Check(progress.CurrentDay.CurrentValue == 1 && progress.GameHours.CurrentValue == 12,
            "同時刻の指定で日付が変わった");

        progress.AdvanceGameTimeHours(11);
        Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 11,
            "過ぎた時刻を翌日として扱わない");
        progress.AdvanceGameTimeSeconds(30f * 60f);
        progress.AdvanceGameTimeHours(11);
        Check(progress.CurrentDay.CurrentValue == 3 && progress.GameSeconds.CurrentValue == 59f * 3600f,
            "11時30分から11時への指定で時刻を巻き戻した");
    }

    /// <summary>0時の日付更新と7日目から8日目の週更新を検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateBoundaries()
    {
        using var progress = new GameProgress();
        progress.AdvanceGameTimeHours(23);
        progress.AdvanceGameTimeSeconds(3599f);
        Check(progress.CurrentDay.CurrentValue == 1 && progress.GameHours.CurrentValue == 23,
            "0時より前に日付が増えた");
        progress.AdvanceGameTimeSeconds(1f);
        Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 0
            && progress.CurrentTimeOfDay == TimeOfDay.Night, "0時で日付と時計と夜が一致しない");
        progress.AdvanceGameTimeSeconds(5f * 24f * 3600f);
        Check(progress.CurrentDay.CurrentValue == 7 && progress.CurrentWeek.CurrentValue == 1,
            "7日目に週が増えた");
        progress.AdvanceGameTimeSeconds(24f * 3600f);
        Check(progress.CurrentDay.CurrentValue == 8 && progress.CurrentWeek.CurrentValue == 2,
            "8日目で週が増えない");
        progress.AdvanceGameTimeSeconds(3f * 24f * 3600f);
        Check(progress.CurrentDay.CurrentValue == 11 && progress.CurrentWeek.CurrentValue == 2,
            "複数日の早送りで日付が一致しない");
    }

    /// <summary>負数と非有限値と上限超過で状態と通知が変化しないことを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateInvalidSeconds()
    {
        using var progress = new GameProgress();
        float before = progress.GameSeconds.CurrentValue;
        int notifications = 0;
        using var subscription = progress.TimeOfDayChanged.Subscribe(_ => notifications++);
        foreach (float seconds in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity, float.MaxValue })
        {
            bool rejected = false;
            try
            {
                progress.AdvanceGameTimeSeconds(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejected = true;
            }

            Check(rejected && progress.GameSeconds.CurrentValue == before && notifications == 0,
                "不正な経過秒数で時計が更新された: " + seconds);
        }
        progress.AdvanceGameTimeSeconds(0f);
        Check(progress.GameSeconds.CurrentValue == before && notifications == 0, "0秒の更新で状態が変わった");
    }

    /// <summary>既存の時間帯切替APIも時計の時刻を変更し、同じ時間帯を二重通知しないことを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateTimeOfDaySwitches()
    {
        using var progress = new GameProgress();
        int notifications = 0;
        using var subscription = progress.TimeOfDayChanged.Subscribe(_ => notifications++);
        progress.StartEvening();
        Check(progress.GameHours.CurrentValue == 17 && progress.CurrentTimeOfDay == TimeOfDay.Evening,
            "夕方切替で時計が17時にならない");
        progress.StartEvening();
        Check(notifications == 1, "同じ時刻への切替で時間帯を二重通知した");
        progress.StartNight();
        progress.StartDawn();
        Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 4
            && progress.CurrentTimeOfDay == TimeOfDay.Dawn, "明け方切替で時計と日付が一致しない");
        progress.StartDay();
        Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 7
            && notifications == 4, "朝切替で時計や通知回数が一致しない");
    }

    /// <summary>時間帯の通知を受け取る時点で日時と時間帯がすべて更新済みであることを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateNotificationState()
    {
        using var progress = new GameProgress();
        progress.AdvanceGameTimeHours(23);
        bool notified = false;
        bool consistent = false;
        using var subscription = progress.TimeOfDayChanged.Subscribe(period =>
        {
            consistent = period == TimeOfDay.Dawn && progress.CurrentTimeOfDay == period
                && progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 4;
            notified = true;
        });
        progress.AdvanceGameTimeHours(4);
        Check(notified && consistent, "時間帯の通知時点で日時がまだ更新されていない");
    }

    /// <summary>経過時間指定と翌日の明示指定を、時刻指定と混同せず処理できることを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateDurationAndNextDay()
    {
        using var progress = new GameProgress();
        progress.AdvanceGameTimeByHours(1.5f);
        Check(progress.GameSeconds.CurrentValue == 8.5f * 3600f, "1時間30分の早送りになっていない");
        progress.AdvanceGameTimeHours(8);
        Check(progress.CurrentDay.CurrentValue == 2 && progress.GameHours.CurrentValue == 8,
            "過ぎた8時へ巻き戻した");
        progress.AdvanceGameTimeHours(8, true);
        Check(progress.CurrentDay.CurrentValue == 3 && progress.GameHours.CurrentValue == 8,
            "同時刻で翌日を指定しても日付が増えない");
        progress.AdvanceGameTimeHours(23, true);
        Check(progress.CurrentDay.CurrentValue == 4 && progress.GameHours.CurrentValue == 23,
            "未来の時刻でも翌日指定を優先しない");
        progress.AdvanceGameTimeByHours(2f);
        Check(progress.CurrentDay.CurrentValue == 5 && progress.GameHours.CurrentValue == 1,
            "経過時間指定で0時をまたげない");
    }

    /// <summary>無効な時刻と時間帯と経過時間、および破棄後の更新を拒否することを検証する</summary>
    /// <example>Runから呼ぶ</example>
    private static void ValidateInvalidTargetsAndDisposal()
    {
        using var progress = new GameProgress();
        foreach (Action invalidUpdate in new Action[]
        {
            () => progress.AdvanceGameTimeHours(-1),
            () => progress.AdvanceGameTimeHours(24),
            () => progress.AdvanceGameTimeByHours(-1f),
            () => progress.AdvanceGameTimeByHours(float.NaN),
            () => progress.AdvanceGameTimeByHours(float.PositiveInfinity),
            () => progress.AdvanceToTimeOfDay((TimeOfDay)999)
        })
        {
            bool rejected = false;
            try { invalidUpdate(); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && progress.GameSeconds.CurrentValue == 7f * 3600f, "無効な指定先で状態が変わった");
        }

        progress.Dispose();
        progress.Dispose();
        bool disposedUpdateRejected = false;
        try { progress.AdvanceGameTimeSeconds(1f); }
        catch (ObjectDisposedException) { disposedUpdateRejected = true; }
        Check(disposedUpdateRejected, "破棄後も時計を更新できてしまう");
    }

    /// <summary>Play中のCampStageから釣り場へ往復し、実際のRoot時計の同一性と日時の維持を検証する</summary>
    /// <example>Play中にTools/Clock/Validate Scene Round Tripから実行し、SceneResultで結果を読む</example>
    [MenuItem("Tools/Clock/Validate Scene Round Trip")]
    public static void StartSceneRoundTrip()
    {
        if (!UnityEngine.Application.isPlaying
            || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "CampStage")
            throw new InvalidOperationException("Play ModeのCampStageから実行する");
        if (SceneResult == "RUNNING") throw new InvalidOperationException("シーン往復を既に検証中");
        SceneResult = "RUNNING";
        ValidateSceneRoundTripAsync().Forget(exception => SceneResult = "FAIL: " + exception);
    }

    /// <summary>時計を日付と週の境界より先へ進め、実シーン往復後も同じ時計と日時を使うことを検証する</summary>
    /// <returns>CampStageへの帰還と確認まで待つUniTask</returns>
    /// <example>StartSceneRoundTripから呼ぶ シーン保存や進行データの永続化APIはこの検証から呼ばない</example>
    private static async UniTask ValidateSceneRoundTripAsync()
    {
        var root = UnityEngine.Object.FindFirstObjectByType<LifetimeScopes.GameLifetimeScope>();
        var progress = (GameProgress)root.Container.Resolve(typeof(GameProgress));
        var clock = (Controller.ClockController)root.Container.Resolve(typeof(Controller.ClockController));
        var loader = (SceneLoadServices.SceneLoadService)root.Container.Resolve(typeof(SceneLoadServices.SceneLoadService));
        using IDisposable pause = clock.Pause();

        // 複数の日付と週の更新後の23時30分を、シーンをまたいで保持する
        progress.AdvanceGameTimeHours(23);
        progress.AdvanceGameTimeByHours(7f * 24f + 0.5f);
        float expectedSeconds = progress.GameSeconds.CurrentValue;
        int expectedDay = progress.CurrentDay.CurrentValue;
        int expectedWeek = progress.CurrentWeek.CurrentValue;

        // Addressablesの表示名とファイル名の違いに依存せず、本番のSceneReferenceと同じGUIDで指定する
        string fishingGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scenes/FishingStage.unity");
        string campGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scenes/CampStage.unity");
        Check(!string.IsNullOrEmpty(fishingGuid) && !string.IsNullOrEmpty(campGuid), "検証対象のシーンGUIDが見つからない");
        Check(await loader.LoadSceneAsync(fishingGuid), "釣り場を読み込めない");
        var fishing = UnityEngine.Object.FindFirstObjectByType<LifetimeScopes.FishingSceneLifetimeScope>();
        Check(ReferenceEquals(progress, fishing.Container.Resolve(typeof(GameProgress)))
            && progress.GameSeconds.CurrentValue == expectedSeconds, "釣り場への移動で時計が変わった");

        Check(await loader.LoadSceneAsync(campGuid), "キャンプを読み込めない");
        var camp = UnityEngine.Object.FindFirstObjectByType<LifetimeScopes.CampStageLifetimeScope>();
        Check(ReferenceEquals(progress, camp.Container.Resolve(typeof(GameProgress)))
            && progress.GameSeconds.CurrentValue == expectedSeconds && progress.CurrentDay.CurrentValue == expectedDay
            && progress.CurrentWeek.CurrentValue == expectedWeek && progress.GameHours.CurrentValue == 23
            && progress.CurrentTimeOfDay == TimeOfDay.Night, "往復後の時計と日時が一致しない");
        SceneResult = "PASS: CampStage→FishingStage→CampStageでRoot共有・時刻・日付・週・時間帯を保持";
    }

    /// <summary>条件を満たさない場合に検証を失敗させる</summary>
    /// <param name="condition">成立すべき条件</param>
    /// <param name="message">失敗時に報告する内容</param>
    /// <example>Check(progress.GameHours.CurrentValue == 8, "8時へ進まない")</example>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

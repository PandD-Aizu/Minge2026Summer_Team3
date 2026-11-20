using System;
using System.Collections.Generic;
using _Project.Scripts.Core;
using Controller;
using Quota;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>独立した進行データでノルマの朝の期限と重複判定を検証する</summary>
public static class QuotaDeadlineChecks
{
    /// <summary>翌朝と週末の境界、達成と未達、未発行と破棄後を検証する</summary>
    /// <returns>すべての条件を満たした場合の結果文字列</returns>
    /// <example>Tools/Quota/Validate Morning Deadlineから実行する</example>
    [MenuItem("Tools/Quota/Validate Morning Deadline")]
    public static string Run()
    {
        using (var fixture = new Fixture())
        {
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeByHours(24);
            Check(fixture.Service.Progress == 0 && fixture.Logs.Count == 0, "未発行のノルマを進行または判定した");
        }

        using (var fixture = new Fixture())
        {
            fixture.StartNightQuota();
            fixture.Progress.AdvanceGameTimeHours(0);
            fixture.Progress.AdvanceGameTimeHours(4);
            fixture.Progress.AdvanceGameTimeSeconds(3 * 3600 - 1);
            Check(fixture.Logs.Count == 0, "翌朝7時より前に判定した");
            fixture.Progress.AdvanceGameTimeSeconds(1);
            Check(fixture.Logs.Count == 1 && fixture.Logs[0].Contains("GameOver"), "未達の翌朝にGameOver判定がない");
            fixture.Progress.AdvanceGameTimeByHours(48);
            Check(fixture.Logs.Count == 1, "判定を繰り返した");
        }

        using (var fixture = new Fixture())
        {
            fixture.StartNightQuota();
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeHours(7);
            Check(fixture.Logs.Count == 1 && fixture.Logs[0].Contains("継続可能"), "目標数ちょうどで達成にならない");
            Check(fixture.Service.Progress == 1, "判定後に進捗をリセットした");
        }

        using (var fixture = new Fixture())
        {
            fixture.StartMainQuota();
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeByHours(6 * 24 + 17);
            Check(fixture.Progress.CurrentWeek.CurrentValue == 2 && fixture.Service.Progress == 1
                && fixture.Logs.Count == 0, "8日目0時に進捗が消えたか期限前に判定した");
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeByHours(7);
            Check(fixture.Logs.Count == 1 && fixture.Logs[0].Contains("継続可能")
                && fixture.Service.Progress == 3, "週の期限朝に超過達成を判定できない");
        }

        using (var fixture = new Fixture())
        {
            fixture.StartMainQuota();
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeByHours(8 * 24);
            Check(fixture.Logs.Count == 1 && fixture.Logs[0].Contains("GameOver"), "朝を飛び越す時間更新で週の未達判定がない");
            fixture.Progress.RecordFishCaught(1);
            Check(fixture.Service.Progress == 1, "期限後の釣果で判定済みの進捗が変わった");
        }

        using (var fixture = new Fixture())
        {
            fixture.StartNightQuota();
            fixture.Presenter.Dispose();
            fixture.Progress.RecordFishCaught(1);
            fixture.Progress.AdvanceGameTimeByHours(48);
            Check(fixture.Logs.Count == 0 && fixture.Service.Progress == 0, "破棄後も時計か釣果を購読している");
        }

        return "PASS: 未発行、翌朝7時の前後、達成・未達・超過達成、週変更時の進捗保持、朝をまたぐ時間更新、一度だけの判定、期限後の釣果、購読解除";
    }

    /// <summary>条件を満たさない場合に検証を失敗させる</summary>
    /// <param name="condition">成立すべき条件</param>
    /// <param name="message">失敗時の説明</param>
    /// <example>Check(logs.Count == 1, "判定回数が異なる")</example>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>実際の表示Prefabと一時的なノルマ設定を使う検証環境</summary>
    private sealed class Fixture : IDisposable
    {
        public readonly GameProgress Progress = new();
        public readonly QuotaService Service = new();
        public readonly List<string> Logs = new();
        public readonly QuotaPresenter Presenter;
        private readonly TutorialController _tutorial;
        private readonly QuotaView _view;
        private readonly QuotaData[] _data;

        /// <summary>保存済みデータを変更せず、検証用Presenterを初期化する</summary>
        /// <example>using var fixture = new Fixture()</example>
        public Fixture()
        {
            _tutorial = new TutorialController(Progress, new ClockController());
            _view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<QuotaView>("Assets/_Project/Prefab/Canvas/QuotaCanvas.prefab"));
            _data = new[] { CreateQuota(1), CreateQuota(2), CreateQuota(3) };
            Presenter = new QuotaPresenter(_tutorial, _view, Service, _data, Progress);
            Application.logMessageReceived += CaptureLog;
            Presenter.Initialize();
        }

        /// <summary>公開APIで最初の夜のノルマ発行まで進める</summary>
        /// <example>翌朝判定の検証前に呼ぶ</example>
        public void StartNightQuota()
        {
            _tutorial.NotifyRadioInteracted();
            _tutorial.NotifyFishCaught();
            _tutorial.NotifyReturnedToCollection();
            _tutorial.NotifyRadioInteracted();
            _tutorial.NotifyExchangeCompleted();
            _tutorial.NotifyRadioInteracted();
        }

        /// <summary>練習ノルマを達成して本編1週目のノルマ発行まで進める</summary>
        /// <example>週の境界の検証前に呼ぶ</example>
        public void StartMainQuota()
        {
            StartNightQuota();
            _tutorial.NotifyEnteredFishingStage();
            _tutorial.NotifyUpperBeingSeen();
            Progress.RecordFishCaught(1);
            _tutorial.NotifyFishCaught();
            _tutorial.NotifyReturnedToCollection();
            _tutorial.NotifyRadioInteracted();
            Check(Logs.Count == 1 && Logs[0].Contains("継続可能") && Service.Progress == 0,
                "本編開始朝に練習ノルマの判定前に進捗がリセットされた");
            Logs.Clear();
        }

        /// <summary>目標数を設定した一時的なSOを作成する</summary>
        /// <param name="amount">検証用の目標数</param>
        /// <returns>ディスクへ保存しないノルマ設定</returns>
        /// <example>CreateQuota(2)で魚2匹を目標にする</example>
        private static QuotaData CreateQuota(int amount)
        {
            var data = ScriptableObject.CreateInstance<QuotaData>();
            var serialized = new SerializedObject(data);
            serialized.FindProperty("_quotaAmount").intValue = amount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        /// <summary>この検証中のノルマ判定ログを収集する</summary>
        /// <param name="message">ログ本文</param>
        /// <param name="stackTrace">呼び出し履歴</param>
        /// <param name="type">ログ種別</param>
        /// <example>Application.logMessageReceivedから呼ばれる</example>
        private void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (message.StartsWith("[Quota]", StringComparison.Ordinal)) Logs.Add(message);
        }

        /// <summary>購読と一時オブジェクトを解放し、シーンとアセットを保存しない</summary>
        /// <example>usingブロック終了時に呼ばれる</example>
        public void Dispose()
        {
            Presenter.Dispose();
            Application.logMessageReceived -= CaptureLog;
            _tutorial.Dispose();
            Progress.Dispose();
            Object.DestroyImmediate(_view.gameObject);
            foreach (QuotaData data in _data) Object.DestroyImmediate(data);
        }
    }
}

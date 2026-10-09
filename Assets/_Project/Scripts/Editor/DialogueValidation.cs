using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Dialogue;
using R3;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>会話の状態遷移と入力制限を実際のUnity APIで検証する</summary>
public static class DialogueValidation
{
    private static int _checks;

    /// <summary>会話単体と本編連携の回帰検証を実行する</summary>
    /// <example>Tools/Dialogue/Validateから実行する</example>
    [MenuItem("Tools/Dialogue/Validate")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Edit Modeで実行してください");
        _checks = 0;
        var data = CreateData();
        var empty = ScriptableObject.CreateInstance<DialogueData>();
        try
        {
            ValidateService(data, empty);
            ValidateGameplay(data, empty);
            ValidateView(data);
            Debug.Log($"DIALOGUE_VALIDATION_PASSED: {_checks} checks");
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(empty);
        }
    }

    /// <summary>開始拒否、中断、破棄、連続再生で結果が混ざらないことを確認する</summary>
    /// <param name="data">二行のテスト会話</param>
    /// <param name="empty">空の会話</param>
    /// <example>Runから呼ぶ</example>
    private static void ValidateService(DialogueData data, DialogueData empty)
    {
        using var service = new DialogueService();
        var completed = 0;
        using var subscription = service.Completed.Subscribe(_ => completed++);
        Check(Result(service.PlayAsync(null)) == DialogueResult.Rejected, "null data");
        Check(Result(service.PlayAsync(empty)) == DialogueResult.Rejected, "empty data");

        var first = service.PlayAsync(data);
        Check(service.IsPlaying && service.CurrentLine.CurrentValue == data.GetLine(0), "first line");
        Check(Result(service.PlayAsync(data)) == DialogueResult.Rejected, "double start");
        Check(service.CurrentLine.CurrentValue == data.GetLine(0), "double start preserves line");
        service.Advance();
        Check(service.CurrentLine.CurrentValue == data.GetLine(1), "second line");
        service.Advance();
        Check(Result(first) == DialogueResult.Completed && completed == 1, "normal completion");
        Check(!service.IsPlaying && service.CurrentLine.CurrentValue == null, "normal cleanup");
        service.Advance();
        Check(completed == 1, "extra advance");

        using var cancellation = new CancellationTokenSource();
        var canceled = service.PlayAsync(data, cancellation.Token);
        cancellation.Cancel();
        Check(Result(canceled) == DialogueResult.Canceled, "token cancellation");
        Check(!service.IsPlaying && completed == 1, "cancel must not complete story");
        Check(Result(service.PlayAsync(data, cancellation.Token)) == DialogueResult.Canceled, "pre-canceled token");

        var manualCancel = service.PlayAsync(data);
        service.CancelDialogue();
        Check(Result(manualCancel) == DialogueResult.Canceled, "manual cancellation");

        // 完了通知で次の会話を開始しても、前の要求に次の結果を返さない
        UniTask<DialogueResult> next = default;
        using (service.Completed.Subscribe(_ => next = service.PlayAsync(data)))
        {
            var prior = service.PlayAsync(data);
            service.Advance();
            service.Advance();
            Check(Result(prior) == DialogueResult.Completed && service.IsPlaying, "reentrant next dialogue");
        }
        service.CancelDialogue();
        Check(Result(next) == DialogueResult.Canceled, "next dialogue owns result");

        var disposed = service.PlayAsync(data);
        service.Dispose();
        Check(Result(disposed) == DialogueResult.Canceled, "dispose wakes waiter");
        Check(Result(service.PlayAsync(data)) == DialogueResult.Rejected, "disposed service rejects start");
    }

    /// <summary>入力の抑止、他の停止所有者、開始失敗と破棄時の復旧を確認する</summary>
    /// <param name="data">有効なテスト会話</param>
    /// <param name="empty">空の会話</param>
    /// <example>Runから呼ぶ</example>
    private static void ValidateGameplay(DialogueData data, DialogueData empty)
    {
        var go = new GameObject("DialogueValidationPlayer");
        var originalSettings = InputSystem.settings;
        var savedSettings = Object.Instantiate(originalSettings);
        var testSettings = Object.Instantiate(originalSettings);
        InputSystem.settings = testSettings;
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var actions = new PlayerInputAction();
        using var service = new DialogueService();
        var input = go.AddComponent<PlayerInputReader>();
        using var menuInput = new Input.MenuInputService();
        input.Construct(actions, menuInput);
        var savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dialogue-" + Guid.NewGuid() + ".json");
        var save = new SaveSettings.SaveService(savePath);
        using var controller = new GameplayDialogueController(service, input, save);
        try
        {
            Check(input.CanStartGameplayAction, "player ready");
            Check(Result(controller.PlayAsync(empty)) == DialogueResult.Rejected, "invalid request rejected");
            Check(!input.IsGameplayInputBlocked && input.CanStartGameplayAction, "invalid request releases input");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            Check(input.MoveInput.sqrMagnitude > 0f, "real movement input");
            var play = controller.PlayAsync(data);
            Check(input.IsGameplayInputBlocked && input.MoveInput == Vector2.zero, "movement blocked");
            Check(Result(controller.PlayAsync(data)) == DialogueResult.Rejected, "controller double start");
            var interactions = 0;
            var inventory = 0;
            var cancel = 0;
            using var interactionSub = input.OnInteractPressed.Subscribe(_ => interactions++);
            using var inventorySub = input.OnInventoryPressed.Subscribe(_ => inventory++);
            using var cancelSub = input.OnCancelPressed.Subscribe(_ => cancel++);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.J, Key.F, Key.Escape));
            InputSystem.Update();
            Check(interactions == 0 && inventory == 0 && cancel == 0, "gameplay key events suppressed");

            using (input.BlockMovement())
            {
                service.Advance();
                service.Advance();
                Check(Result(play) == DialogueResult.Completed && !input.IsGameplayInputBlocked, "completed unlock");
                Check(input.MoveInput == Vector2.zero, "other movement owner preserved");
                Check(Result(controller.PlayAsync(data)) == DialogueResult.Rejected, "shop busy rejected");
            }
            Check(input.MoveInput.sqrMagnitude > 0f, "held movement restored");

            using var cancellation = new CancellationTokenSource();
            var interrupted = controller.PlayAsync(data, cancellation.Token);
            cancellation.Cancel();
            Check(Result(interrupted) == DialogueResult.Canceled && input.CanStartGameplayAction, "cancel unlock");

            ValidateSaveHistory(data, empty, service, controller, input, save, savePath);

            actions.Player.Disable();
            Check(Result(controller.PlayAsync(data)) == DialogueResult.Rejected, "mini-game input mode rejected");
            actions.Player.Enable();
            var dying = controller.PlayAsync(data);
            controller.Dispose();
            Check(Result(dying) == DialogueResult.Canceled && !input.IsGameplayInputBlocked, "scope dispose unlock");
        }
        finally
        {
            save.DeleteSaveData();
            Object.DestroyImmediate(go);
            InputSystem.RemoveDevice(keyboard);
            // 自動生成されたDisposeはPlay Mode用のDestroyを使うため、Edit Modeでは即時破棄する
            actions.Disable();
            Object.DestroyImmediate(actions.asset);
            // InputSystemはアセットでない旧設定を差し替え時に破棄する場合がある
            InputSystem.settings = originalSettings != null ? originalSettings : savedSettings;
            if (originalSettings != null) Object.DestroyImmediate(savedSettings);
            Object.DestroyImmediate(testSettings);
        }
    }

    /// <summary>表示履歴の永続化、切り替え、旧セーブとの互換性を確認する</summary>
    /// <param name="data">表示できる会話</param>
    /// <param name="empty">表示できない空の会話</param>
    /// <param name="service">会話の進行元</param>
    /// <param name="controller">検証対象の本編Controller</param>
    /// <param name="input">検証用の主人公入力</param>
    /// <param name="save">一時ファイル用のセーブサービス</param>
    /// <param name="savePath">一時ファイルのパス</param>
    /// <example>ValidateGameplayから呼び、実際のゲームセーブには触れない</example>
    private static void ValidateSaveHistory(DialogueData data, DialogueData empty, DialogueService service,
        GameplayDialogueController controller, PlayerInputReader input, SaveSettings.SaveService save, string savePath)
    {
        SetSaveOnce(data, true, "test-dialogue");
        SetSaveOnce(empty, true, "empty-dialogue");
        Check(Result(controller.PlayAsync(empty)) == DialogueResult.Rejected, "empty saved dialogue rejected");
        Check(!save.HasShownDialogue(empty.SaveId), "invalid dialogue not recorded");
        using (var canceled = new CancellationTokenSource())
        {
            canceled.Cancel();
            Check(Result(controller.PlayAsync(data, canceled.Token)) == DialogueResult.Canceled, "canceled request");
            Check(!save.HasShownDialogue(data.SaveId), "canceled request not recorded");
        }

        var first = controller.PlayAsync(data);
        Check(save.HasShownDialogue(data.SaveId), "recorded on first display");
        Check(save.LoadSaveData().useDefaultAudioSettings, "history first save preserves FMOD defaults");
        service.CancelDialogue();
        Check(Result(first) == DialogueResult.Canceled, "displayed dialogue can be interrupted");

        // インスタンスを作り直し、メモリではなくファイルから履歴を復元する
        using (var reloadedService = new DialogueService())
        using (var reloaded = new GameplayDialogueController(reloadedService, input, new SaveSettings.SaveService(savePath)))
        {
            Check(Result(reloaded.PlayAsync(data)) == DialogueResult.Skipped, "history survives reload");
            Check(!reloadedService.IsPlaying && input.CanStartGameplayAction, "skip leaves input untouched");
        }

        SetSaveOnce(data, false, "test-dialogue");
        var repeat = controller.PlayAsync(data);
        Check(service.IsPlaying, "toggle off allows repeat");
        service.CancelDialogue();
        Check(Result(repeat) == DialogueResult.Canceled, "repeat cleanup");
        SetSaveOnce(data, true, "test-dialogue");
        Check(Result(controller.PlayAsync(data)) == DialogueResult.Skipped, "toggle on retains history");

        // 履歴がない旧形式のセーブも読み、音量を残して履歴だけを追加する
        System.IO.File.WriteAllText(savePath, "{\"audioSettings\":{\"masterVolume\":0.4}}");
        Check(!save.HasShownDialogue(data.SaveId), "old save has no history");
        var oldSave = controller.PlayAsync(data);
        Check(Mathf.Approximately(save.LoadSaveData().audioSettings.masterVolume, 0.4f), "audio preserved");
        Check(!save.LoadSaveData().useDefaultAudioSettings, "old saved audio remains enabled");
        service.CancelDialogue();
        Check(Result(oldSave) == DialogueResult.Canceled, "old save playback cleanup");
        save.MarkDialogueShown(data.SaveId);
        Check(save.LoadSaveData().shownDialogueIds.Count == 1, "history IDs remain unique");

        save.DeleteSaveData();
        var reset = controller.PlayAsync(data);
        Check(service.IsPlaying, "deleted save allows replay");
        service.CancelDialogue();
        Check(Result(reset) == DialogueResult.Canceled, "reset cleanup");
        SetSaveOnce(data, false, "test-dialogue");
        SetSaveOnce(empty, false, "empty-dialogue");
    }

    /// <summary>検証用会話の保存設定を変更する</summary>
    /// <param name="data">一時会話データ</param>
    /// <param name="enabled">セーブ単位で一度だけ表示する場合はtrue</param>
    /// <param name="id">検証用の固定ID</param>
    /// <example>SetSaveOnce(data, true, "test-dialogue")</example>
    private static void SetSaveOnce(DialogueData data, bool enabled, string id)
    {
        var serialized = new SerializedObject(data);
        serialized.FindProperty("_playOncePerSave").boolValue = enabled;
        serialized.FindProperty("_saveId").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Prefabの表示、クリックとJキーの進行、外部非表示時の中断を確認する</summary>
    /// <param name="data">二行のテスト会話</param>
    /// <example>Runから呼ぶ</example>
    private static void ValidateView(DialogueData data)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Canvas/DialogueCanvas .prefab");
        var instance = Object.Instantiate(prefab);
        var view = instance.GetComponent<DialogueUIView>();
        using var service = new DialogueService();
        var advanceSoundCount = 0;
        using var presenter = new DialoguePresenter(view, service, () => advanceSoundCount++);
        var previousKeyboard = Keyboard.current;
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            // Edit Modeでは通常のMonoBehaviour.Awakeが自動実行されない
            InvokeLifecycle(view, "Awake");
            presenter.Initialize();
            Check(!view.gameObject.activeSelf, "initial hidden UI");
            var play = service.PlayAsync(data);
            Check(view.gameObject.activeSelf, "show UI");
            var button = instance.GetComponentInChildren<Button>();
            button.onClick.Invoke();
            Check(service.CurrentLine.CurrentValue == data.GetLine(1), "button advances");
            button.onClick.Invoke();
            Check(Result(play) == DialogueResult.Completed && !view.gameObject.activeSelf, "button completes and hides");
            Check(advanceSoundCount == 2, "advance sound includes final line");

            // 会話開始時に押されていたJキーでは先頭を飛ばさない
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            InputSystem.Update();
            var keyboardPlay = service.PlayAsync(data);
            InvokeLifecycle(view, "OnEnable");
            InvokeLifecycle(view, "Update");
            Check(service.CurrentLine.CurrentValue == data.GetLine(0), "opening key does not advance");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokeLifecycle(view, "Update");

            // 他のキーと操作不可のボタンでは進めず、再押下で一行進める
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            Check(service.CurrentLine.CurrentValue == data.GetLine(0), "other key does not advance");
            button.interactable = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            Check(service.CurrentLine.CurrentValue == data.GetLine(0), "disabled button blocks keyboard");
            button.interactable = true;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            Check(service.CurrentLine.CurrentValue == data.GetLine(1), "J advances");

            // 長押しで連続進行せず、次の押下で会話を終了する
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            Check(service.CurrentLine.CurrentValue == data.GetLine(1), "held J does not repeat");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.J));
            InputSystem.Update();
            InvokeLifecycle(view, "Update");
            Check(Result(keyboardPlay) == DialogueResult.Completed && !view.gameObject.activeSelf, "J completes and hides");

            var hiddenInputs = 0;
            using var inputSubscription = view.TextBoxClicked.Subscribe(_ => hiddenInputs++);
            InvokeLifecycle(view, "Update");
            Check(hiddenInputs == 0, "hidden UI ignores keyboard");

            var interrupted = service.PlayAsync(data);
            view.Hide();
            InvokeLifecycle(view, "OnDisable");
            Check(Result(interrupted) == DialogueResult.Canceled && !service.IsPlaying, "hidden UI cancels");

            var parent = new GameObject("DisabledDialogueParent");
            instance.transform.SetParent(parent.transform, false);
            parent.SetActive(false);
            Check(Result(service.PlayAsync(data)) == DialogueResult.Canceled, "inactive parent cancels start");
            instance.transform.SetParent(null, false);
            Object.DestroyImmediate(parent);
        }
        finally
        {
            presenter.Dispose();
            Object.DestroyImmediate(instance);
            InputSystem.RemoveDevice(keyboard);
            previousKeyboard?.MakeCurrent();
        }
    }

    /// <summary>Edit Modeでは自動実行されないViewのライフサイクルを検証用に呼ぶ</summary>
    /// <param name="view">一時生成した会話View</param>
    /// <param name="method">Awake、OnEnable、Update、OnDisableのいずれか</param>
    /// <example>InvokeLifecycle(view, "Awake")</example>
    private static void InvokeLifecycle(DialogueUIView view, string method)
    {
        typeof(DialogueUIView).GetMethod(method,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(view, null);
    }

    /// <summary>検証用に二行の会話データを作る</summary>
    /// <returns>呼び出し元が破棄する一時データ</returns>
    /// <example>Runの開始時に作り、finallyで破棄する</example>
    private static DialogueData CreateData()
    {
        var data = ScriptableObject.CreateInstance<DialogueData>();
        var serialized = new SerializedObject(data);
        var lines = serialized.FindProperty("_lines");
        lines.arraySize = 2;
        for (var i = 0; i < 2; i++)
        {
            lines.GetArrayElementAtIndex(i).FindPropertyRelative("_speaker").enumValueIndex = i;
            lines.GetArrayElementAtIndex(i).FindPropertyRelative("_text").stringValue = $"会話テスト {i}";
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return data;
    }

    /// <summary>完了済みであることを確認して非同期結果を取得する</summary>
    /// <param name="task">その場で完了するはずの要求</param>
    /// <returns>会話の結果</returns>
    /// <example>キャンセル処理が待機を残していないことを確認する</example>
    private static DialogueResult Result(UniTask<DialogueResult> task)
    {
        Check(task.Status.IsCompleted(), "request must be completed");
        return task.GetAwaiter().GetResult();
    }

    /// <summary>検証が失敗した場合に例外でバッチ実行を失敗させる</summary>
    /// <param name="condition">成立するべき条件</param>
    /// <param name="label">失敗箇所を識別する説明</param>
    /// <example>Check(!service.IsPlaying, "stopped")</example>
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Dialogue validation failed: " + label);
        _checks++;
    }
}

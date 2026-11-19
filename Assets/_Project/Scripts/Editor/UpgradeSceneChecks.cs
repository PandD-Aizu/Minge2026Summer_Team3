using System;
using System.IO;
using InventoryData;
using LifetimeScopes;
using SaveSettings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Upgrades;
using VContainer;
using VContainer.Unity;

/// <summary>実シーンのDIと強化の持ち越しを一時セーブで検証する</summary>
[InitializeOnLoad]
public static class UpgradeSceneChecks
{
    private sealed class TestSaveService : SaveService
    {
        /// <summary>通常登録と区別できる検証専用の保存先を作る</summary>
        /// <param name="path">一時セーブのパス</param>
        /// <example>テスト時だけSaveServiceとして登録する</example>
        public TestSaveService(string path) : base(path) { }
    }
    private static IDisposable _saveOverride;
    private static SaveService _save;
    private static AsyncOperation _loading;
    private static UpgradeService _upgrades;
    private static int _stage, _frame;
    private static double _next, _deadline;
    private static float _baseSpeed;

    /// <summary>ドメインリロード後に検証の更新を再開する</summary>
    /// <example>UnityのPlay Mode移行時に呼ばれる</example>
    static UpgradeSceneChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("UpgradeSceneChecks", false)) return;
            _stage = 0;
            _deadline = EditorApplication.timeSinceStartup + 120;
            _next = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += Tick;
        };
    }

    /// <summary>空シーンから検証専用のセーブ差し替えを準備する</summary>
    /// <example>Unity -batchmode -executeMethod UpgradeSceneChecks.Runで起動する</example>
    public static void Run()
    {
        EconomyChecks.Run();
        UpgradeChecks.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("UpgradeSceneChecks", true);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Rootの自動生成より先に検証用の保存先を登録する</summary>
    /// <example>BeforeSceneLoadでRootが作られる前にUnityが呼ぶ</example>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void PrepareSave()
    {
        if (!SessionState.GetBool("UpgradeSceneChecks", false)) return;
        _save = new TestSaveService(Path.Combine(Path.GetTempPath(), "upgrade-scenes-" + Guid.NewGuid() + ".json"));
        if (!_save.WriteSaveData(new GameData { hasEconomy = true, balance = 1000, useDefaultAudioSettings = true }))
            throw new Exception("一時セーブの初期化");
        // Rootの通常登録の後から保存先だけ差し替え、実プレイヤーのセーブには触れない
        _saveOverride = LifetimeScope.Enqueue(builder => builder.RegisterInstance((TestSaveService)_save).As<SaveService>());
    }

    /// <summary>キャンプから釣り場へ往復し、実コンポーネントの注入と倍率を確認する</summary>
    /// <example>検証中だけEditorの更新から実行する</example>
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + 1;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new Exception("実シーン検証のタイムアウト");
            if (_loading != null && !_loading.isDone) return;
            if (_stage == 0)
            {
                Application.runInBackground = true;
                if (_save == null) throw new Exception("Root生成前の一時セーブ準備が実行されていない");
                _loading = SceneManager.LoadSceneAsync("CampStage");
            }
            else if (_stage == 1)
            {
                var root = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>();
                if (root == null || root.Container.Resolve<SaveService>() != _save) throw new Exception("検証用保存先の注入");
                _upgrades = root.Container.Resolve<UpgradeService>();
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
                if (player == null) throw new Exception("キャンプのプレイヤーが見つからない");
                _baseSpeed = player.CurrentMoveSpeed;
                foreach (var definition in _upgrades.Definitions)
                    if (!_upgrades.TryBuy(definition, 0, out var error)) throw new Exception(error);
                VerifyPlayer("キャンプ購入直後");
                _frame = Time.frameCount;
            }
            else if (_stage == 2)
            {
                if (Time.frameCount <= _frame) throw new Exception("キャンプのフレームが進んでいない");
                _loading = SceneManager.LoadSceneAsync("FishingStage");
            }
            else if (_stage == 3)
            {
                VerifyPlayer("釣り場への移動後");
                var scope = UnityEngine.Object.FindFirstObjectByType<FishingSceneLifetimeScope>();
                if (scope == null || scope.Container.Resolve<UpgradeService>() != _upgrades
                    || scope.Container.Resolve<MiniGame.GaugeMiniGameController>() == null)
                    throw new Exception("釣り場で強化とゲージControllerを解決できない");
                _frame = Time.frameCount;
            }
            else if (_stage == 4)
            {
                if (Time.frameCount <= _frame) throw new Exception("釣り場のフレームが進んでいない");
                _loading = SceneManager.LoadSceneAsync("CampStage");
            }
            else if (_stage == 5)
            {
                VerifyPlayer("キャンプへ帰還後");
                var restored = new UpgradeService(new Inventory(_save), UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>()
                    .Container.Resolve<_Project.Scripts.Data.EconomyAssets>());
                if (Mathf.Abs(restored.MoveSpeedMultiplier - 1.05f) > 0.0001f
                    || Mathf.Abs(restored.FishingGainMultiplier - 1.1f) > 0.0001f)
                    throw new Exception("シーン往復後の保存値");
                Debug.Log("UPGRADE SCENE CHECKS PASSED");
                Finish(0);
            }
            _stage++;
        }
        catch (Exception ex) { Debug.LogException(ex); Finish(1); }
    }

    /// <summary>現在のプレイヤーにRootの移動強化が反映されているか確認する</summary>
    /// <param name="phase">確認する場面</param>
    /// <example>シーンを移動する前後で呼ぶ</example>
    private static void VerifyPlayer(string phase)
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null || Mathf.Abs(player.CurrentMoveSpeed - _baseSpeed * 1.05f) > 0.0001f)
            throw new Exception(phase + "の移動強化が反映されていない");
        Debug.Log("Upgrade scene OK: " + phase);
    }

    /// <summary>差し替えと一時保存を解除してバッチ検証を終了する</summary>
    /// <param name="exitCode">成功は0、失敗は1</param>
    /// <example>成功時と例外時の両方から呼ぶ</example>
    private static void Finish(int exitCode)
    {
        SessionState.SetBool("UpgradeSceneChecks", false);
        EditorApplication.update -= Tick;
        _saveOverride?.Dispose();
        _save?.DeleteSaveData();
        EditorApplication.Exit(exitCode);
    }
}

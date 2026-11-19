using System;
using Controller;
using ExchangePoint;
using InventoryData;
using ItemMenus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.Data;
using LifetimeScopes;
using VContainer;

/// <summary>実際のPrefabをPlay Modeで組み立てて取引UIを検証する</summary>
[InitializeOnLoad]
public static class EconomyUIChecks
{
    private static TradePanelBinding _binding;
    private static EconomyAssets _assets;
    private static ItemMenuNavigation _navigation;
    private static Inventory _inventory;
    private static GameObject _detail;
    private static int _stage;
    private static double _next;
    private static int _previousFrame;
    private static GameObject _root;

    /// <summary>ドメインリロード後もバッチ検証を再開する</summary>
    /// <example>Unityが自動実行する</example>
    static EconomyUIChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("EconomyUIChecks", false))
            {
                _stage = 0;
                _next = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update += Tick;
            }
        };
    }

    /// <summary>独立した空シーンでUIの検証を開始する</summary>
    /// <example>Unity -batchmode -executeMethod EconomyUIChecks.Run</example>
    public static void Run()
    {
        EconomyChecks.Run();
        UpgradeChecks.Run();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("EconomyUIChecks", true);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>検証用カメラでUIを画像として保存する</summary>
    /// <param name="name">画像名</param><param name="width">画像幅</param><param name="height">画像高さ</param>
    /// <example>Capture("EconomyList")</example>
    private static void Capture(string name, int width = 1920, int height = 1080)
    {
        var cameraObject = new GameObject("VerificationCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.06f, 0.08f);
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        System.IO.File.WriteAllBytes("Library/" + name + ".png", image.EncodeToPNG());
        RenderTexture.active = previous;
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        camera.targetTexture = null;
        target.Release();
        UnityEngine.Object.Destroy(target);
        UnityEngine.Object.Destroy(image);
        UnityEngine.Object.Destroy(cameraObject);
    }

    /// <summary>フレームをまたいで一覧、詳細、取引と描画を確認する</summary>
    /// <example>検証中だけEditorの更新に登録する</example>
    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < _next) return;
        _next = EditorApplication.timeSinceStartup + 1;
        try
        {
            if (_stage == 0)
            {
                Application.runInBackground = true;
                _previousFrame = Time.frameCount;
                _assets = UnityEngine.Object.FindFirstObjectByType<GameLifetimeScope>().Container.Resolve<EconomyAssets>();
                var catalog = _assets.Items;
                _inventory = new Inventory();
                _inventory.Add(1001, 3);
                var service = new ExchangePointService(_inventory, catalog);
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Canvas/ItemListMenu.prefab"));
                _root = root;
                _detail = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Canvas/ItemSelectedPage.prefab"));
                _detail.GetComponentInChildren<Canvas>().sortingOrder = 20;
                _detail.SetActive(false);
                _navigation = root.GetComponent<ItemMenuNavigation>();
                _binding = new TradePanelBinding(root, _navigation, service, new TutorialController(new GameProgress(), new ClockController()), () => { }, new Upgrades.UpgradeService(_inventory, _assets), _assets);
                _navigation.SelectFirst();
            }
            else if (_stage == 1)
            {
                if (Time.frameCount <= _previousFrame) throw new Exception("Play Modeのフレームが進んでいない");
                if (_navigation.SelectedItem == null) throw new Exception("売却一覧を選択できない");
                Capture("EconomyList");
            }
            else if (_stage == 2)
            {
                _binding.Select(_navigation.SelectedItem);
                _detail.SetActive(true);
                _binding.BindDetail(_detail);
                _binding.Execute();
                _binding.Execute();
                if (_inventory.Balance != 200 || _inventory.GetCount(1001) != 2) throw new Exception("同一フレームの二重決定");
                Capture("EconomyDetail");
            }
            else if (_stage == 3)
            {
                _detail.GetComponentInChildren<Canvas>().sortingOrder = 20;
                _detail.SetActive(false);
                _navigation.SelectTab(1); _navigation.SelectFirst();
                if (_navigation.SelectedItem == null) throw new Exception("購入一覧が空");
                _navigation.SelectTab(2); _navigation.SelectFirst();
                if (_navigation.SelectedItem == null) throw new Exception("交換一覧が空");
                _navigation.SelectTab(3); _navigation.SelectFirst();
                if (_navigation.SelectedItem == null) throw new Exception("強化一覧が空");
                Capture("UpgradeList");
            }
            else if (_stage == 4)
            {
                _binding.Select(_navigation.SelectedItem);
                _detail.SetActive(true);
                _binding.BindDetail(_detail);
                Capture("UpgradeBefore");
                _binding.Execute();
                _binding.Execute();
                if (_inventory.Balance != 100 || _inventory.GetUpgradeLevel(1) != 1)
                    throw new Exception("強化の購入または二重決定の防止");
            }
            else if (_stage == 5)
            {
                Capture("UpgradeAfter");
                Capture("UpgradeAfter720", 1280, 720);
                _binding.Execute();
                if (_inventory.Balance != 100 || _inventory.GetUpgradeLevel(1) != 1)
                    throw new Exception("残高不足の強化");
                var wallet = new Wallet(_inventory);
                if (!wallet.TryDeposit(1800)) throw new Exception("検証用残高追加");
                var upgrades = new Upgrades.UpgradeService(_inventory, _assets);
                var definition = upgrades.Definitions[0];
                for (int level = 1; level < 5; level++)
                    if (!upgrades.TryBuy(definition, level, out _)) throw new Exception("上限検証の準備");
                _binding.Refresh();
                Capture("UpgradeMaximum");
            }
            else if (_stage == 6)
            {
                _detail.SetActive(false);
                _navigation.SelectTab(2);
                Transform potionRow = null;
                foreach (Transform row in _navigation.Pages[2].content)
                {
                    var label = row.GetComponentInChildren<TMPro.TMP_Text>();
                    if (row.gameObject.activeSelf && label != null && label.text.Contains("気配消し")) potionRow = row;
                }
                if (potionRow == null) throw new Exception("ポーション調合が一覧にない");
                _binding.Select(potionRow);
                _detail.SetActive(true);
                _binding.BindDetail(_detail);
                Capture("AlchemyBefore");
                _binding.Execute();
                _binding.Execute();
                if (_inventory.GetCount(3000) != 1 || _inventory.GetCount(1001) != 1)
                    throw new Exception("調合の購入または二重決定の防止");
            }
            else if (_stage == 7)
            {
                Capture("AlchemyAfter");
                _root.SetActive(false);
                _detail.SetActive(false);
                var prefab = AssetDatabase.LoadAssetAtPath<InventoryView>("Assets/_Project/Prefab/Canvas/InventoryUI.prefab");
                var inventoryView = UnityEngine.Object.Instantiate(prefab);
                inventoryView.ClearItems();
                var catalog = _assets.Items;
                inventoryView.AddItem(catalog.FindById(3000), _inventory.GetCount(3000));
                inventoryView.Show();
                inventoryView.Navigation.SelectTab(2);
                inventoryView.Navigation.SelectFirst();
                if (inventoryView.Navigation.SelectedItem == null) throw new Exception("薬がインベントリで選択できない");
                inventoryView.SetSelectedItem(catalog.FindById(3000), 1);
            }
            else if (_stage == 8)
            {
                Capture("PotionInventory");
                Debug.Log("ECONOMY UI CHECKS PASSED");
                SessionState.SetBool("EconomyUIChecks", false);
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            _stage++;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            SessionState.SetBool("EconomyUIChecks", false);
            EditorApplication.update -= Tick;
            EditorApplication.Exit(1);
        }
    }
}

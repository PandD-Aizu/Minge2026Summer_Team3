using System;
using Controller;
using ExchangePoint;
using InventoryData;
using ItemMenus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using _Project.Scripts.Core;
using _Project.Scripts.Data.Item;

/// <summary>実際のPrefabをPlay Modeで組み立てて取引UIを検証する</summary>
[InitializeOnLoad]
public static class EconomyUIChecks
{
    private static TradePanelBinding _binding;
    private static ItemMenuNavigation _navigation;
    private static Inventory _inventory;
    private static GameObject _detail;
    private static int _stage;
    private static double _next;

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
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("EconomyUIChecks", true);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>検証用カメラでUIを画像として保存する</summary>
    /// <param name="name">画像名</param><example>Capture("EconomyList")</example>
    private static void Capture(string name)
    {
        var cameraObject = new GameObject("VerificationCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.06f, 0.08f);
        var target = new RenderTexture(1920, 1080, 24);
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
        var image = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
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
                var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>("Assets/_Project/SO/Item/ItemCatalog.asset");
                _inventory = new Inventory();
                _inventory.Add(1001, 3);
                var service = new ExchangePointService(_inventory, catalog);
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Canvas/ItemListMenu.prefab"));
                _detail = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Canvas/ItemSelectedPage.prefab"));
                _detail.GetComponentInChildren<Canvas>().sortingOrder = 20;
                _detail.SetActive(false);
                _navigation = root.GetComponent<ItemMenuNavigation>();
                _binding = new TradePanelBinding(root, _navigation, service, new TutorialController(new GameProgress(), new ClockController()), () => { });
                _navigation.SelectFirst();
            }
            else if (_stage == 1)
            {
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

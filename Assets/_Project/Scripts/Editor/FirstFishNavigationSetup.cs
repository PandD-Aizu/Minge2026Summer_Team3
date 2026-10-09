using System;
using _Project.Scripts.Fishing;
using Controller;
using LifetimeScopes;
using NavigationArrowServices;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using View;

/// <summary>最初の魚の固定スポットと岸上の案内終点を設定する</summary>
public static class FirstFishNavigationSetup
{
    /// <summary>開いているFishingStageに初回釣りの固定位置と案内を設定する</summary>
    /// <example>FishingStageを開いてConfigureを実行する</example>
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before configuring");
        var scope = UnityEngine.Object.FindFirstObjectByType<FishingSceneLifetimeScope>();
        if (scope == null) throw new InvalidOperationException("Open FishingStage first");
        var settings = new SerializedObject(scope);
        var player = (PlayerInputReader)settings.FindProperty("_playerInputReader").objectReferenceValue;
        var firstSpot = GameObject.Find("FishingSpot_04").GetComponent<FishingSpot>();

        // 最初の魚だけを入口寄りのスポット中央に出し、他のスポットは初回釣果を待つ
        foreach (var spot in UnityEngine.Object.FindObjectsByType<FishingSpot>(FindObjectsSortMode.None))
        {
            if (spot.gameObject.scene != scope.gameObject.scene) continue;
            var spotSettings = new SerializedObject(spot);
            spotSettings.FindProperty("_firstCatchSpot").boolValue = spot == firstSpot;
            spotSettings.FindProperty("_waitForFirstCatch").boolValue = true;
            spotSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(spot);
        }

        // 海中ではなく、固定魚にインタラクトできる岸上まで経路を描く
        var candidate = firstSpot.transform.position + new Vector3(0f, 1.5f, 2.6f);
        if (!NavMesh.SamplePosition(candidate, out var hit, 1f, 1))
            throw new InvalidOperationException("First fish has no walkable approach");
        var target = settings.FindProperty("_firstFishApproach").objectReferenceValue as Transform;
        if (target == null) target = new GameObject("First Fish Navigation Target").transform;
        target.SetParent(firstSpot.transform, true);
        target.position = hit.position;
        var view = scope.GetComponent<NavigationArrowView>();
        if (view == null) view = scope.gameObject.AddComponent<NavigationArrowView>();
        var viewSettings = new SerializedObject(view);
        viewSettings.FindProperty("_pathMaterial").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_NavigationTwinkle.mat");
        viewSettings.ApplyModifiedPropertiesWithoutUndo();
        settings.FindProperty("_firstFishApproach").objectReferenceValue = target;
        settings.FindProperty("_navigationView").objectReferenceValue = view;
        settings.ApplyModifiedPropertiesWithoutUndo();

        Validate(player.transform, target.position, view);
        EditorSceneManager.MarkSceneDirty(scope.gameObject.scene);
        EditorSceneManager.SaveScene(scope.gameObject.scene);
    }

    /// <summary>入口からの直角経路と初回獲得後の案内終了を確認する</summary>
    /// <param name="player">入口に配置されたプレイヤー</param>
    /// <param name="target">固定魚の手前の岸上座標</param>
    /// <param name="view">共通の光パーティクル表示</param>
    /// <example>Configureの保存前に呼ぶ</example>
    private static void Validate(Transform player, Vector3 target, NavigationArrowView view)
    {
        var service = new NavigationArrowService(player, target, guideFirstFish: true);
        if (!service.TryCalculatePath(out var path)) throw new InvalidOperationException("First fish is unreachable");
        for (var i = 1; i < path.Length; i++)
        {
            var delta = path[i] - path[i - 1];
            if (Mathf.Abs(delta.x) > 0.001f && Mathf.Abs(delta.z) > 0.001f)
                throw new InvalidOperationException("First fish route contains a diagonal");
            if (NavMesh.Raycast(path[i - 1], path[i], out var hit, 1)
                && (hit.position - path[i]).sqrMagnitude >= 0.0001f)
                throw new InvalidOperationException("First fish route crosses an obstacle");
        }
        var tutorial = new TutorialController(new _Project.Scripts.Core.GameProgress());
        tutorial.NotifyRadioInteracted();
        var presenter = new NavigationArrowPresenter(view, service, tutorial);
        try
        {
            presenter.LateTick();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            if (particles == null || !particles.isPlaying) throw new InvalidOperationException("First fish guidance missing");
            tutorial.NotifyFishCaught();
            presenter.LateTick();
            if (particles.isPlaying || tutorial.IsFirstFishingPending)
                throw new InvalidOperationException("First fish guidance survived capture");
            Debug.Log($"First fish navigation validated: {path.Length} points, capture hides guidance");
        }
        finally
        {
            view.Hide();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            if (particles != null) UnityEngine.Object.DestroyImmediate(particles.gameObject);
        }
    }
}

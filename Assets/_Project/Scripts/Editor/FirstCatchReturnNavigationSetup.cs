using System;
using Controller;
using LifetimeScopes;
using NavigationArrowServices;
using Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using View;

/// <summary>初回釣果後の帰還ワープと無人集荷場への案内を設定する</summary>
public static class FirstCatchReturnNavigationSetup
{
    /// <summary>開いているゲームシーンへ帰還先を登録し、進行による切り替えを確認して保存する</summary>
    /// <example>CampStageとFishingStageをそれぞれ単独で開いてConfigureを実行する</example>
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before setup");
        var fishing = UnityEngine.Object.FindAnyObjectByType<FishingSceneLifetimeScope>();
        Component scope = fishing != null ? fishing : UnityEngine.Object.FindAnyObjectByType<NavigationArrowLifetimeScope>();
        var settings = new SerializedObject(scope);
        var initial = (Transform)settings.FindProperty(fishing != null ? "_firstFishApproach" : "_initialTarget").objectReferenceValue;
        var player = fishing != null
            ? ((PlayerInputReader)settings.FindProperty("_playerInputReader").objectReferenceValue).transform
            : (Transform)settings.FindProperty("_playerTransform").objectReferenceValue;
        Collider trigger;
        Transform parent;
        Vector3 candidate;

        if (fishing != null)
        {
            // 最初の魚から近い、CampStage行きの出口を選ぶ
            var guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scenes/CampStage.unity");
            SceneLoadServices.SceneTransitionTrigger exit = null;
            var nearest = float.PositiveInfinity;
            foreach (var item in UnityEngine.Object.FindObjectsByType<SceneLoadServices.SceneTransitionTrigger>())
            {
                if (new SerializedObject(item).FindProperty("_destination._guid").stringValue != guid) continue;
                var distance = (item.transform.position - initial.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                exit = item;
            }
            if (exit == null) throw new InvalidOperationException("CampStage return exit missing");
            trigger = exit.GetComponent<Collider>();
            parent = exit.transform;
            candidate = trigger.bounds.center;
        }
        else
        {
            // キャンプでは白い集荷場の操作可能な手前を目的地にする
            var shop = GameObject.Find("shop");
            var bounds = shop.GetComponent<Collider>().bounds;
            parent = shop.transform;
            candidate = new Vector3(bounds.center.x, initial.position.y, bounds.min.z - 0.65f);
            var exchange = UnityEngine.Object.FindAnyObjectByType<ExchangeLifetimeScope>();
            var connector = (Component)new SerializedObject(exchange).FindProperty("_exchangeInteractableConnector").objectReferenceValue;
            trigger = connector.GetComponent<Collider>();
        }
        candidate.y = initial.position.y;
        if (!NavMesh.SamplePosition(candidate, out var hit, 4f, 1)) throw new InvalidOperationException("No walkable return target");
        var controller = player.GetComponent<CharacterController>();
        var standing = hit.position - Vector3.up * (controller.center.y - controller.height * 0.5f);
        if (!Physics.ComputePenetration(controller, standing, player.rotation,
                trigger, trigger.transform.position, trigger.transform.rotation, out _, out _))
            throw new InvalidOperationException("Return target is outside its interaction/warp trigger");

        var target = settings.FindProperty("_firstCatchReturnTarget").objectReferenceValue as Transform;
        if (target == null) target = new GameObject("First Catch Return Target").transform;
        target.SetParent(parent, true);
        target.position = hit.position;
        settings.FindProperty("_firstCatchReturnTarget").objectReferenceValue = target;
        settings.ApplyModifiedPropertiesWithoutUndo();

        // 同じPresenterを使い、釣果通知後とキャンプ再入場後の案内を検証する
        var service = new NavigationArrowService(initial, initial.position, guideFirstFish: fishing != null,
            firstCatchReturnPosition: target.position);
        var tutorial = new TutorialController(new _Project.Scripts.Core.GameProgress());
        tutorial.NotifyRadioInteracted();
        tutorial.NotifyFishCaught();
        if (fishing == null) tutorial.NotifyReturnedToCollection();
        var view = scope.GetComponent<NavigationArrowView>();
        var presenter = new NavigationArrowPresenter(view, service, tutorial);
        try
        {
            presenter.LateTick();
            if (service.TargetPosition != target.position || !service.TryCalculatePath(out var path))
                throw new InvalidOperationException("Return navigation did not reach the configured destination");
            for (var i = 1; i < path.Length; i++)
            {
                var delta = path[i] - path[i - 1];
                if (Mathf.Abs(delta.x) > 0.001f && Mathf.Abs(delta.z) > 0.001f)
                    throw new InvalidOperationException("Diagonal return route");
                if (NavMesh.Raycast(path[i - 1], path[i], out var edge, 1)
                    && (edge.position - path[i]).sqrMagnitude >= 0.0001f)
                    throw new InvalidOperationException("Return route crosses an obstacle");
            }
            if (fishing == null)
            {
                tutorial.NotifyRadioInteracted();
                new NavigationArrowPresenter(view, service, tutorial).LateTick();
                var particles = view.GetComponentInChildren<ParticleSystem>();
                if (particles == null || !particles.isPlaying) throw new InvalidOperationException("Collection guidance missing before exchange");
                tutorial.NotifyCollectionAccessed();
                presenter.LateTick();
                if (particles.isPlaying || particles.particleCount > 0) throw new InvalidOperationException("Guidance remained after collection access");
            }
            Debug.Log($"First catch return validated: {scope.gameObject.scene.name}, target={target.position}, points={path.Length}");
        }
        finally
        {
            view.Hide();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            if (particles != null) UnityEngine.Object.DestroyImmediate(particles.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(scope.gameObject.scene);
        EditorSceneManager.SaveScene(scope.gameObject.scene);
    }
}

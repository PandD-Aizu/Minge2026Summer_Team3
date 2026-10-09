using System;
using Controller;
using LifetimeScopes;
using NavigationArrowServices;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using View;

/// <summary>キャンプの地形変更後にラジオまでの案内用NavMeshを再生成する</summary>
public static class CampNavigationSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/CampStage.unity";
    private const string MeshPath = "Assets/_Project/Graphics/CampNavigation.asset";
    private const string MaterialPath = "Assets/_Project/Materials/M_NavigationTwinkle.mat";

    /// <summary>キャンプを開き、経路をベイクしてラジオの接近地点と表示を保存する</summary>
    /// <example>Tools/Navigation/Bake Camp Radio Pathから実行する</example>
    [MenuItem("Tools/Navigation/Bake Camp Radio Path")]
    public static void Bake()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var scope = UnityEngine.Object.FindFirstObjectByType<NavigationArrowLifetimeScope>();
        var settings = new SerializedObject(scope);
        var player = (Transform)settings.FindProperty("_playerTransform").objectReferenceValue;
        var radio = GameObject.Find("Radio").transform;
        var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface == null) surface = new GameObject("Camp Navigation Surface").AddComponent<NavMeshSurface>();

        // プレイヤー自身の当たり判定をベイクへ含めず、地形と障害物のColliderを使う
        surface.agentTypeID = 0;
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.layerMask = LayerMask.GetMask("Default", "Ground");
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.1f;
        var controller = player.GetComponent<CharacterController>();
        BakePlayerSurface(surface, controller);
        if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh bake failed");

        // 既存アセットのGUIDを維持してベイク結果を保存する
        var baked = surface.navMeshData;
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(MeshPath);
        surface.RemoveData();
        if (existing == null) AssetDatabase.CreateAsset(baked, MeshPath);
        else
        {
            EditorUtility.CopySerialized(baked, existing);
            UnityEngine.Object.DestroyImmediate(baked);
            surface.navMeshData = existing;
        }
        surface.AddData();

        // ラジオの高さではなく足元の歩行面を目的地にし、机の上へ案内しない
        var projectedRadio = radio.position;
        projectedRadio.y = player.position.y;
        if (!NavMesh.SamplePosition(projectedRadio, out var hit, 4f, 1))
            throw new InvalidOperationException("No walkable radio approach found");
        var approach = GameObject.Find("Radio Navigation Target")?.transform;
        if (approach == null) approach = new GameObject("Radio Navigation Target").transform;
        approach.position = hit.position;
        approach.SetParent(radio, true);
        settings.FindProperty("_initialTarget").objectReferenceValue = approach;
        settings.ApplyModifiedPropertiesWithoutUndo();
        ValidateInteraction(controller, radio, approach.position);

        // 専用マテリアルを参照させ、ビルド時のシェーダーストリッピングを防ぐ
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            var shader = Shader.Find("Project/NavigationTwinkle");
            if (shader == null) throw new InvalidOperationException("Navigation shader missing");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        var view = scope.GetComponent<NavigationArrowView>();
        var viewSettings = new SerializedObject(view);
        var oldArrow = (Transform)viewSettings.FindProperty("_arrowTransform").objectReferenceValue;
        viewSettings.FindProperty("_arrowTransform").objectReferenceValue = null;
        viewSettings.FindProperty("_pathMaterial").objectReferenceValue = material;
        viewSettings.ApplyModifiedPropertiesWithoutUndo();
        if (oldArrow != null) UnityEngine.Object.DestroyImmediate(oldArrow.gameObject);

        ConfigureFishingStageTarget();
        ValidatePath(player, approach.position);
        Debug.Log($"Navigation setup: player={player.position}, radio={radio.position}, target={approach.position}");
        EditorUtility.SetDirty(surface);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
    }

    /// <summary>開いているキャンプのFishingStageへの出口を案内先に設定し、切り替えを検証する</summary>
    /// <example>CampStageを開いてConfigureFishingStageTargetを実行した後、シーンを保存する</example>
    public static void ConfigureFishingStageTarget()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Configure navigation outside Play mode");
        var scope = UnityEngine.Object.FindFirstObjectByType<NavigationArrowLifetimeScope>();
        var settings = new SerializedObject(scope);
        var player = (Transform)settings.FindProperty("_playerTransform").objectReferenceValue;
        var radio = (Transform)settings.FindProperty("_initialTarget").objectReferenceValue;
        // FishingStageへ接続する出口のうち、ラジオから近い方を選ぶ
        var destinationGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Scenes/FishingStage.unity");
        SceneLoadServices.SceneTransitionTrigger exit = null;
        var nearest = float.PositiveInfinity;
        foreach (var candidateExit in UnityEngine.Object.FindObjectsByType<SceneLoadServices.SceneTransitionTrigger>(FindObjectsSortMode.None))
        {
            var exitSettings = new SerializedObject(candidateExit);
            if (exitSettings.FindProperty("_destination._guid").stringValue != destinationGuid) continue;
            var distance = (candidateExit.transform.position - radio.position).sqrMagnitude;
            if (distance >= nearest) continue;
            nearest = distance;
            exit = candidateExit;
        }
        if (exit == null) throw new InvalidOperationException("FishingStage exit missing");
        var trigger = exit.GetComponent<Collider>();
        var candidate = trigger.bounds.center;
        candidate.y = radio.position.y;
        if (!NavMesh.SamplePosition(candidate, out var hit, 4f, 1))
            throw new InvalidOperationException("No FishingStage exit approach on NavMesh");
        var controller = player.GetComponent<CharacterController>();
        var standing = hit.position - Vector3.up * (controller.center.y - controller.height * 0.5f);
        if (!Physics.ComputePenetration(controller, standing, player.rotation,
                trigger, trigger.transform.position, trigger.transform.rotation, out _, out _))
            throw new InvalidOperationException("Navigation endpoint does not enter the warp trigger");

        var target = settings.FindProperty("_fishingStageTarget").objectReferenceValue as Transform;
        if (target == null) target = new GameObject("FishingStage Navigation Target").transform;
        target.name = "FishingStage Navigation Target";
        target.SetParent(exit.transform, true);
        target.position = hit.position;
        settings.FindProperty("_fishingStageTarget").objectReferenceValue = target;
        settings.ApplyModifiedPropertiesWithoutUndo();

        // 本番のPresenterでラジオ会話完了直後の切り替えと初回交換後の消灯を確認する
        var service = new NavigationArrowService(player, radio.position, target.position);
        var tutorial = new TutorialController(new _Project.Scripts.Core.GameProgress());
        var view = scope.GetComponent<NavigationArrowView>();
        var presenter = new Presentation.NavigationArrowPresenter(view, service, tutorial);
        var existingParticles = view.GetComponentInChildren<ParticleSystem>();
        try
        {
            presenter.LateTick();
            if (service.TargetPosition != radio.position) throw new InvalidOperationException("Initial target is not radio");
            tutorial.NotifyRadioDialogueCompleted(true, null);
            presenter.LateTick();
            if (service.TargetPosition != target.position || !service.TryCalculatePath(out var path))
                throw new InvalidOperationException("Radio completion did not select a reachable FishingStage exit");
            ValidateOrthogonal(path);
            var fromRadio = new NavigationArrowService(radio, target.position);
            if (!fromRadio.TryCalculatePath(out var exitPath))
                throw new InvalidOperationException("FishingStage exit is unreachable from radio");
            ValidateOrthogonal(exitPath);

            tutorial.NotifyFishCaught();
            tutorial.NotifyReturnedToCollection();
            tutorial.NotifyRadioDialogueCompleted(true, null);
            tutorial.NotifyExchangeCompleted();
            presenter.LateTick();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            if (particles != null && (particles.isPlaying || particles.particleCount > 0))
                throw new InvalidOperationException("Navigation remained after first exchange");
            tutorial.NotifyRadioDialogueCompleted(true, null);
            presenter.LateTick();
            if (service.TargetPosition != target.position || particles == null || !particles.isPlaying)
                throw new InvalidOperationException("Night fishing did not resume exit guidance");
            Debug.Log($"FishingStage navigation validated: radio -> {target.position}, first exchange -> hidden, night fishing -> exit");
        }
        finally
        {
            view.Hide();
            var particles = view.GetComponentInChildren<ParticleSystem>();
            if (existingParticles == null && particles != null) UnityEngine.Object.DestroyImmediate(particles.gameObject);
        }
    }

    /// <summary>プレイヤーの高さと段差上限を反映した歩行面を生成する</summary>
    /// <param name="surface">ベイク結果の保持先</param>
    /// <param name="controller">通行可能な体格と段差の基準</param>
    /// <example>Bakeから呼び、敵用の共通Agent設定は変更しない</example>
    private static void BakePlayerSurface(NavMeshSurface surface, CharacterController controller)
    {
        var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
        var markups = new System.Collections.Generic.List<NavMeshBuildMarkup>
        {
            new() { root = controller.transform, ignoreFromBuild = true }
        };
        NavMeshBuilder.CollectSources(null, surface.layerMask, surface.useGeometry, 0, markups, sources);
        sources.RemoveAll(source => source.component is Collider collider && collider.isTrigger);

        var bounds = new Bounds(controller.transform.position, Vector3.one);
        foreach (var source in sources)
        {
            if (source.component is Collider collider) bounds.Encapsulate(collider.bounds);
        }
        bounds.Expand(4f);
        var settings = surface.GetBuildSettings();
        settings.agentRadius = controller.radius;
        settings.agentHeight = controller.height;
        settings.agentClimb = controller.stepOffset;
        settings.agentSlope = controller.slopeLimit;
        surface.RemoveData();
        surface.navMeshData = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds,
            surface.transform.position, surface.transform.rotation);
    }

    /// <summary>初期位置からの到達性と初回会話後の非表示を検証する</summary>
    /// <param name="player">シーンのプレイヤー</param>
    /// <param name="target">ラジオ付近の接近地点</param>
    /// <example>Bakeの保存前に呼ぶ</example>
    private static void ValidatePath(Transform player, Vector3 target)
    {
        var service = new NavigationArrowService(player, target);
        if (!service.TryCalculatePath(out var corners))
            throw new InvalidOperationException($"Radio route is unreachable from {player.position} to {target}");
        Debug.Log($"Navigation path validated: {corners.Length} corners: {string.Join(", ", corners)}");

        // 本番と同じPresenterで、初回表示と会話後の消灯を検証する
        var tutorial = new TutorialController(new _Project.Scripts.Core.GameProgress());
        var view = UnityEngine.Object.FindFirstObjectByType<NavigationArrowView>();
        var presenter = new Presentation.NavigationArrowPresenter(view, service, tutorial);
        presenter.LateTick();
        var particles = view.GetComponentInChildren<ParticleSystem>();
        if (particles == null || !particles.isPlaying) throw new InvalidOperationException("Initial guidance was not shown");
        ValidateOrthogonal(corners);
        if (Application.isBatchMode)
        {
            for (var i = 0; i < 8; i++)
            {
                typeof(NavigationArrowView).GetMethod("EmitParticles",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(view, null);
                particles.Simulate(0.12f, true, false);
            }
            var emitted = new ParticleSystem.Particle[particles.particleCount];
            if (particles.GetParticles(emitted) == 0) throw new InvalidOperationException("No navigation particles emitted");
            var spreadAngle = new SerializedObject(view).FindProperty("_spreadAngle").floatValue;
            var maxSpreadRatio = Mathf.Tan(spreadAngle * Mathf.Deg2Rad);
            foreach (var particle in emitted)
            {
                var horizontalSpeed = new Vector2(particle.velocity.x, particle.velocity.z).magnitude;
                if (particle.velocity.y <= 0f || horizontalSpeed > particle.velocity.y * maxSpreadRatio + 0.001f)
                    throw new InvalidOperationException("Navigation particle is outside the upward spread cone");
            }
            particles.Play();
            CapturePreview(corners);
        }
        tutorial.NotifyRadioDialogueCompleted(true, null);
        presenter.LateTick();
        if (particles.isPlaying || particles.particleCount > 0) throw new InvalidOperationException("Guidance remained after radio dialogue");

        // 進行済み状態でシーンに再入場しても案内を復活させない
        new Presentation.NavigationArrowPresenter(view, service, tutorial).LateTick();
        if (particles.isPlaying || particles.particleCount > 0) throw new InvalidOperationException("Guidance returned after re-entry");
        UnityEngine.Object.DestroyImmediate(particles.gameObject);
        Debug.Log("Navigation state validation passed: initial route, radio completion, re-entry");

        // NavMeshのない目的地では、障害物を無視した代替の直線を返さない
        service.SetTargetPosition(new Vector3(10000f, 10000f, 10000f));
        if (service.TryCalculatePath(out _)) throw new InvalidOperationException("Invalid target produced a route");
        service.SetTargetPosition(target);
        ValidateDetours(player, service);
    }

    /// <summary>案内終点でプレイヤーがラジオの会話判定に重なることを確認する</summary>
    /// <param name="player">接近判定に使うプレイヤーのCollider</param>
    /// <param name="radio">ラジオの表示オブジェクト</param>
    /// <param name="target">案内の終点</param>
    /// <example>地面のベイク後に呼び、話せない位置への案内を防ぐ</example>
    private static void ValidateInteraction(CharacterController player, Transform radio, Vector3 target)
    {
        var connector = radio.parent.GetComponentInChildren<_Project.Scripts.InteractableObject.InteractableConnector>();
        var trigger = connector.GetComponent<Collider>();
        var feetOffset = player.center.y - player.height * 0.5f;
        var standingPosition = target - Vector3.up * feetOffset;
        if (!Physics.ComputePenetration(player, standingPosition, player.transform.rotation,
                trigger, trigger.transform.position, trigger.transform.rotation, out _, out _))
            throw new InvalidOperationException($"Radio approach lies outside interaction range: {target}");
        Debug.Log("Navigation interaction validation passed");
    }

    /// <summary>障害物の裏側を含む複数地点から経路を再計算できることを確認する</summary>
    /// <param name="player">一時的に位置を変えるプレイヤー</param>
    /// <param name="service">本番の経路計算サービス</param>
    /// <example>Bakeの検証時に呼び、検証後はプレイヤー位置を戻す</example>
    private static void ValidateDetours(Transform player, NavigationArrowService service)
    {
        var original = player.position;
        var count = 0;
        var detours = 0;
        try
        {
            for (var x = -10f; x <= 10f; x += 2f)
            for (var z = -8f; z <= 8f; z += 2f)
            {
                if (!NavMesh.SamplePosition(new Vector3(x, 0.5f, z), out var hit, 0.3f, 1)) continue;
                player.position = hit.position;
                if (!service.TryCalculatePath(out var points)) continue;
                ValidateOrthogonal(points);
                count++;
                var length = 0f;
                for (var i = 1; i < points.Length; i++) length += Vector3.Distance(points[i - 1], points[i]);
                if (length > Mathf.Abs(points[0].x - points[points.Length - 1].x)
                    + Mathf.Abs(points[0].z - points[points.Length - 1].z) + 0.5f) detours++;
            }
        }
        finally
        {
            player.position = original;
        }
        if (count == 0 || detours == 0) throw new InvalidOperationException("No obstacle detour was validated");
        Debug.Log($"Navigation movement validation passed: {count} start positions, {detours} obstacle detours");
    }

    /// <summary>経路に斜めの区間が含まれないことを検証する</summary>
    /// <param name="points">検証する経路頂点</param>
    /// <example>初期位置と障害物周辺の経路へ適用する</example>
    private static void ValidateOrthogonal(Vector3[] points)
    {
        for (var i = 1; i < points.Length; i++)
        {
            var delta = points[i] - points[i - 1];
            if (Mathf.Abs(delta.x) > 0.001f && Mathf.Abs(delta.z) > 0.001f)
                throw new InvalidOperationException($"Diagonal navigation segment: {delta}");
            if (NavMesh.Raycast(points[i - 1], points[i], out var hit, 1)
                && (hit.position - points[i]).sqrMagnitude >= 0.0001f)
                throw new InvalidOperationException("Navigation segment crosses an obstacle");
        }
    }

    /// <summary>バッチ検証時に経路周辺の俯瞰画像を保存する</summary>
    /// <param name="corners">表示中の経路頂点</param>
    /// <example>Bakeのバッチ実行時にLogs/navigation-preview.pngを出力する</example>
    private static void CapturePreview(Vector3[] corners)
    {
        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (var corner in corners) bounds.Encapsulate(corner);
        var cameraObject = new GameObject("Navigation validation camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(8f, bounds.size.magnitude * 0.7f);
        camera.transform.position = bounds.center + new Vector3(0f, 30f, -15f);
        camera.transform.LookAt(bounds.center);
        var target = new RenderTexture(1280, 720, 24);
        var previous = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllBytes("Logs/navigation-preview.png", image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
}



using System;
using System.Threading;
using _Project.Scripts.Fishing;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>水面を覗く主観視点から、タコに掴まれて海へ沈むまでを演出する</summary>
[RequireComponent(typeof(CinemachineCamera))]
public sealed class GameOverCameraView : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private Renderer _playerVisual;
    [SerializeField] private Renderer _waterSurface;
    [SerializeField] private Shader _creatureShader;
    [SerializeField] private Shader _underSurfaceShader;
    [SerializeField] private Material _rippleMaterial;
    [SerializeField] private TextAsset _waterSound;
    [SerializeField] private Texture2D _scareFace;
    [SerializeField] private Shader _waterParticleShader;
    [SerializeField] private int _inactivePriority = 0;
    [SerializeField] private int _activePriority = 20;
    [SerializeField, Min(0f)] private float _blendWaitSeconds = 2.1f;
    [Header("水中からの捕獲")]
    [SerializeField, Min(0.1f)] private float _surfaceLookDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float _emergeDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float _reachDuration = 0.22f;
    [SerializeField, Min(0.1f)] private float _pullDuration = 0.2f;
    [SerializeField, Min(0.1f)] private float _faceHoldDuration = 0.32f;
    [SerializeField, Min(0.3f)] private float _faceDistance = 0.9f;
    [SerializeField, Min(0.1f)] private float _fallDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float _submergedDuration = 2.4f;
    [SerializeField, Min(0.5f)] private float _octopusSize = 2.8f;

    private bool _activated;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private LensSettings _originalLens;
    private CinemachineComponentBase[] _cameraControllers;
    private bool[] _controllerEnabled;
    private Renderer[] _playerRenderers;
    private bool[] _playerEnabled;
    private Vector3 _waterPoint;
    private Vector3 _forward;
    private Vector3 _eyePosition;
    private Camera _output;
    private OceanCaptureCreature _creature;
    private OceanCaptureScreenView _screen;
    private OceanCaptureAudio _audio;
    private OceanCaptureParticles _particles;
    private bool _enteredWater;
    private bool _splashed;
    private float _submergedTime;

    public string CapturePhase { get; private set; } = "Idle";

    /// <summary>同じオブジェクトの仮想カメラを取得する</summary>
    /// <example>Unityの初期化時に呼ばれる</example>
    private void Awake() => _camera = GetComponent<CinemachineCamera>();

    /// <summary>最寄りの釣り場の水面を覗く視点へ切り替える</summary>
    /// <param name="cancellation">シーン破棄時の中断トークン</param>
    /// <returns>カメラの切り替えが完了するまでの待機</returns>
    /// <example>暗転してからActivateAsyncを待機する</example>
    public async UniTask ActivateAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_camera == null) _camera = GetComponent<CinemachineCamera>();
        _output = Camera.main;
        if (_waterSurface == null || _creatureShader == null || _underSurfaceShader == null || _output == null)
            throw new InvalidOperationException("落水演出の海面、シェーダー、メインカメラの参照が未設定");

        // 一時的にCinemachineの追従と注視を止め、演出の座標をそのまま使う
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        _originalLens = _camera.Lens;
        _activated = true;
        _cameraControllers = GetComponents<CinemachineComponentBase>();
        _controllerEnabled = new bool[_cameraControllers.Length];
        for (var i = 0; i < _cameraControllers.Length; i++)
        {
            _controllerEnabled[i] = _cameraControllers[i].enabled;
            _cameraControllers[i].enabled = false;
        }

        var player = FindFirstObjectByType<PlayerMovement>();
        var playerPosition = player != null ? player.transform.position : transform.position;
        _playerRenderers = player != null ? player.GetComponentsInChildren<Renderer>(true)
            : _playerVisual != null ? new[] { _playerVisual } : Array.Empty<Renderer>();
        _playerEnabled = new bool[_playerRenderers.Length];
        for (var i = 0; i < _playerRenderers.Length; i++)
        {
            _playerEnabled[i] = _playerRenderers[i].enabled;
            _playerRenderers[i].enabled = false;
        }

        // 魚影の出現位置を基準にし、陸や桟橋の上へ落とさない
        var closest = float.PositiveInfinity;
        _waterPoint = _waterSurface.bounds.center;
        foreach (var spot in FindObjectsByType<FishingSpot>(FindObjectsSortMode.None))
        {
            var distance = (spot.transform.position - playerPosition).sqrMagnitude;
            if (distance >= closest) continue;
            closest = distance;
            _waterPoint = spot.transform.position;
        }
        _forward = Vector3.ProjectOnPlane(_waterSurface.bounds.center - _waterPoint, Vector3.up).normalized;
        if (_forward.sqrMagnitude < 0.01f) _forward = Vector3.back;
        _waterPoint += _forward * 2f;
        _waterPoint.y = _waterSurface.transform.position.y;
        _eyePosition = _waterPoint - _forward * 3.2f + Vector3.up * 1.6f;
        transform.SetPositionAndRotation(_eyePosition, Quaternion.LookRotation(_waterPoint - _eyePosition, Vector3.up));
        _camera.Lens.FieldOfView = 66f;
        _camera.Lens.NearClipPlane = 0.04f;
        _camera.Lens.Dutch = 0f;
        _camera.Priority = _activePriority;
        _screen = _output.gameObject.AddComponent<OceanCaptureScreenView>();
        _audio = new OceanCaptureAudio(_waterSound);
        _audio.BeginQuiet();
        if (_waterParticleShader != null) _particles = new OceanCaptureParticles(_waterParticleShader, gameObject.scene);
        _enteredWater = false;
        _splashed = false;
        _submergedTime = 0f;
        CapturePhase = "WaterSurface";
        await UniTask.Delay(TimeSpan.FromSeconds(_blendWaitSeconds),
            DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellation);
    }

    /// <summary>既存の敵を隠し、水中に演出専用のタコを用意する</summary>
    /// <param name="enemy">既存のタコ画像を持つ敵</param>
    /// <example>主観視点へ切り替えた暗転中に呼ぶ</example>
    public void PrepareWaterAmbush(VisionEnemyView enemy)
    {
        _creature = new OceanCaptureCreature(enemy, _creatureShader, _underSurfaceShader, _waterPoint, _forward, _octopusSize);
        _creature.PrepareScareFace(_scareFace);
        enemy.SetVisible(false);
    }

    /// <summary>実体化、触手の掴み、落水、水中の漂いを順番に再生する</summary>
    /// <param name="cancellation">演出を中断するトークン</param>
    /// <param name="onEmerge">水面を突き破る瞬間に一度だけ呼ぶ音声などの処理</param>
    /// <param name="onGrab">引き寄せが始まる瞬間の衝撃音などの処理</param>
    /// <returns>潜水の余韻が終わるまでの待機</returns>
    /// <example>明転後にPlayWaterAmbushAsyncを待機する</example>
    public async UniTask PlayWaterAmbushAsync(CancellationToken cancellation, Action onEmerge = null, Action onGrab = null)
    {
        await AnimateAsync(_surfaceLookDuration, p =>
        {
            transform.position = _eyePosition + Vector3.up * Mathf.Sin(p * Mathf.PI) * 0.015f;
            _creature.ShowOmen(p, transform.position);
        }, cancellation);

        CapturePhase = "OctopusEmerging";
        cancellation.ThrowIfCancellationRequested();
        _audio.RestoreAmbience();
        EmitRipple(0.16f);
        _particles?.Splash(_waterPoint + Vector3.up * 0.05f, true);
        onEmerge?.Invoke();
        var lookStart = transform.rotation;
        await AnimateAsync(_emergeDuration, p =>
        {
            // 最初の数フレームで実体化し、飛沫とともに跳ね上がる
            var rise = 1f - Mathf.Pow(1f - p, 3f);
            var impact = Mathf.Sin(p * Mathf.PI) * (1f - p);
            _creature.Reveal(rise, transform.position);
            transform.position = _eyePosition - _forward * impact * 0.16f;
            var target = _waterPoint + Vector3.up * Mathf.Lerp(0f, _octopusSize * 0.3f, rise);
            transform.rotation = Quaternion.Slerp(lookStart, Quaternion.LookRotation(target - transform.position), rise)
                * Quaternion.Euler(-impact * 6f, Mathf.Sin(p * 25f) * impact * 2f, Mathf.Sin(p * 20f) * impact * 4f);
            _camera.Lens.FieldOfView = 66f + impact * 14f;
        }, cancellation);

        await UniTask.Delay(TimeSpan.FromSeconds(0.18f), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, cancellation);
        CapturePhase = "TentaclesGrabbing";
        await AnimateAsync(_reachDuration, p => _creature.UpdateReach(p, transform.position, transform.rotation), cancellation);

        onGrab?.Invoke();
        await PullToFaceAsync(cancellation);

        // 前方へ引かれながら加速して落ち、水面を横切ったフレームで音と水膜を開始する
        CapturePhase = "Falling";
        var fallStart = transform.position;
        var fallRotation = transform.rotation;
        var fallFov = _camera.Lens.FieldOfView;
        var fallEnd = _waterPoint - _forward * 0.6f - Vector3.up * 0.65f;
        await AnimateAsync(_fallDuration, p =>
        {
            var fall = p * p * (2f - p);
            transform.position = Vector3.Lerp(fallStart, fallEnd, fall);
            var downward = Quaternion.LookRotation(_forward + Vector3.down * 1.2f) * Quaternion.Euler(0f, 0f, -23f);
            transform.rotation = Quaternion.Slerp(fallRotation, downward, p)
                * Quaternion.Euler(Mathf.Sin(p * 28f) * p * 2f, 0f, Mathf.Sin(p * 21f) * p * 2.5f);
            _camera.Lens.FieldOfView = Mathf.Lerp(fallFov, 83f, Mathf.Sin(p * Mathf.PI * 0.5f));
            _creature.UpdateReach(1f, transform.position, transform.rotation);
            UpdateWater();
        }, cancellation);

        CapturePhase = "Underwater";
        var sunkRotation = transform.rotation;
        var upward = Quaternion.LookRotation(_forward * 0.4f + Vector3.up, Vector3.up) * Quaternion.Euler(0f, 0f, 28f);
        await AnimateAsync(_submergedDuration, p =>
        {
            transform.position = fallEnd - Vector3.up * (p * 1.8f) + Vector3.Cross(Vector3.up, _forward) * Mathf.Sin(p * 4f) * 0.12f;
            transform.rotation = Quaternion.Slerp(sunkRotation, upward, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 1.8f)))
                * Quaternion.Euler(Mathf.Sin(p * 13f) * 1.5f, 0f, Mathf.Sin(p * 9f) * 3f);
            _camera.Lens.FieldOfView = Mathf.Lerp(83f, 72f, p);
            _creature.UpdateReach(1f, transform.position, transform.rotation);
            UpdateWater();
        }, cancellation);
        CapturePhase = "SubmergedEnd";
    }

    /// <summary>触手に引かれて目の前へ急接近し、短い衝撃の余韻を残す</summary>
    /// <param name="cancellation">シーン終了や演出中断のトークン</param>
    /// <returns>引き寄せと目の前での停止が終わるまでの待機</returns>
    /// <example>触手が視点を掴み終わった直後に待機する</example>
    private async UniTask PullToFaceAsync(CancellationToken cancellation)
    {
        CapturePhase = "PulledToFace";
        var start = transform.position;
        var startRotation = transform.rotation;
        var target = _creature.EyePosition;
        var stop = target - _creature.FaceForward * Mathf.Max(0.3f, _faceDistance);
        var faceRotation = Quaternion.LookRotation(target - stop, Vector3.up);

        // 最初に加速し、終盤で急停止することで触手の引く力を見せる
        await AnimateAsync(_pullDuration, p =>
        {
            var travel = p < 0.7f ? 0.9f * Mathf.Pow(p / 0.7f, 2f)
                : Mathf.Lerp(0.9f, 1f, 1f - Mathf.Pow((1f - p) / 0.3f, 3f));
            var kick = Mathf.Sin(p * Mathf.PI);
            transform.position = Vector3.Lerp(start, stop, travel);
            transform.rotation = Quaternion.Slerp(startRotation, faceRotation, travel)
                * Quaternion.Euler(Mathf.Sin(p * 18f) * kick * 1.5f, 0f, -7f * kick);
            _camera.Lens.FieldOfView = Mathf.Lerp(66f, 58f, travel) + kick * 12f;
            _creature.ShowScareFace(Mathf.SmoothStep(0f, 1f, p), 0f);
            _creature.UpdateReach(1f, transform.position, transform.rotation);
        }, cancellation);

        // 目を認識できる短い間を作り、振動を減衰させてから落下へつなぐ
        CapturePhase = "FaceToFace";
        await AnimateAsync(_faceHoldDuration, p =>
        {
            var recoil = Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
            transform.position = stop - _creature.FaceForward * recoil * 0.06f;
            transform.rotation = faceRotation * Quaternion.Euler(recoil * 2.2f, 0f, recoil * 1.5f);
            _creature.ShowScareFace(1f, p);
            _creature.UpdateReach(1f, transform.position, transform.rotation);
        }, cancellation);
    }

    /// <summary>水面を越える動きに合わせて水中の映像と音を更新する</summary>
    /// <example>落下と潜水アニメーションの各フレームで呼ぶ</example>
    private void UpdateWater()
    {
        var immersion = Mathf.InverseLerp(_waterPoint.y + 0.15f, _waterPoint.y - 0.3f, transform.position.y);
        // 顔より先に体が水面へ触れ、視界の前へ飛沫が立ち上がる
        if (!_splashed && transform.position.y <= _waterPoint.y + 0.65f)
        {
            _splashed = true;
            var impact = transform.position + _forward * 0.35f;
            impact.y = _waterPoint.y + 0.05f;
            _particles?.Splash(impact);
        }
        if (!_enteredWater && immersion > 0f)
        {
            _enteredWater = true;
            _audio.EnterWater();
            EmitRipple(0.1f);
        }
        if (_enteredWater)
        {
            _submergedTime += Time.unscaledDeltaTime;
            _particles?.Tick(transform.position, transform.rotation, _submergedTime, Time.unscaledDeltaTime);
        }
        _screen.SetWater(immersion, _submergedTime);
        if (_enteredWater) _creature.Submerge(immersion, _submergedTime);
        _audio.UpdateMuffling(Mathf.Clamp01(_submergedTime / 0.35f) * immersion);
    }

    /// <summary>タコの出現と入水で既存の海面へ大きな波紋を加える</summary>
    /// <param name="strength">波の高さ</param>
    /// <example>出現開始と水面通過時に呼ぶ</example>
    private void EmitRipple(float strength)
    {
        OceanRippleField.Emit(_rippleMaterial, _waterPoint, 3f, Vector2.zero, strength, 3f, true, gameObject.scene);
    }

    /// <summary>入力停止中でも進む時間で指定の処理を補間する</summary>
    /// <param name="duration">演出時間、秒単位</param>
    /// <param name="apply">0から1の進行度を受け取る処理</param>
    /// <param name="cancellation">中断トークン</param>
    /// <returns>演出が終わるまでの待機</returns>
    /// <example>落下中の位置と回転を同時に補間する</example>
    private static async UniTask AnimateAsync(float duration, Action<float> apply, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var elapsed = 0f;
        apply(0f);
        while (elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
            elapsed += Time.unscaledDeltaTime;
            apply(Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
        }
    }

    /// <summary>演出の資源とカメラ設定を解除し、元の視点と音へ戻す</summary>
    /// <example>帰還失敗やシーン破棄でも呼ぶ</example>
    public void Deactivate()
    {
        _particles?.Dispose();
        _particles = null;
        _audio?.Dispose();
        _audio = null;
        _creature?.Dispose();
        _creature = null;
        if (_screen != null) { _screen.Clear(); Destroy(_screen); }
        _screen = null;
        if (!_activated) return;
        _activated = false;
        CapturePhase = "Idle";
        if (_camera != null)
        {
            _camera.Priority = _inactivePriority;
            _camera.Lens = _originalLens;
        }
        transform.SetPositionAndRotation(_originalPosition, _originalRotation);
        for (var i = 0; i < _cameraControllers.Length; i++)
            if (_cameraControllers[i] != null) _cameraControllers[i].enabled = _controllerEnabled[i];
        for (var i = 0; i < _playerRenderers.Length; i++)
            if (_playerRenderers[i] != null) _playerRenderers[i].enabled = _playerEnabled[i];
    }

    /// <summary>シーン終了時にもフィルターと一時描画資源を残さない</summary>
    /// <example>Unityがオブジェクト破棄時に呼ぶ</example>
    private void OnDestroy() => Deactivate();
}

using System;
using Enemy;
using _Project.Scripts.Core;
using _Project.Scripts.Enemy;
using R3;
using FMOD.Studio;
using FMODUnity;
using FMODSettings;
using UnityEngine;
using VContainer.Unity;

/// <summary>昼夜と発見状態に応じて敵の追跡を制御する</summary>
public sealed class VisionEnemyController : IInitializable, ITickable, IDisposable
{
    private readonly VisionEnemyView _view;
    private readonly VisionEnemyDetectSensor _detectSensor;
    private readonly VisionEnemyDeathSensor _deathSensor;
    private readonly IPlayerPosition _player;
    private readonly GameProgress _progress;
    private readonly VisionEnemyNavigator _navigator;
    private IDisposable _timeSubscription;
    private IDisposable _detectedSubscription;
    private IDisposable _lostSubscription;
    private bool _isNight;
    private bool _hasDetectedPlayer;
    private float _chaseElapsed;
    private float _rediscoveryRemaining;
    private bool _isCapturing;
    private enum EscapePhase { None, FadingOut, Hidden, FadingIn }
    private EscapePhase _escapePhase;
    private float _escapeElapsed;
    private EventInstance _heartbeat;
    private bool _heartbeatFailed;
    private EventInstance _closeEnemy;
    private bool _closeEnemyFailed;
    private EnemyStaticOverlay _staticOverlay;
    private HeartbeatRippleView _heartbeatRipple;

    /// <summary>シーンの敵、プレイヤー、進行状態を受け取る</summary>
    /// <param name="view">敵の見た目</param>
    /// <param name="detectSensor">プレイヤーの発見と見失いを通知するSensor</param>
    /// <param name="deathSensor">接触を通知するSensor</param>
    /// <param name="player">追跡対象の位置</param>
    /// <param name="progress">現在の時間帯と変更通知</param>
    /// <example>FishingSceneLifetimeScopeのEntryPointとして生成する</example>
    public VisionEnemyController(VisionEnemyView view,
        VisionEnemyDetectSensor detectSensor, VisionEnemyDeathSensor deathSensor,
        IPlayerPosition player, GameProgress progress,
        VisionEnemyNavigator  navigator)
    {
        _view = view;
        _detectSensor = detectSensor;
        _deathSensor = deathSensor;
        _player = player;
        _progress = progress;
        _navigator = navigator;
    }

    /// <summary>昼夜と発見通知を購読し、現在の状態を反映する</summary>
    /// <example>VContainerがシーン構築時に呼ぶ</example>
    public void Initialize()
    {
        _staticOverlay = new EnemyStaticOverlay(_view.gameObject.scene);
        _view.SetVisible(false);
        _detectSensor.SetSensing(false);
        _deathSensor.SetSensing(false);
        _detectedSubscription = _detectSensor.DetectCollisionEnter.Subscribe(_ =>
        {
            if (_escapePhase == EscapePhase.None && _rediscoveryRemaining <= 0f) _hasDetectedPlayer = true;
        });
        _lostSubscription = _detectSensor.DetectCollisionExit.Subscribe(_ => _hasDetectedPlayer = false);
        ApplyTimeOfDay(_progress.CurrentTimeOfDay);
        _timeSubscription = _progress.TimeOfDayChanged.Subscribe(ApplyTimeOfDay);
    }

    /// <summary>夜にプレイヤーを発見している間だけ追跡させる</summary>
    /// <example>VContainerが毎フレーム呼ぶ</example>
    public void Tick()
    {
        // 敵が非表示の間も残ったノイズのフェードを進める
        _staticOverlay?.Tick(Time.deltaTime);

        // 消失から再出現が終わるまでは追跡も接触判定も再開しない
        if (_escapePhase != EscapePhase.None)
        {
            if (_isNight && !_isCapturing) UpdateEscape(Time.deltaTime);
            return;
        }

        // ポーズ中は追跡時間と再発見までの猶予を進めない
        _rediscoveryRemaining = Mathf.Max(0f, _rediscoveryRemaining - Time.deltaTime);
        if (_isNight && !_isCapturing && _rediscoveryRemaining <= 0f && _detectSensor.PlayerInside)
            _hasDetectedPlayer = true;

        if (!_isNight || _isCapturing || !_hasDetectedPlayer || _player.PlayerPosition == null)
        {
            _chaseElapsed = 0f;
            _navigator.SetActive(false);
            StopHeartbeat();
            StopCloseEnemy();
            _staticOverlay?.BeginFadeOut(_view.StaticFadeOutDuration);
            return;
        }

        // 制限時間を逃げ切ったら経路を破棄し、範囲内でも一定時間は再追跡しない
        _chaseElapsed += Time.deltaTime;
        if (_chaseElapsed >= _view.ChaseDuration)
        {
            BeginEscape();
            return;
        }

        _navigator.SetActive(true);
        _navigator.SetDestination(_player.PlayerPosition);
        UpdateHeartbeat();
        UpdateCloseEnemy();
        _staticOverlay?.SetIntensity(_view.GetStaticIntensity(
            Vector3.Distance(_view.Position, _player.PlayerPosition.position)), _view.GlitchStrength);
    }

    /// <summary>逃げ切り時に攻撃と移動を止め、透明化を始める</summary>
    /// <example>追跡時間がChaseDurationに達したフレームで呼ぶ</example>
    private void BeginEscape()
    {
        _hasDetectedPlayer = false;
        _chaseElapsed = 0f;
        _escapeElapsed = 0f;
        _escapePhase = EscapePhase.FadingOut;
        _detectSensor.SetSensing(false);
        _deathSensor.SetSensing(false);
        _navigator.SetActive(false);
        StopHeartbeat();
        StopCloseEnemy();
        _view.SetCloakOpacity(1f);
    }

    /// <summary>透明化、再配置待ち、再出現をゲーム内時間で順に進める</summary>
    /// <param name="deltaTime">ポーズ中は0になる経過秒数</param>
    /// <example>逃走演出中のTickから呼ぶ</example>
    private void UpdateEscape(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        _escapeElapsed += deltaTime;
        switch (_escapePhase)
        {
            case EscapePhase.FadingOut:
                _view.SetCloakOpacity(1f - Mathf.Clamp01(_escapeElapsed / _view.CloakFadeDuration));
                if (_escapeElapsed < _view.CloakFadeDuration) return;
                _view.SetVisible(false);
                _staticOverlay?.BeginFadeOut(_view.StaticFadeOutDuration);
                _escapePhase = EscapePhase.Hidden;
                _escapeElapsed = 0f;
                _rediscoveryRemaining = _view.RediscoveryDelay;
                break;

            case EscapePhase.Hidden:
                _rediscoveryRemaining -= deltaTime;
                if (_rediscoveryRemaining > 0f) return;
                // 再配置失敗時は姿を戻さず、毎フレームの経路探索も避ける
                _rediscoveryRemaining = 1f;
                if (_player.PlayerPosition == null || !_navigator.TryRespawn(_player.PlayerPosition.position)) return;
                _view.SetCloakOpacity(0f);
                _view.SetVisible(true);
                _escapePhase = EscapePhase.FadingIn;
                _escapeElapsed = 0f;
                break;

            case EscapePhase.FadingIn:
                _view.SetCloakOpacity(Mathf.Clamp01(_escapeElapsed / _view.CloakFadeDuration));
                if (_escapeElapsed < _view.CloakFadeDuration) return;
                _view.RestoreCloakMaterials();
                _escapePhase = EscapePhase.None;
                _rediscoveryRemaining = 0f;
                _detectSensor.SetSensing(true);
                _deathSensor.SetSensing(true);
                break;
        }
    }

    /// <summary>発見中のBGMをリスナー位置でループ再生し、敵との距離減衰を防ぐ</summary>
    /// <example>Tickから呼び、ポーズ中は再生位置を保持する</example>
    private void UpdateCloseEnemy()
    {
        if (_closeEnemyFailed) return;
        try
        {
            // BGMと同様に距離減衰の基準位置へ追従し、敵との距離に音量を左右させない
            var result = RuntimeManager.StudioSystem.getListenerAttributes(0, out var attributes,
                out var attenuationPosition);
            if (result != FMOD.RESULT.OK) return;
            attributes.position = attenuationPosition;

            // 発見中は同じインスタンスを維持し、多重再生を防ぐ
            var starting = !_closeEnemy.isValid();
            if (starting) _closeEnemy = RuntimeManager.CreateInstance(FMODEventPath.BGM_CLOSE_ENEMY.Reference);

            _closeEnemy.set3DAttributes(attributes);
            _closeEnemy.setPaused(Time.timeScale == 0f);
            if (starting) _closeEnemy.start();
        }
        catch (Exception exception)
        {
            StopCloseEnemy();
            _closeEnemyFailed = true;
            Debug.LogException(exception);
        }
    }

    /// <summary>接近音を停止して解放し、次の発見で再生できる状態に戻す</summary>
    /// <example>離脱、見失い、捕獲、昼への変更、シーン破棄で呼ぶ</example>
    private void StopCloseEnemy()
    {
        if (RuntimeManager.IsInitialized && _closeEnemy.isValid())
        {
            _closeEnemy.stop(STOP_MODE.IMMEDIATE);
            _closeEnemy.release();
        }

        _closeEnemy.clearHandle();
        _closeEnemyFailed = false;
    }

    /// <summary>心音を一つだけ再生し、接近に合わせて速度兼ピッチと音量を更新する</summary>
    /// <example>発見中のTickから呼び、ポーズ中は再生位置を保持する</example>
    private void UpdateHeartbeat()
    {
        if (_heartbeatFailed) return;

        var levels = _view.GetHeartbeatLevels(Vector3.Distance(_view.Position, _player.PlayerPosition.position));
        try
        {
            // 同じイベントを維持し、毎フレーム先頭から再生し直さない
            var starting = !_heartbeat.isValid();
            if (starting) _heartbeat = RuntimeManager.CreateInstance(FMODEventPath.SE_HEART_BEAT.Reference);

            _heartbeat.setPitch(levels.x);
            _heartbeat.setVolume(levels.y);
            _heartbeat.setPaused(Time.timeScale == 0f);
            if (starting) _heartbeat.start();

            // 心音の実波形から拍動を拾い、メインカメラに波を渡す
            if (_heartbeatRipple == null && Camera.main != null)
                _heartbeatRipple = Camera.main.gameObject.AddComponent<HeartbeatRippleView>();
            if (_heartbeatRipple != null)
                _heartbeatRipple.UpdateHeartbeat(_heartbeat, levels,
                    _view.HeartbeatRippleStrength, _view.HeartbeatRippleThreshold);
        }
        catch (Exception exception)
        {
            StopHeartbeat();
            _heartbeatFailed = true;
            Debug.LogException(exception);
        }
    }

    /// <summary>心音を停止して解放し、次回の発見で再生できる状態に戻す</summary>
    /// <example>見失い、昼への変更、捕獲、シーン破棄で呼ぶ</example>
    private void StopHeartbeat()
    {
        if (_heartbeatRipple != null) _heartbeatRipple.Clear();
        if (RuntimeManager.IsInitialized && _heartbeat.isValid())
        {
            _heartbeat.stop(STOP_MODE.IMMEDIATE);
            _heartbeat.release();
        }

        _heartbeat.clearHandle();
        _heartbeatFailed = false;
    }

    /// <summary>昼夜変更に応じて敵の姿と追跡可否を切り替える</summary>
    /// <param name="timeOfDay">変更後の時間帯</param>
    /// <example>GameProgress.StartNightで敵を出現させる</example>
    private void ApplyTimeOfDay(TimeOfDay timeOfDay)
    {
        _staticOverlay?.BeginFadeOut(_view.StaticFadeOutDuration);
        _isNight = timeOfDay == TimeOfDay.Night;
        StopHeartbeat();
        StopCloseEnemy();
        _hasDetectedPlayer = false;
        _escapePhase = EscapePhase.None;
        _escapeElapsed = 0f;
        _view.RestoreCloakMaterials();
        _view.SetVisible(_isNight);
        _chaseElapsed = 0f;
        _rediscoveryRemaining = 0f;
        _detectSensor.SetSensing(_isNight && !_isCapturing);
        _deathSensor.SetSensing(_isNight && !_isCapturing);
        _navigator.SetActive(false);
    }

    /// <summary>捕獲演出中の再接触と通常の追跡を止める</summary>
    /// <example>GameOverPresenterが最初の接触通知で呼ぶ</example>
    public void BeginCapture()
    {
        _staticOverlay?.Clear();
        if (_isCapturing) return;
        _isCapturing = true;
        StopHeartbeat();
        StopCloseEnemy();
        _hasDetectedPlayer = false;
        _detectSensor.SetSensing(false);
        _deathSensor.SetSensing(false);
        _navigator.SetActive(false);
    }

    /// <summary>演出を途中で終了した場合に昼夜の状態へ戻す</summary>
    /// <example>CampStageの読み込みに失敗した場合に呼ぶ</example>
    public void EndCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        ApplyTimeOfDay(_progress.CurrentTimeOfDay);
    }

    /// <summary>昼夜と発見通知の購読を終了する</summary>
    /// <example>FishingStageから離れるときにVContainerが呼ぶ</example>
    public void Dispose()
    {
        _staticOverlay?.Dispose();
        _staticOverlay = null;
        StopHeartbeat();
        StopCloseEnemy();
        _timeSubscription?.Dispose();
        if (_heartbeatRipple != null) UnityEngine.Object.Destroy(_heartbeatRipple);
        _detectedSubscription?.Dispose();
        _lostSubscription?.Dispose();
    }
}

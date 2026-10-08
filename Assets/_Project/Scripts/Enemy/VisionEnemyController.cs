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
    private bool _isCapturing;
    private EventInstance _heartbeat;
    private bool _heartbeatFailed;
    private EventInstance _closeEnemy;
    private bool _closeEnemyFailed;

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
        _view.SetVisible(false);
        _detectSensor.SetSensing(false);
        _deathSensor.SetSensing(false);
        _detectedSubscription = _detectSensor.DetectCollisionEnter.Subscribe(_ => _hasDetectedPlayer = true);
        _lostSubscription = _detectSensor.DetectCollisionExit.Subscribe(_ => _hasDetectedPlayer = false);
        ApplyTimeOfDay(_progress.CurrentTimeOfDay);
        _timeSubscription = _progress.TimeOfDayChanged.Subscribe(ApplyTimeOfDay);
    }

    /// <summary>夜にプレイヤーを発見している間だけ追跡させる</summary>
    /// <example>VContainerが毎フレーム呼ぶ</example>
    public void Tick()
    {
        if (!_isNight || _isCapturing || !_hasDetectedPlayer || _player.PlayerPosition == null)
        {
            StopHeartbeat();
            StopCloseEnemy();
            return;
        }

        _navigator.SetDestination(_player.PlayerPosition);
        UpdateHeartbeat();
        UpdateCloseEnemy();
    }

    /// <summary>追跡中の近距離だけ接近音をループ再生する</summary>
    /// <example>Tickから呼び、ポーズ中は再生位置を保持する</example>
    private void UpdateCloseEnemy()
    {
        var distance = Vector3.Distance(_view.Position, _player.PlayerPosition.position);
        if (!_view.ShouldPlayCloseEnemy(distance, _closeEnemy.isValid()))
        {
            StopCloseEnemy();
            return;
        }

        if (_closeEnemyFailed) return;
        try
        {
            // 範囲内では同じインスタンスを維持し、多重再生を防ぐ
            var starting = !_closeEnemy.isValid();
            if (starting) _closeEnemy = RuntimeManager.CreateInstance(FMODEventPath.SE_CLOSE_ENEMY.Reference);
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

    /// <summary>接近音を停止して解放し、次の接近で再生できる状態に戻す</summary>
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
        _isNight = timeOfDay == TimeOfDay.Night;
        StopHeartbeat();
        StopCloseEnemy();
        _hasDetectedPlayer = false;
        _view.SetVisible(_isNight);
        _detectSensor.SetSensing(_isNight && !_isCapturing);
        _deathSensor.SetSensing(_isNight && !_isCapturing);
        _navigator.SetActive(_isNight && !_isCapturing);
    }

    /// <summary>捕獲演出中の再接触と通常の追跡を止める</summary>
    /// <example>GameOverPresenterが最初の接触通知で呼ぶ</example>
    public void BeginCapture()
    {
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
        StopHeartbeat();
        StopCloseEnemy();
        _timeSubscription?.Dispose();
        _detectedSubscription?.Dispose();
        _lostSubscription?.Dispose();
    }
}

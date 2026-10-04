using System;
using _Project.Scripts.Enemy;
using _Project.Scripts.Core;
using R3;
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
        if (!_isNight || !_hasDetectedPlayer || _player.PlayerPosition == null) return;

        _navigator.SetDestination(_player.PlayerPosition);
    }

    /// <summary>昼夜変更に応じて敵の姿と追跡可否を切り替える</summary>
    /// <param name="timeOfDay">変更後の時間帯</param>
    /// <example>GameProgress.StartNightで敵を出現させる</example>
    private void ApplyTimeOfDay(TimeOfDay timeOfDay)
    {
        _isNight = timeOfDay == TimeOfDay.Night;
        _hasDetectedPlayer = false;
        _view.SetVisible(_isNight);
        _detectSensor.SetSensing(_isNight);
        _deathSensor.SetSensing(_isNight);
        _navigator.SetActive(_isNight);
    }

    /// <summary>昼夜と発見通知の購読を終了する</summary>
    /// <example>FishingStageから離れるときにVContainerが呼ぶ</example>
    public void Dispose()
    {
        _timeSubscription?.Dispose();
        _detectedSubscription?.Dispose();
        _lostSubscription?.Dispose();
    }
}

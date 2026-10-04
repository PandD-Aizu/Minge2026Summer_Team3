using System;
using _Project.Scripts.Core;
using R3;
using UnityEngine;
using VContainer.Unity;

/// <summary>夜だけ敵を表示してプレイヤーを追跡させる</summary>
public sealed class VisionEnemyController : IInitializable, ITickable, IDisposable
{
    private readonly VisionEnemyView _view;
    private readonly IPlayerPosition _player;
    private readonly GameProgress _progress;
    private IDisposable _timeSubscription;
    private bool _isNight;

    /// <summary>シーンの敵、プレイヤー、進行状態を受け取る</summary>
    /// <param name="view">敵の表示と移動</param>
    /// <param name="player">追跡対象の位置</param>
    /// <param name="progress">現在の時間帯と変更通知</param>
    /// <example>FishingSceneLifetimeScopeのEntryPointとして生成する</example>
    public VisionEnemyController(VisionEnemyView view, IPlayerPosition player, GameProgress progress)
    {
        _view = view;
        _player = player;
        _progress = progress;
    }

    /// <summary>現在の時間帯を反映し、以後の変更を購読する</summary>
    /// <example>VContainerがシーン構築時に呼ぶ</example>
    public void Initialize()
    {
        _view.SetVisible(false);
        ApplyTimeOfDay(_progress.CurrentTimeOfDay);
        _timeSubscription = _progress.TimeOfDayChanged.Subscribe(ApplyTimeOfDay);
    }

    /// <summary>夜だけプレイヤーへ近づく</summary>
    /// <example>VContainerが毎フレーム呼ぶ</example>
    public void Tick()
    {
        if (!_isNight || _player.PlayerPosition == null) return;

        Vector3 offset = _player.PlayerPosition.position - _view.Position;
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance <= _view.StopDistance) return;

        float step = Mathf.Min(_view.MoveSpeed * Time.deltaTime, distance - _view.StopDistance);
        _view.Move(offset / distance * step);
    }

    /// <summary>昼夜変更に応じて敵の姿と追跡可否を切り替える</summary>
    /// <param name="timeOfDay">変更後の時間帯</param>
    /// <example>GameProgress.StartNightで敵を出現させる</example>
    private void ApplyTimeOfDay(TimeOfDay timeOfDay)
    {
        _isNight = timeOfDay == TimeOfDay.Night;
        _view.SetVisible(_isNight);
    }

    /// <summary>時間帯変更の購読を終了する</summary>
    /// <example>FishingStageから離れるときにVContainerが呼ぶ</example>
    public void Dispose()
    {
        _timeSubscription?.Dispose();
    }
}

using System;
using System.Threading;
using _Project.Scripts.Core;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer.Unity;

/// <summary>夜だけ敵を追跡させ、接触後の演出と再出現を管理する</summary>
public sealed class VisionEnemyController : IInitializable, ITickable, IDisposable
{
    private readonly VisionEnemyView _view;
    private readonly IPlayerPosition _player;
    private readonly PlayerInputReader _input;
    private readonly GameProgress _progress;
    private readonly CameraShaker _cameraShaker;
    private readonly GameOverView _gameOverView;
    private readonly CancellationTokenSource _lifetime = new();
    private IDisposable _timeSubscription;
    private IDisposable _inputBlock;
    private CancellationTokenSource _recoveryCancellation;
    private Vector3 _initialPosition;
    private bool _isNight;
    private bool _isRecovering;

    /// <summary>シーンの敵、プレイヤー、進行状態、演出を受け取る</summary>
    /// <param name="view">敵の表示と接触通知</param>
    /// <param name="player">追跡対象の位置</param>
    /// <param name="input">接触中に操作を止める入力窓口</param>
    /// <param name="progress">現在の時間帯と変更通知</param>
    /// <param name="cameraShaker">接触時のカメラ演出</param>
    /// <param name="gameOverView">一時的なGameOver表示</param>
    /// <example>FishingSceneLifetimeScopeのEntryPointとして生成する</example>
    public VisionEnemyController(VisionEnemyView view, IPlayerPosition player, PlayerInputReader input,
        GameProgress progress, CameraShaker cameraShaker, GameOverView gameOverView)
    {
        _view = view;
        _player = player;
        _input = input;
        _progress = progress;
        _cameraShaker = cameraShaker;
        _gameOverView = gameOverView;
    }

    /// <summary>初期位置を記録し、現在の時間帯と接触通知を反映する</summary>
    /// <example>VContainerがシーン構築時に呼ぶ</example>
    public void Initialize()
    {
        _initialPosition = _view.Position;
        _view.SetVisible(false);
        _view.PlayerTouched += OnPlayerTouched;
        ApplyTimeOfDay(_progress.CurrentTimeOfDay);
        _timeSubscription = _progress.TimeOfDayChanged.Subscribe(ApplyTimeOfDay);
    }

    /// <summary>夜で操作可能な間だけプレイヤーへ近づく</summary>
    /// <example>VContainerが毎フレーム呼ぶ</example>
    public void Tick()
    {
        if (!_isNight || _isRecovering || _player.PlayerPosition == null) return;

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
        if (!_isNight) _recoveryCancellation?.Cancel();
        _view.SetVisible(_isNight && !_isRecovering);
    }

    /// <summary>操作できるプレイヤーとの接触を一度だけ処理する</summary>
    /// <param name="touched">接触した位置提供者</param>
    /// <example>VisionEnemyViewのTrigger通知から呼ぶ</example>
    private void OnPlayerTouched(IPlayerPosition touched)
    {
        if (!_isNight || _isRecovering || !ReferenceEquals(touched, _player) ||
            !_input.CanStartGameplayAction) return;

        _isRecovering = true;
        _view.SetVisible(false);
        _inputBlock = _input.BlockGameplayInput();
        _cameraShaker.TriggerShake();
        _gameOverView.Show();
        _recoveryCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        RecoverAsync(_recoveryCancellation).Forget();
    }

    /// <summary>表示時間の後に敵を離れた場所へ戻し、プレイヤーの操作を再開する</summary>
    /// <param name="cancellation">昼への変更またはシーン破棄で終了するトークン</param>
    /// <returns>復帰演出の終了を待つ非同期処理</returns>
    /// <example>敵と接触した直後に開始する</example>
    private async UniTask RecoverAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_view.GameOverSeconds),
                DelayType.UnscaledDeltaTime, cancellationToken: cancellation.Token);
            RespawnAwayFromPlayer();
        }
        catch (OperationCanceledException)
        {
            // 昼への変更やシーン破棄では再出現させない
        }
        finally
        {
            if (_gameOverView != null) _gameOverView.Hide();
            _inputBlock?.Dispose();
            _inputBlock = null;
            _isRecovering = false;
            if (_view != null) _view.SetVisible(_isNight && !cancellation.IsCancellationRequested);
            if (_recoveryCancellation == cancellation) _recoveryCancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>プレイヤーから離れた歩行可能な地面を探して敵を配置する</summary>
    /// <example>GameOver表示の終了時に呼ぶ</example>
    private void RespawnAwayFromPlayer()
    {
        Vector3 playerPosition = _player.PlayerPosition.position;
        Vector3 away = _view.Position - playerPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = _initialPosition - playerPosition;
        if (away.sqrMagnitude < 0.01f) away = Vector3.right;
        away.Normalize();

        // 海のColliderや崖を避け、複数の方向と距離で地面を探す
        for (int radiusStep = 0; radiusStep < 3; radiusStep++)
        {
            float radius = _view.RespawnDistance + radiusStep * 3f;
            for (int directionStep = 0; directionStep < 8; directionStep++)
            {
                Vector3 direction = Quaternion.Euler(0f, directionStep * 45f, 0f) * away;
                Vector3 candidate = playerPosition + direction * radius;
                Vector3 rayOrigin = candidate + Vector3.up * 20f;
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 50f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.7f || hit.point.y < playerPosition.y - 0.5f ||
                    hit.point.y > playerPosition.y + 2f) continue;

                _view.Respawn(new Vector3(candidate.x, _initialPosition.y, candidate.z));
                return;
            }
        }

        // 地面が見つからない場合も同じ位置で即再接触しない距離を保つ
        Vector3 fallback = _initialPosition;
        Vector3 separation = fallback - playerPosition;
        separation.y = 0f;
        if (separation.sqrMagnitude < _view.RespawnDistance * _view.RespawnDistance)
        {
            fallback = playerPosition + away * _view.RespawnDistance;
            fallback.y = _initialPosition.y;
        }

        _view.Respawn(fallback);
    }

    /// <summary>購読と進行中の復帰演出を終了する</summary>
    /// <example>FishingStageから離れるときにVContainerが呼ぶ</example>
    public void Dispose()
    {
        _view.PlayerTouched -= OnPlayerTouched;
        _timeSubscription?.Dispose();
        _lifetime.Cancel();
        _recoveryCancellation?.Cancel();
        _inputBlock?.Dispose();
        _inputBlock = null;
        if (_gameOverView != null) _gameOverView.Hide();
        _lifetime.Dispose();
    }
}

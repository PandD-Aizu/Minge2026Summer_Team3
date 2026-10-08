using System;
using System.Threading;
using _Project.Scripts.Core;
using _Project.Scripts.Data.Enum;
using Cysharp.Threading.Tasks;
using Enemy;
using FMODServices;
using FMODSettings;
using GameOver;
using R3;
using SceneLoadServices;
using UnityEngine;
using VContainer.Unity;

/// <summary>敵の捕獲通知から演出、CampStageへの帰還までを順に実行する</summary>
public sealed class GameOverPresenter : IInitializable, IDisposable
{
    private readonly VisionEnemyDeathSensor _sensor;
    private readonly VisionEnemyController _enemyController;
    private readonly VisionEnemyView _enemyView;
    private readonly GameOverView _view;
    private readonly GameOverCameraView _camera;
    private readonly CameraShaker _shaker;
    private readonly PlayerInputReader _input;
    private readonly GameProgress _progress;
    private readonly SceneLoadService _sceneLoader;
    private readonly FMODSEService _se;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    private IDisposable _deathSubscription;
    private IDisposable _inputBlock;
    private bool _capturing;
    private bool _disposed;

    /// <summary>SceneとRootに登録された捕獲演出の依存関係を受け取る</summary>
    /// <param name="sensor">敵との接触通知</param>
    /// <param name="enemyController">通常追跡の停止</param>
    /// <param name="enemyView">敵の接近と突進</param>
    /// <param name="view">暗転Canvas</param>
    /// <param name="camera">一人称視点の仮想カメラ</param>
    /// <param name="shaker">Cinemachineの揺れ</param>
    /// <param name="input">主人公の操作</param>
    /// <param name="progress">帰還時の一回限りの会話予約</param>
    /// <param name="sceneLoader">CampStageの読み込み</param>
    /// <param name="se">死亡音の再生</param>
    public GameOverPresenter(VisionEnemyDeathSensor sensor, VisionEnemyController enemyController,
        VisionEnemyView enemyView, GameOverView view, GameOverCameraView camera,
        CameraShaker shaker, PlayerInputReader input, GameProgress progress,
        SceneLoadService sceneLoader, FMODSEService se)
    {
        _sensor = sensor;
        _enemyController = enemyController;
        _enemyView = enemyView;
        _view = view;
        _camera = camera;
        _shaker = shaker;
        _input = input;
        _progress = progress;
        _sceneLoader = sceneLoader;
        _se = se;
    }

    /// <summary>捕獲用Sensorの通知を一度だけ購読する</summary>
    /// <example>FishingSceneLifetimeScopeのEntryPointから呼ばれる</example>
    public void Initialize()
    {
        _deathSubscription = _sensor.DeathCollisionEnter.Subscribe(OnDeathCollisionEnter);
    }

    /// <summary>同時接触による演出の多重起動を防ぐ</summary>
    /// <param name="type">接触した敵の種類</param>
    private void OnDeathCollisionEnter(EnemyType type)
    {
        if (type != EnemyType.Vision || _capturing || _disposed) return;
        _capturing = true;
        RunCaptureAsync(_lifetimeCancellation.Token).Forget();
    }

    /// <summary>軽い揺れ、暗転、一人称の接近、突進、帰還を順番に行う</summary>
    /// <param name="cancellation">Scene破棄時の中断トークン</param>
    /// <returns>捕獲演出とScene読み込みの完了まで待つUniTask</returns>
    private async UniTask RunCaptureAsync(CancellationToken cancellation)
    {
        var loadedCamp = false;
        try
        {
            _enemyController.BeginCapture();
            _inputBlock = _input.BlockGameplayInput();
            _shaker.TriggerShake();
            await _view.FadeToBlackAsync(cancellation);

            await _camera.ActivateAsync(cancellation);
            _enemyView.PrepareCapture(_camera.transform.position);
            await _view.FadeToWhiteAsync(cancellation);

            await _enemyView.ApproachCaptureAsync(cancellation);
            _shaker.TriggerBigShake();
            await _enemyView.LungeCaptureAsync(cancellation);
            _se.PlayOneShot(FMODEventPath.SE_PLAYER_DEATH.Reference);
            await _view.FadeToBlackAsync(cancellation);

            // 帰還後の会話はSceneを読み込む前に予約する
            _progress.MarkCaptureReturn();
            loadedCamp = await _sceneLoader.LoadSceneAsync("CampStage");
            if (!loadedCamp) _progress.ClearCaptureReturn();
        }
        catch (OperationCanceledException)
        {
            _progress.ClearCaptureReturn();
        }
        catch (Exception exception)
        {
            _progress.ClearCaptureReturn();
            Debug.LogException(exception);
        }
        finally
        {
            _inputBlock?.Dispose();
            _inputBlock = null;

            if (!loadedCamp)
            {
                if (_view != null) _view.ClearImmediately();
                if (!_disposed)
                {
                    if (_camera != null) _camera.Deactivate();
                    if (_enemyView != null) _enemyView.RestoreCaptureVisuals();
                    _enemyController.EndCapture();
                }
                _capturing = false;
            }
        }
    }

    /// <summary>Sceneを離れる際に購読と入力制限を解放する</summary>
    /// <example>FishingSceneLifetimeScopeの破棄時に呼ばれる</example>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _deathSubscription?.Dispose();
        _lifetimeCancellation.Cancel();
        _inputBlock?.Dispose();
        _inputBlock = null;
        _lifetimeCancellation.Dispose();
    }
}

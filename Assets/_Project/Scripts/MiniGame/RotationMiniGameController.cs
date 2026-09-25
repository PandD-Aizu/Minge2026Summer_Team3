using System;
using Input;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniGame
{
public sealed class RotationMiniGameController : IDisposable
{
    private readonly RotationMiniGameView _view;
    private readonly RotationMiniGameSettings _settings;
    private readonly PlayerInputAction _input;
    private readonly InputModeService _inputMode;
    private readonly MiniGameResult _result;

    private readonly Subject<MiniGameResult> _completed = new();
    private readonly Subject<Unit> _canceled = new();

    public Observable<MiniGameResult> Completed => _completed;
    public Observable<Unit> Canceled => _canceled;

    private bool _isPlaying;
    private bool _hasStopped;
    private bool _wasPlayerEnabled;
    private bool _isDisposed;

    public RotationMiniGameController(
        RotationMiniGameView view,
        RotationMiniGameSettings settings,
        PlayerInputAction input, InputModeService inputMode)
    {
        _view = view;
        _settings = settings;
        _input = input;
        _inputMode = inputMode;

        _input.MiniGame.Stop.performed += OnStop;
        _input.MiniGame.Cancel.performed += OnCancel;
    }

    /// <summary>
    /// ミニゲームを開始する。
    /// </summary>
    public void StartGame()
    {
        if (_isDisposed || _isPlaying) return;

        _isPlaying = true;
        _hasStopped = false;

        _wasPlayerEnabled = _input.Player.enabled;

        _view.ShowMiniGameCanvas();
        _view.StartRotatingPin();

        _inputMode.SwitchToMiniGame();

    }

    /// <summary>
    /// Jキーで針を止め、結果を通知する。
    /// </summary>
    private void OnStop(InputAction.CallbackContext context)
    {
        if (_isDisposed || !_isPlaying || _hasStopped) return;

        _hasStopped = true;
        _view.StopRotatingPin();

        // 操作説明の文字を消して、入力を受け付けない
        _view.HideText();
        _inputMode.DisableAllInputs();

        float angle = _view.GetPinAngle();
        MiniGameResult result = Judge(angle);

        _completed.OnNext(result);
    }

    /// <summary>
    /// 土台を基準にした角度から結果を判定する。
    /// Great、Goodの順に反時計回りに並ぶ配置を想定。
    /// </summary>
    private MiniGameResult Judge(float angle)
    {
        angle = Mathf.Repeat(angle, 360f);

        if (angle < _settings.GreatAngle)
            return MiniGameResult.Great;

        if (angle < _settings.GoodAngle)
            return MiniGameResult.Good;

        return MiniGameResult.Miss;
    }

    /// <summary>
    /// Escキーで画面を閉じる。
    /// 判定前なら中断として通知する。
    /// </summary>
    private void OnCancel(InputAction.CallbackContext context)
    {
        if (_isDisposed || !_isPlaying) return;

        bool wasCanceled = !_hasStopped;

        EndGame();

        if (wasCanceled)
            _canceled.OnNext(Unit.Default);
    }

    /// <summary>
    /// 画面を閉じ、元の入力状態に戻す。
    /// </summary>
    public void EndGame()
    {
        if (_isDisposed || !_isPlaying) return;

        _isPlaying = false;

        _inputMode.DisableAllInputs();

        _view.StopRotatingPin();
        _view.HideMiniGameCanvas();

        if (_wasPlayerEnabled)
            _inputMode.SwitchToPlayer();
    }

    /// <summary>
    /// 入力の購読解除と、通知用Subjectの破棄。
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _input.MiniGame.Stop.performed -= OnStop;
        _input.MiniGame.Cancel.performed -= OnCancel;

        if (_isPlaying)
        {
            _isPlaying = false;
            _input.MiniGame.Disable();

            if (_view != null)
            {
                _view.StopRotatingPin();
                _view.HideMiniGameCanvas();
            }
        }

        _completed.Dispose();
        _canceled.Dispose();

    }
}
}

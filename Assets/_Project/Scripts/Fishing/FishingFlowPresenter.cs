using System;
using System.Threading;
using _Project.Scripts.Data.Fish;
using Controller;
using Cysharp.Threading.Tasks;
using FMODServices;
using FMODSettings;
using Input;
using MiniGame;
using R3;
using UnityEngine;

namespace _Project.Scripts.Fishing
{
    public enum FishingState { Idle, Casting, Waiting, Biting, Playing, Result }

    /// <summary>一回の釣りに必要な対象魚、入力、道具、結果処理の寿命をまとめて所有する</summary>
    public sealed class FishingFlowPresenter : IDisposable
    {
        private readonly PlayerInputReader _reader;
        private readonly PlayerInputAction _input;
        private readonly InputModeService _mode;
        private readonly FishingBiteController _bite;
        private readonly MiniGameFlowPresenter _games;
        private readonly MiniGameResultPresenter _results;
        private readonly TutorialController _tutorial;
        private readonly FMODSEService _se;
        private readonly FishingCastView _cast;
        private CancellationTokenSource _session;
        private bool _disposed;
        public FishingState State { get; private set; }
        public bool IsFishing => _session != null;

        /// <summary>釣りシーンの共通サービスを受け取る</summary>
        /// <param name="reader">移動と外部イベントの操作制限</param>
        /// <param name="input">共有入力</param>
        /// <param name="mode">入力マップ切替</param>
        /// <param name="bite">共通の食いつき</param>
        /// <param name="games">個別ゲームの抽選と実行</param>
        /// <param name="results">魚の獲得と結果表示</param>
        /// <param name="tutorial">初回釣果の進行</param>
        /// <param name="se">キャスト音</param>
        /// <example>FishingSceneLifetimeScopeから生成する</example>
        public FishingFlowPresenter(PlayerInputReader reader, PlayerInputAction input, InputModeService mode,
            FishingBiteController bite, MiniGameFlowPresenter games, MiniGameResultPresenter results,
            TutorialController tutorial, FMODSEService se)
        {
            _reader = reader; _input = input; _mode = mode; _bite = bite;
            _games = games; _results = results; _tutorial = tutorial; _se = se;
            _cast = reader.GetComponent<FishingCastView>();
        }

        /// <summary>魚影を確保し、一回の釣りを開始する</summary>
        /// <param name="fish">キャスト時に確定した魚</param>
        /// <param name="spot">確保する釣り場</param>
        /// <example>インタラクト時にStartFishing(fish, spot)を呼ぶ</example>
        public void StartFishing(FishDefinition fish, FishingSpot spot)
        {
            if (_disposed || IsFishing || !_reader.CanStartGameplayAction || fish == null || spot == null) return;
            if (_cast == null || !_cast.IsConfigured)
            { Debug.LogError("釣り道具の参照を設定してください"); return; }
            var reservation = spot.TryReserve();
            if (reservation == null) return;
            _session = CancellationTokenSource.CreateLinkedTokenSource(_reader.GetCancellationTokenOnDestroy(), spot.GetCancellationTokenOnDestroy());
            RunAsync(fish, spot, reservation, _session).Forget();
        }

        /// <summary>キャストから結果まで進め、すべての終了経路で確保したリソースを解放する</summary>
        /// <param name="fish">対象魚</param>
        /// <param name="spot">対象スポット</param>
        /// <param name="reservation">魚影の確保解除</param>
        /// <param name="session">この実行が所有する中断元</param>
        /// <returns>釣りと後始末の完了までの待機</returns>
        /// <example>StartFishingから一度だけ実行する</example>
        private async UniTask RunAsync(FishDefinition fish, FishingSpot spot, IDisposable reservation, CancellationTokenSource session)
        {
            var token = session.Token;
            bool fishAdded = false;
            bool wasPlayerEnabled = _input.Player.enabled;
            var movement = _reader.BlockMovement();
            // Escは中断、会話や捕獲による操作占有も釣りを終了させる
            using var monitor = Observable.EveryUpdate().Subscribe(_ =>
            {
                if (_reader == null || !_reader.isActiveAndEnabled || _reader.IsGameplayInputBlocked ||
                    spot == null || !spot.isActiveAndEnabled || _cast == null || !_cast.isActiveAndEnabled ||
                    (Time.timeScale > 0 && _input.MiniGame.Cancel.WasPressedThisFrame())) session.Cancel();
            });
            try
            {
                _mode.SwitchToMiniGame();
                State = FishingState.Casting;
                _se.PlayOneShot(FMODEventPath.SE_FISH_ROD_SWING.Reference);
                _cast.BeginCast(spot.CastPosition);
                await UniTask.WaitUntil(() => _cast.HasLanded, cancellationToken: token);

                State = FishingState.Waiting;
                bool hooked = await _bite.PlayAsync(_cast, spot, () => State = FishingState.Biting, token);
                token.ThrowIfCancellationRequested();
                MiniGameResult result = MiniGameResult.Miss;
                if (hooked)
                {
                    State = FishingState.Playing;
                    result = await _games.PlayAsync(fish, token);
                }

                token.ThrowIfCancellationRequested();
                State = FishingState.Result;
                _cast.Hide();
                // ゲームのUIはPlayAsyncのfinallyで閉じられている Escだけは結果中も受け付ける
                fishAdded = await _results.PlayResultAsync(result, fish, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                monitor.Dispose();
                if (_cast != null) _cast.Hide();
                reservation.Dispose();
                movement.Dispose();
                _input.MiniGame.Disable();
                if (!_disposed && wasPlayerEnabled && _reader != null && _reader.isActiveAndEnabled && !_reader.IsGameplayInputBlocked)
                    _mode.SwitchToPlayer();
                _session = null;
                session.Dispose();
                State = FishingState.Idle;
            }

            // 獲得後に結果演出を中断しても、所持品と進行状態を一致させる
            if (fishAdded) _tutorial.NotifyFishCaught();
        }

        /// <summary>シーンの寿命が終わったら進行中の釣りを中断する</summary>
        /// <example>VContainerがシーン終了時に呼ぶ</example>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _session?.Cancel();
        }
    }
}

using System;
using System.Threading;
using _Project.Scripts.Data.Fish;
using _Project.Scripts.View;
using Cysharp.Threading.Tasks;
using InventoryData;
using _Project.Scripts.Core;
using FMODServices;
using FMODSettings;
using MiniGame;
using UnityEngine;

/// <summary>ゲーム種類に依存せず、成績の表示と魚の獲得を行う</summary>
public class MiniGameResultPresenter
{
    private readonly MiniGameResultView _view;
    private readonly Inventory _inventory;
    private readonly FMODSEService _se;
    private readonly IPlayerPosition _player;
    private readonly PlayerInputReader _input;
    private readonly GameProgress _progress;

    /// <summary>釣果の表示、所持品、効果音に必要な依存関係を受け取る</summary>
    /// <param name="view">結果の表示先</param>
    /// <param name="inventory">魚の追加先</param>
    /// <param name="se">結果音</param>
    /// <param name="player">頭上表示の基準</param>
    /// <param name="input">外部イベントの操作制限</param>
    /// <param name="progress">釣果の記録先</param>
    /// <example>FishingSceneLifetimeScopeから生成する</example>
    public MiniGameResultPresenter(MiniGameResultView view, Inventory inventory, FMODSEService se,
        IPlayerPosition player, PlayerInputReader input, GameProgress progress)
    { _view = view; _inventory = inventory; _se = se; _player = player; _input = input; _progress = progress; }

    /// <summary>魚を一度追加し、演出を中断しても獲得の有無を呼び出し側へ返す</summary>
    /// <param name="result">ゲームの成績</param>
    /// <param name="fish">キャスト時に確定した魚</param>
    /// <param name="cancellation">釣り全体の中断トークン</param>
    /// <returns>所持品へ魚を追加した場合はtrue</returns>
    /// <example>await presenter.PlayResultAsync(result, fish, token)</example>
    public async UniTask<bool> PlayResultAsync(MiniGameResult result, FishDefinition fish, CancellationToken cancellation)
    {
        bool fishAdded = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _view.GetCancellationTokenOnDestroy());
        try
        {
            linked.Token.ThrowIfCancellationRequested();
            if (_input.IsGameplayInputBlocked) return false;
            int delay;
            switch (result)
            {
                case MiniGameResult.Great:
                    _view.GreatResult();
                    _se.PlayOneShot(FMODEventPath.SE_FISSHING_GREAT.Reference);
                    delay = 500;
                    break;
                case MiniGameResult.Good:
                    _view.GoodResult();
                    _se.PlayOneShot(FMODEventPath.SE_FISSHING_OK.Reference);
                    delay = 1000;
                    break;
                default:
                    _view.MissResult();
                    _se.PlayOneShot(FMODEventPath.SE_FISSHING_MISS.Reference);
                    delay = 1500;
                    break;
            }

            // 演出の前に獲得を確定し、演出の中断によって二重追加や進行の不一致を起こさない
            if (result != MiniGameResult.Miss && fish != null)
            {
                _inventory.Add(fish.ItemId);
                _progress.RecordFishCaught(fish.ItemId);
                fishAdded = true;
            }

            await UniTask.Delay(delay, cancellationToken: linked.Token);
            _view.HideResult();
            if (fishAdded && !_input.IsGameplayInputBlocked)
                await _view.ShowFishAsync(fish, _player.PlayerPosition, linked.Token);
        }
        catch (OperationCanceledException) { }
        finally { if (_view != null) _view.HideResult(); }
        return fishAdded;
    }
}

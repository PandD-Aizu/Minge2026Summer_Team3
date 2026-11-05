using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MiniGame;
using UnityEngine;

namespace _Project.Scripts.Fishing
{
    /// <summary>着水後に食いつきを待ち、受付時間内の新しい押下で合わせる</summary>
    public sealed class FishingBiteController
    {
        private readonly FishingBiteSettings _settings;
        private readonly PlayerInputAction _input;

        /// <summary>共通設定と入力を受け取る</summary>
        /// <param name="settings">食いつきの時間と浮きの演出</param>
        /// <param name="input">共有入力</param>
        /// <example>VContainerから生成する</example>
        public FishingBiteController(FishingBiteSettings settings, PlayerInputAction input)
        { _settings = settings; _input = input; }

        /// <summary>待機と食いつきを表示し、合わせ成功かを返す</summary>
        /// <param name="view">着水した釣り道具</param>
        /// <param name="spot">泳いで浮きを突っつく魚影を確保したスポット</param>
        /// <param name="onBite">食いつき状態になった際の通知</param>
        /// <param name="cancellation">釣りの中断トークン</param>
        /// <returns>受付時間内の合わせ成功ならtrue、逃げられたらfalse</returns>
        /// <example>await bite.PlayAsync(cast, spot, () => State = FishingState.Biting, token)</example>
        public async UniTask<bool> PlayAsync(FishingCastView view, FishingSpot spot, Action onBite, CancellationToken cancellation)
        {
            float wait = UnityEngine.Random.Range(Mathf.Max(0.1f, _settings.WaitSeconds.x),
                Mathf.Max(_settings.WaitSeconds.x, _settings.WaitSeconds.y));
            // 待っている魚も広い範囲を泳ぎ、頭が浮きへ到着してから合わせを受け付ける
            float elapsed = 0f;
            while (elapsed < wait)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                elapsed += Time.deltaTime;
                spot.SwimWhileWaiting(Time.deltaTime);
            }
            await spot.ApproachBobberAsync(view.WaterPosition, _settings.ApproachSeconds, cancellation);
            cancellation.ThrowIfCancellationRequested();
            view.SetBiting(true, _settings.BobHeight);
            onBite();
            var gate = new MiniGameInputGate();
            // 待機中からの長押しは捨て、浮きの変化後の新しい押下を待つ
            gate.Sample(_input.MiniGame.Stop.IsPressed());
            elapsed = 0f;
            try
            {
                while (elapsed < Mathf.Max(0.1f, _settings.HookWindow))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                    if (Time.deltaTime <= 0) continue;
                    elapsed += Time.deltaTime;

                    // 魚が接触する瞬間に浮きを沈め、離れる瞬間に浮かせる
                    float retreat = (1f - Mathf.Cos(elapsed * Mathf.PI * 2f / Mathf.Max(0.01f, _settings.BobPeriod))) * 0.5f;
                    spot.PokeBobber(view.WaterPosition, retreat * _settings.NibbleDistance);
                    view.SetBiteRetreat(retreat);
                    if (elapsed >= _settings.HookWindow) break;
                    if (gate.Sample(_input.MiniGame.Stop.IsPressed())) return true;
                }
                return false;
            }
            finally { if (view != null) view.SetBiting(false); }
        }
    }
}

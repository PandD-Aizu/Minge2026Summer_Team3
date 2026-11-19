using System;
using System.Threading;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MiniGame
{
    /// <summary>ゲージ式の入力とモデルを結び、成績を共通フローへ返す</summary>
    public sealed class GaugeMiniGameController : IMiniGameController
    {
        private readonly GaugeMiniGameView _view;
        private readonly PlayerInputAction _input;
        private readonly Upgrades.UpgradeService _upgrades;
        public MiniGameType GameType => MiniGameType.Gauge;

        /// <summary>画面と入力を受け取る</summary>
        /// <param name="view">ゲージ画面</param>
        /// <param name="input">共有入力</param>
        /// <param name="upgrades">共有する釣り強化</param>
        /// <example>VContainerから生成する</example>
        public GaugeMiniGameController(GaugeMiniGameView view, PlayerInputAction input, Upgrades.UpgradeService upgrades)
        { _view = view; _input = input; _upgrades = upgrades; }

        /// <inheritdoc />
        public bool CanPlay(MiniGameSettings settings) => settings is GaugeMiniGameSettings;

        /// <inheritdoc />
        public async UniTask<MiniGameResult> PlayAsync(MiniGameSettings settings, FishDefinition fish, CancellationToken cancellation)
        {
            if (settings is not GaugeMiniGameSettings gauge) throw new ArgumentException(nameof(settings));
            cancellation.ThrowIfCancellationRequested();
            var model = new GaugeMiniGameModel(gauge, new System.Random(UnityEngine.Random.Range(0, int.MaxValue)),
                _upgrades.FishingGainMultiplier);
            var gate = new MiniGameInputGate();
            try
            {
                _view.Show(fish != null ? fish.ItemImage : null);
                _view.Render(model);
                while (!model.IsFinished)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                    if (Time.deltaTime <= 0) continue;
                    gate.Sample(_input.MiniGame.Stop.IsPressed());
                    model.Tick(Time.deltaTime, gate.Held);
                    _view.Render(model);
                }
                return model.Result;
            }
            finally { if (_view != null) _view.Hide(); }
        }
    }
}

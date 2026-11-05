using System;
using System.Threading;
using _Project.Scripts.Data.Enum;
using _Project.Scripts.Data.Fish;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace MiniGame
{
    /// <summary>回転する針を新しいJ入力で止めて成績を返す</summary>
    public sealed class RotationMiniGameController : IMiniGameController
    {
        private readonly RotationMiniGameView _view;
        private readonly PlayerInputAction _input;
        public MiniGameType GameType => MiniGameType.Rotation;

        /// <summary>目押し画面と共通入力を受け取る</summary>
        /// <param name="view">目押し画面</param>
        /// <param name="input">共有入力アクション</param>
        /// <example>VContainerから生成する</example>
        public RotationMiniGameController(RotationMiniGameView view, PlayerInputAction input)
        { _view = view; _input = input; }

        /// <inheritdoc />
        public bool CanPlay(MiniGameSettings settings) => settings is RotationMiniGameSettings;

        /// <inheritdoc />
        public async UniTask<MiniGameResult> PlayAsync(MiniGameSettings settings, FishDefinition fish, CancellationToken cancellation)
        {
            if (settings is not RotationMiniGameSettings rotation) throw new ArgumentException(nameof(settings));
            cancellation.ThrowIfCancellationRequested();
            var gate = new MiniGameInputGate();
            try
            {
                _view.ApplySettings(rotation);
                _view.ShowMiniGameCanvas();
                _view.StartRotatingPin();
                while (true)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellation);
                    if (Time.deltaTime <= 0) continue;
                    if (!gate.Sample(_input.MiniGame.Stop.IsPressed())) continue;
                    float angle = Mathf.Repeat(_view.GetPinAngle(), 360f);
                    if (angle < rotation.GreatAngle) return MiniGameResult.Great;
                    return angle < rotation.GoodAngle ? MiniGameResult.Good : MiniGameResult.Miss;
                }
            }
            finally
            {
                // 中断でも結果表示に進む場合でも、このゲームの表示だけを片付ける
                if (_view != null) _view.HideMiniGameCanvas();
            }
        }
    }
}

using System;
using R3;
using UnityEngine;
using VContainer.Unity;

namespace MiniGame
{
    public sealed class RotationMiniGameTestEntryPoint : IStartable, IDisposable
    {
        private readonly RotationMiniGameController _controller;
        private readonly RotationMiniGameSettings _settings;
        private readonly CompositeDisposable _subscriptions = new();

        public RotationMiniGameTestEntryPoint(
            RotationMiniGameController controller,
            RotationMiniGameSettings settings)
        {
            _controller = controller;
            _settings = settings;
        }

        public void Start()
        {
            _controller.Completed
                .Subscribe(result =>
                {
                    Debug.Log($"ミニゲーム結果：{result}");

                    // 今回は動作確認なので、結果が出たら閉じる
                    _controller.EndGame();
                })
                .AddTo(_subscriptions);

            _controller.Canceled
                .Subscribe(_ => Debug.Log("ミニゲームを中断しました"))
                .AddTo(_subscriptions);

            _controller.StartGame(_settings);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }
    }
}

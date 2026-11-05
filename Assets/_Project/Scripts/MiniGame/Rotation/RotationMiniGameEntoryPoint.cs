using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Input;
using UnityEngine;
using VContainer.Unity;

namespace MiniGame
{
    /// <summary>開発シーンから目押し単体を確認する</summary>
    public sealed class RotationMiniGameTestEntryPoint : IStartable, IDisposable
    {
        private readonly RotationMiniGameController _controller;
        private readonly RotationMiniGameSettings _settings;
        private readonly InputModeService _input;
        private readonly CancellationTokenSource _cancellation = new();

        /// <summary>テスト対象と入力切替を受け取る</summary>
        /// <param name="controller">目押しの実行元</param>
        /// <param name="settings">テスト設定</param>
        /// <param name="input">入力モード</param>
        /// <example>開発用LifetimeScopeにEntryPointとして登録する</example>
        public RotationMiniGameTestEntryPoint(RotationMiniGameController controller, RotationMiniGameSettings settings, InputModeService input)
        { _controller = controller; _settings = settings; _input = input; }

        /// <summary>開発用の実行を開始する</summary>
        /// <example>VContainerから呼ばれる</example>
        public void Start() => RunAsync().Forget();

        /// <summary>入力を切り替え、成績をログに出して入力を戻す</summary>
        /// <returns>実行終了までの待機</returns>
        /// <example>Startから呼ぶ</example>
        private async UniTask RunAsync()
        {
            _input.SwitchToMiniGame();
            try { Debug.Log(await _controller.PlayAsync(_settings, null, _cancellation.Token)); }
            catch (OperationCanceledException) { }
            finally { _input.SwitchToPlayer(); }
        }

        /// <summary>開発シーン終了時に実行を中断する</summary>
        /// <example>VContainerから呼ばれる</example>
        public void Dispose() { _cancellation.Cancel(); _cancellation.Dispose(); }
    }
}

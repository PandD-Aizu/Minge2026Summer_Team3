using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Dialogue
{
    /// <summary>本編の操作制限と会話再生をまとめ、会話単体から主人公への依存を分離する</summary>
    public sealed class GameplayDialogueController : IDisposable
    {
        private readonly DialogueService _dialogue;
        private readonly PlayerInputReader _input;
        private readonly CancellationTokenSource _lifetime = new();
        private IDisposable _inputBlock;
        private bool _playing;
        private bool _disposed;

        /// <summary>同じシーンの会話サービスと主人公の入力を受け取る</summary>
        /// <param name="dialogue">共通の会話サービス</param>
        /// <param name="input">操作を制限する主人公</param>
        /// <example>シーンのLifetimeScopeに登録してVContainerから生成する</example>
        public GameplayDialogueController(DialogueService dialogue, PlayerInputReader input)
        {
            _dialogue = dialogue;
            _input = input;
        }

        /// <summary>操作を制限して会話を再生し、終了理由にかかわらず自分の制限を解除する</summary>
        /// <param name="data">再生する会話データ</param>
        /// <param name="cancellationToken">イベントや呼び出し元の寿命に対応するトークン</param>
        /// <returns>正常終了、中断、または会話や別のUIが使用中の場合の開始拒否</returns>
        /// <example>if (await controller.PlayAsync(data, token) == DialogueResult.Completed) イベントを進める</example>
        public async UniTask<DialogueResult> PlayAsync(DialogueData data, CancellationToken cancellationToken = default)
        {
            if (_disposed || cancellationToken.IsCancellationRequested) return DialogueResult.Canceled;
            if (_playing || _dialogue.IsPlaying || !_input.CanStartGameplayAction) return DialogueResult.Rejected;

            _playing = true;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            try
            {
                // 歩行だけでなく、背後の釣り・交換・インベントリ入力も止める
                _inputBlock = _input.BlockGameplayInput();
                return await _dialogue.PlayAsync(data, cancellation.Token);
            }
            finally
            {
                _inputBlock?.Dispose();
                _inputBlock = null;
                _playing = false;
            }
        }

        /// <summary>Scope破棄時に会話を中断し、入力制限を解放する</summary>
        /// <example>シーンのアンロード時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _lifetime.Cancel();
            _inputBlock?.Dispose();
            _inputBlock = null;
            _lifetime.Dispose();
        }
    }
}

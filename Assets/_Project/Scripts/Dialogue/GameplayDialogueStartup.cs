using System.Threading;
using _Project.Scripts.Core;
using Cysharp.Threading.Tasks;
using GameOver;
using UnityEngine;
using VContainer.Unity;

namespace Dialogue
{
    /// <summary>捕獲から帰ったときだけ使う会話データを区別して渡す</summary>
    public sealed class CaptureReturnDialogue
    {
        public DialogueData Data { get; }

        /// <summary>帰還用の会話データを保持する</summary>
        /// <param name="data">未設定なら帰還後の会話は再生しない</param>
        public CaptureReturnDialogue(DialogueData data) => Data = data;
    }

    /// <summary>本編シーンの開始時に、設定された会話を自動再生する</summary>
    public sealed class GameplayDialogueStartup : IAsyncStartable
    {
        private readonly GameplayDialogueController _controller;
        private readonly DialogueData _dialogue;
        private readonly CaptureReturnDialogue _captureReturnDialogue;
        private readonly GameProgress _progress;
        private readonly GameOverView _fade;

        /// <summary>開始時の会話と本編用の再生処理を受け取る</summary>
        /// <param name="controller">会話中の操作制限を担当するController</param>
        /// <param name="dialogue">シーン開始時に再生する会話データ</param>
        /// <param name="captureReturnDialogue">敵から帰ったときの独り言</param>
        /// <param name="progress">帰還したことを一度だけ保持する状態</param>
        /// <param name="fade">暗転を解除するCanvas</param>
        /// <example>GameplayDialogueLifetimeScopeで開始時の会話を指定して使用する</example>
        public GameplayDialogueStartup(GameplayDialogueController controller, DialogueData dialogue,
            CaptureReturnDialogue captureReturnDialogue, GameProgress progress, GameOverView fade)
        {
            _controller = controller;
            _dialogue = dialogue;
            _captureReturnDialogue = captureReturnDialogue;
            _progress = progress;
            _fade = fade;
        }

        /// <summary>シーンの初期化後に会話を開始し、終了まで待つ</summary>
        /// <param name="cancellation">Scope破棄時に会話を中断するトークン</param>
        /// <returns>会話の終了まで待つUniTask</returns>
        /// <example>VContainerの開始処理から自動実行される</example>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            var returningFromCapture = _progress.ConsumeCaptureReturn();
            if (returningFromCapture) await _fade.FadeToWhiteAsync(cancellation);

            var dialogue = returningFromCapture ? _captureReturnDialogue.Data : _dialogue;
            if (dialogue == null)
            {
                Debug.LogWarning("捕獲から帰ったときのDialogueDataが未設定です");
                return;
            }

            var result = await _controller.PlayAsync(dialogue, cancellation);
            if (result == DialogueResult.Rejected)
                Debug.LogWarning("シーン開始時の会話を再生できないため、会話データと入力状態を確認してください");
        }
    }
}

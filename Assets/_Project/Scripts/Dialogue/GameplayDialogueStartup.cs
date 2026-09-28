using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer.Unity;

namespace Dialogue
{
    /// <summary>本編シーンの開始時に、設定された会話を自動再生する</summary>
    public sealed class GameplayDialogueStartup : IAsyncStartable
    {
        private readonly GameplayDialogueController _controller;
        private readonly DialogueData _dialogue;

        /// <summary>開始時の会話と本編用の再生処理を受け取る</summary>
        /// <param name="controller">会話中の操作制限を担当するController</param>
        /// <param name="dialogue">シーン開始時に再生する会話データ</param>
        /// <example>GameplayDialogueLifetimeScopeで開始時の会話を指定して使用する</example>
        public GameplayDialogueStartup(GameplayDialogueController controller, DialogueData dialogue)
        {
            _controller = controller;
            _dialogue = dialogue;
        }

        /// <summary>シーンの初期化後に会話を開始し、終了まで待つ</summary>
        /// <param name="cancellation">Scope破棄時に会話を中断するトークン</param>
        /// <returns>会話の終了まで待つUniTask</returns>
        /// <example>VContainerの開始処理から自動実行される</example>
        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            var result = await _controller.PlayAsync(_dialogue, cancellation);
            if (result == DialogueResult.Rejected)
                Debug.LogWarning("シーン開始時の会話を再生できないため、会話データと入力状態を確認してください");
        }
    }
}

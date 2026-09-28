using System;
using System.Threading;
using _Project.Scripts.InteractableObject;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Dialogue
{
    /// <summary>接近中のインタラクトから会話を呼び出す配置用のコンポーネント</summary>
    public sealed class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueData _dialogue;
        [SerializeField] private InteractableConnector _connector;
        [SerializeField, Tooltip("正常終了した会話を、このシーンにいる間は再生しない")]
        private bool _playOnce;
        private GameplayDialogueController _controller;
        private IDisposable _subscription;
        private CancellationTokenSource _playCancellation;
        private bool _completed;

        /// <summary>会話の呼び出し先と入力を接続する</summary>
        /// <param name="controller">本編の会話と操作制限を担当するController</param>
        /// <param name="input">このシーンの主人公入力</param>
        /// <example>GameplayDialogueLifetimeScopeのTriggersに登録する</example>
        [Inject]
        public void Construct(GameplayDialogueController controller, PlayerInputReader input)
        {
            _controller = controller;
            _subscription?.Dispose();
            _subscription = input.OnInteractPressed
                .Where(_ => isActiveAndEnabled && _connector != null && _connector.IsPlayerNearby
                    && input.CanStartGameplayAction)
                .Subscribe(_ => PlayAsync().Forget());
        }

        /// <summary>会話を呼び出し、正常終了した場合だけ再生済みとして扱う</summary>
        /// <returns>会話の終了または開始拒否まで待つUniTask</returns>
        /// <example>接近中のJ入力から呼ぶ</example>
        private async UniTask PlayAsync()
        {
            if (_playCancellation != null || (_playOnce && _completed)) return;
            var cancellation = new CancellationTokenSource();
            _playCancellation = cancellation;
            try
            {
                var result = await _controller.PlayAsync(_dialogue, cancellation.Token);
                if (result == DialogueResult.Completed) _completed = true;
            }
            finally
            {
                _playCancellation = null;
                cancellation.Dispose();
            }
        }

        /// <summary>対象が無効になったら、その対象が開始した会話を中断する</summary>
        /// <example>イベント対象を非表示にした場合にUnityが呼ぶ</example>
        private void OnDisable() => _playCancellation?.Cancel();

        /// <summary>破棄時に入力の購読を解放する</summary>
        /// <example>シーンのアンロード時にUnityが呼ぶ</example>
        private void OnDestroy() => _subscription?.Dispose();
    }
}

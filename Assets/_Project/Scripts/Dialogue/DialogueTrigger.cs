using System;
using System.Collections.Generic;
using System.Threading;
using _Project.Scripts.Core;
using _Project.Scripts.InteractableObject;
using Controller;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using VContainer;

namespace Dialogue
{
    /// <summary>接近中のインタラクトから条件付き会話または通常会話を呼び出す</summary>
    public sealed class DialogueTrigger : MonoBehaviour
    {
        [Serializable]
        private sealed class RadioDialogueEntry
        {
            [SerializeField] private TutorialStep _requiredTutorialStep = TutorialStep.None;
            [SerializeField] private StoryFlag _requiredStoryFlag = StoryFlag.None;
            [SerializeField] private DialogueData _dialogue;
            [SerializeField] private bool _advanceTutorialOnComplete;
            [SerializeField] private StoryFlag _storyFlagOnComplete = StoryFlag.None;

            public TutorialStep? RequiredTutorialStep => _requiredTutorialStep == TutorialStep.None
                ? null : _requiredTutorialStep;
            public StoryFlag? RequiredStoryFlag => _requiredStoryFlag == StoryFlag.None
                ? null : _requiredStoryFlag;
            public DialogueData Dialogue => _dialogue;
            public bool AdvanceTutorialOnComplete => _advanceTutorialOnComplete;
            public StoryFlag? StoryFlagOnComplete => _storyFlagOnComplete == StoryFlag.None
                ? null : _storyFlagOnComplete;
        }

        [SerializeField, Tooltip("条件を満たしたときに優先して再生する会話。上から順に確認")]
        private RadioDialogueEntry[] _eventDialogues = Array.Empty<RadioDialogueEntry>();
        [SerializeField, Tooltip("再生できるイベント会話がないときにランダム再生する会話")]
        private DialogueData[] _randomDialogues = Array.Empty<DialogueData>();
        [SerializeField] private InteractableConnector _connector;
        [SerializeField, HideInInspector] private DialogueData _dialogue;
        [SerializeField, HideInInspector] private bool _playOnce;

        [SerializeField, Tooltip("この対象との会話中に使うカメラ 未設定なら切り替えない")]
        private CameraZoom _conversationCamera;

        private GameplayDialogueController _controller;
        private TutorialController _tutorialController;
        private IDisposable _subscription;
        private CancellationTokenSource _playCancellation;
        private readonly HashSet<int> _completedEventIndices = new();
        private readonly HashSet<int> _skippedRandomIndices = new();
        private int _lastRandomIndex = -1;
        private bool _legacyCompleted;

        /// <summary>会話再生、チュートリアル判定、入力を接続する</summary>
        /// <param name="controller">会話と操作制限を担当するController</param>
        /// <param name="tutorialController">イベント条件とチュートリアル進行を担当するController</param>
        /// <param name="input">このシーンの主人公入力</param>
        /// <example>GameplayDialogueLifetimeScopeのTriggersに登録する</example>
        [Inject]
        public void Construct(GameplayDialogueController controller, TutorialController tutorialController,
            PlayerInputReader input)
        {
            _controller = controller;
            _tutorialController = tutorialController;
            _subscription?.Dispose();
            _subscription = input.OnInteractPressed
                .Where(_ => isActiveAndEnabled && _connector != null && _connector.IsPlayerNearby
                    && input.CanStartGameplayAction)
                .Subscribe(_ => PlayAsync().Forget());
        }

        /// <summary>イベント会話、通常会話、旧形式の会話の順に再生候補を探す</summary>
        /// <returns>会話の終了または開始拒否まで待つUniTask</returns>
        /// <example>接近中のインタラクト入力から呼ぶ</example>
        private async UniTask PlayAsync()
        {
            if (_playCancellation != null) return;

            var cancellation = new CancellationTokenSource();
            _playCancellation = cancellation;
            try
            {
                if (await TryPlayEventDialogue(cancellation.Token)) return;

                if (_randomDialogues.Length > 0)
                {
                    await PlayRandomDialogue(cancellation.Token);
                    return;
                }

                if (_dialogue == null || (_playOnce && _legacyCompleted)) return;

                var result = await PlayDialogueAsync(_dialogue, cancellation.Token);
                if (result == DialogueResult.Completed) _legacyCompleted = true;
            }
            finally
            {
                _playCancellation = null;
                cancellation.Dispose();
            }
        }

        /// <summary>条件に合う最初のイベント会話を再生し、終了後の進行を通知する</summary>
        /// <param name="cancellationToken">対象の無効化に伴う中断トークン</param>
        /// <returns>イベント会話を選んだ場合はtrue</returns>
        /// <example>ラジオに話しかけたときにPlayAsyncから呼ぶ</example>
        private async UniTask<bool> TryPlayEventDialogue(CancellationToken cancellationToken)
        {
            for (var i = 0; i < _eventDialogues.Length; i++)
            {
                var entry = _eventDialogues[i];
                if (entry == null || entry.Dialogue == null || _completedEventIndices.Contains(i)) continue;
                if (!_tutorialController.CanPlayRadioDialogue(entry.RequiredTutorialStep, entry.RequiredStoryFlag))
                    continue;

                var result = await PlayDialogueAsync(entry.Dialogue, cancellationToken);
                if (result == DialogueResult.Completed || result == DialogueResult.Skipped)
                {
                    _completedEventIndices.Add(i);
                    _tutorialController.NotifyRadioDialogueCompleted(entry.AdvanceTutorialOnComplete,
                        entry.StoryFlagOnComplete);
                }

                return true;
            }

            return false;
        }

        /// <summary>直前と同じ会話を避けて通常会話をランダム再生する</summary>
        /// <param name="cancellationToken">対象の無効化に伴う中断トークン</param>
        /// <returns>選んだ会話の終了まで待つUniTask</returns>
        /// <example>条件に合うイベント会話がない場合に呼ぶ</example>
        private async UniTask PlayRandomDialogue(CancellationToken cancellationToken)
        {
            var candidates = new List<int>();
            for (var i = 0; i < _randomDialogues.Length; i++)
            {
                if (_randomDialogues[i] == null || _skippedRandomIndices.Contains(i)) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0) return;

            if (candidates.Count > 1) candidates.Remove(_lastRandomIndex);

            var selectedIndex = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            _lastRandomIndex = selectedIndex;
            var result = await PlayDialogueAsync(_randomDialogues[selectedIndex], cancellationToken);
            if (result == DialogueResult.Skipped) _skippedRandomIndices.Add(selectedIndex);
        }

        /// <summary>選んだ会話の開始と終了にカメラ切り替えを接続する</summary>
        /// <param name="data">再生する会話データ</param>
        /// <param name="cancellationToken">対象の無効化に伴う中断トークン</param>
        /// <returns>会話の終了結果</returns>
        /// <example>イベント会話とランダム会話の両方から呼ぶ</example>
        private async UniTask<DialogueResult> PlayDialogueAsync(DialogueData data, CancellationToken cancellationToken)
        {
            var zoomStarted = false;
            try
            {
                // 会話が開始できた場合だけ、この対象に設定されたカメラへ切り替える
                return await _controller.PlayAsync(data, cancellationToken, () =>
                {
                    if (_conversationCamera == null || !_conversationCamera.isActiveAndEnabled) return;
                    zoomStarted = true;
                    _conversationCamera.SetZoom(true);
                });
            }
            finally
            {
                // 正常終了、中断、例外のいずれでも通常表示へ戻す
                if (zoomStarted && _conversationCamera != null) _conversationCamera.SetZoom(false);
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

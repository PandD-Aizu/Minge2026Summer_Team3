
using System.Threading;
using _Project.Scripts.Core;
using Controller;
using Cysharp.Threading.Tasks;
using GameOver;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Dialogue
{
    public sealed class CaptureReturnDialogue
    {
        public DialogueData Data { get; }

        public CaptureReturnDialogue(DialogueData data) => Data = data;
    }

    public sealed class GameplayDialogueStartup : IAsyncStartable
    {
        private readonly GameplayDialogueController _controller;
        private readonly DialogueData _dialogue;
        private readonly CaptureReturnDialogue _captureReturnDialogue;
        private readonly GameProgress _progress;
        private readonly GameOverView _fade;
        private readonly CameraZoom _cameraZoom;
        private readonly TutorialController _tutorialController;
        private readonly TutorialGuideController _tutorialGuideController;

        public GameplayDialogueStartup(
            GameplayDialogueController controller,
            DialogueData dialogue,
            CaptureReturnDialogue captureReturnDialogue,
            GameProgress progress,
            GameOverView fade,
            CameraZoom cameraZoom,
            TutorialController tutorialController,
            IObjectResolver resolver)
        {
            _controller = controller;
            _dialogue = dialogue;
            _captureReturnDialogue = captureReturnDialogue;
            _progress = progress;
            _fade = fade;
            _cameraZoom = cameraZoom;
            _tutorialController = tutorialController;
            // 操作案内を配置しない通常シーンでは未登録を許容する
            resolver.TryResolve(out _tutorialGuideController);
        }

        public async UniTask StartAsync(
            CancellationToken cancellation = default)
        {
            var returningFromCapture = _progress.ConsumeCaptureReturn();

            if (returningFromCapture)
                await _fade.FadeToWhiteAsync(cancellation);

            var dialogue = returningFromCapture
                ? _captureReturnDialogue.Data
                : _dialogue;

            if (dialogue == null)
            {
                Debug.LogWarning("再生するDialogueDataが未設定です");
                return;
            }

            var zoomStarted = false;
            DialogueResult result;

            try
            {
                result = await _controller.PlayAsync(
                    dialogue,
                    cancellation,
                    () =>
                    {
                        if (returningFromCapture ||
                            _cameraZoom == null ||
                            !_cameraZoom.isActiveAndEnabled)
                        {
                            return;
                        }

                        zoomStarted = true;
                        _cameraZoom.SetZoom(true);
                    });

                if (result == DialogueResult.Rejected)
                {
                    Debug.LogWarning(
                        "シーン開始時の会話を再生できませんでした");
                }
            }
            finally
            {
                if (zoomStarted && _cameraZoom != null)
                {
                    _cameraZoom.SetZoom(false);
                }
            }

            // 初回到着時の会話終了後に操作案内を開始する
            if (!returningFromCapture &&
                (result == DialogueResult.Completed ||
                 result == DialogueResult.Skipped) &&
                _tutorialController.CurrentStep == TutorialStep.TalkToRadioFirst)
            {
                _tutorialGuideController?.BeginGuide();
            }
        }
    }
}

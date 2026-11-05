
using Controller;
using Dialogue;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>本編用の会話UI、主人公、呼び出し対象を一組として登録する</summary>
    public sealed class GameplayDialogueLifetimeScope : LifetimeScope
    {
        [SerializeField] private DialogueUIView _dialogueView;
        [SerializeField] private PlayerInputReader _playerInputReader;

        [SerializeField, Tooltip("設定した場合、シーン開始時に自動再生する会話")]
        private DialogueData _startupDialogue;

        [SerializeField, Tooltip("敵に捕まってCampStageへ戻ったときの独り言")]
        private DialogueData _captureReturnDialogue;

        [SerializeField]
        private DialogueTrigger[] _triggers =
            System.Array.Empty<DialogueTrigger>();

        [SerializeField] private RadioTalkingAnimation _radioTalkingAnimation;

        [SerializeField, Tooltip("開始時の会話で使用するラジオ用カメラ切り替え")]
        private CameraZoom _startupCamera;

        [Header("Tutorial")]
        [SerializeField, Tooltip("操作案内を制御するTutorialGuideController")]
        private TutorialGuideController _tutorialGuideController;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterDialogue(_dialogueView, _radioTalkingAnimation);
            builder.RegisterComponent(_playerInputReader);

            builder.Register<GameplayDialogueController>(Lifetime.Scoped);

            // 操作案内のControllerを登録
            if (_tutorialGuideController != null)
            {
                builder.RegisterComponent(_tutorialGuideController);
            }

            // 自動再生は呼び出し側に置き、会話単体のサービスから分離する
            if (_startupDialogue != null)
            {
                builder.RegisterInstance(_startupDialogue);
                builder.RegisterInstance(
                    new CaptureReturnDialogue(_captureReturnDialogue));

                builder.RegisterEntryPoint<GameplayDialogueStartup>()
                    .WithParameter(typeof(CameraZoom), (object)_startupCamera);
            }

            // 配置した対象を同じ会話サービスへ接続する
            builder.RegisterBuildCallback(container =>
            {
                foreach (var trigger in _triggers)
                {
                    if (trigger != null)
                        container.Inject(trigger);
                }
            });
        }
    }
}

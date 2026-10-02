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
        [SerializeField] private DialogueTrigger[] _triggers = System.Array.Empty<DialogueTrigger>();

        /// <summary>会話単体と本編用Controllerを同じScopeへ登録する</summary>
        /// <param name="builder">このシーンの登録先</param>
        /// <example>親にGameLifetimeScopeを指定し、UI、主人公、TriggersをInspectorで設定する</example>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterDialogue(_dialogueView);
            builder.RegisterComponent(_playerInputReader);
            builder.Register<GameplayDialogueController>(Lifetime.Scoped);

            // 自動再生は呼び出し側に置き、会話単体のサービスから分離する
            if (_startupDialogue != null)
            {
                builder.RegisterInstance(_startupDialogue);
                builder.RegisterEntryPoint<GameplayDialogueStartup>();
            }

            // 配置した対象を同じ会話サービスへ接続する
            builder.RegisterBuildCallback(container =>
            {
                foreach (var trigger in _triggers)
                {
                    if (trigger != null) container.Inject(trigger);
                }
            });
        }
    }
}

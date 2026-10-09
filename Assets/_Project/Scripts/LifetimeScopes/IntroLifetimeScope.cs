using Dialogue;
using Controller;
using FMODServices;
using FMODSettings;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class IntroLifetimeScope : LifetimeScope
    {
        [SerializeField] private DialogueData _introDialogue;
        [SerializeField] private DialogueUIView _dialogueView;

        /// <summary>導入会話の状態管理、表示、シーン進行を登録する</summary>
        /// <param name="builder">このScopeのDI登録先</param>
        /// <example>導入シーンの初期化時にVContainerが呼ぶ</example>
        protected override void Configure(IContainerBuilder builder)
        {
            // 会話の状態管理とUIの接続を同じScopeに登録する
            builder.RegisterDialogue(_dialogueView);

            // 会話後のシーン遷移はイントロ側で管理する
            builder.RegisterInstance(_introDialogue);
            builder.RegisterEntryPoint<IntroController>();

            // イントロ単独起動でも昼BGMを再生し、タイトルからの遷移時は再生位置を維持する
            builder.RegisterBuildCallback(resolver => resolver.Resolve<FMODBGMService>()
                .PlayBGM(FMODEventPath.BGM_BENEATH_THE_WAVES.Reference));
        }
    }
}

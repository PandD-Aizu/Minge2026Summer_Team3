using System;
using Dialogue;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>会話の進行と表示を同じScopeへ登録する共通処理</summary>
    public static class DialogueRegistration
    {
        /// <summary>指定したUIと会話単体のサービスを登録する</summary>
        /// <param name="builder">シーンまたは単体利用のScope</param>
        /// <param name="view">このScopeで使う会話UI</param>
        /// <example>builder.RegisterDialogue(_dialogueView)</example>
        public static void RegisterDialogue(this IContainerBuilder builder, DialogueUIView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view), "会話UIを設定してください");
            builder.RegisterComponent(view);
            builder.Register<DialogueService>(Lifetime.Scoped);
            builder.RegisterEntryPoint<DialoguePresenter>();
        }
    }
}

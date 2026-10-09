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
        /// <param name="radioAnimation">ラジオの会話演出 未配置のシーンではnull</param>
        /// <example>builder.RegisterDialogue(_dialogueView)</example>
        public static void RegisterDialogue(this IContainerBuilder builder, DialogueUIView view,
            RadioTalkingAnimation radioAnimation = null)
        {
            if (view == null) throw new ArgumentNullException(nameof(view), "会話UIを設定してください");
            builder.RegisterComponent(view);
            builder.Register<DialogueService>(Lifetime.Scoped);
            // 演出がない場合もnullを明示的に渡し、未登録の型を解決させない
            builder.RegisterEntryPoint<DialoguePresenter>()
                .WithParameter(typeof(RadioTalkingAnimation), (object)radioAnimation);
        }
    }
}

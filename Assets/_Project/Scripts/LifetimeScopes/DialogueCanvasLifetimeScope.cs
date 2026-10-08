using Dialogue;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    /// <summary>
    /// ゲームシーンで使用するDialogueCanvasのLifetimeScope
    /// </summary>
    public class DialogueCanvasLifetimeScope : LifetimeScope
    {
        [UnityEngine.SerializeField] private DialogueUIView _dialogueView;

        /// <summary>会話の状態管理とUIを同じScopeに登録する</summary>
        /// <param name="builder">このScopeのDI登録先</param>
        /// <example>DialogueCanvasの生成時にVContainerが呼ぶ</example>
        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterDialogue(_dialogueView);
        }
    }
}

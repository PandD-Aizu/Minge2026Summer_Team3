
using Controller;
using Dialogue;
using Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LifetimeScopes
{
    public class IntroLifetimeScope : LifetimeScope
    {
        [SerializeField] private DialogueData _introDialogue;

        protected override void Configure(IContainerBuilder builder)
        {
            // DialogueUIのエントリーポイント
            builder.RegisterEntryPoint<DialoguePresenter>().AsSelf();


            // DialogueUIView
            builder.RegisterComponentInHierarchy<DialogueUIView>();
            builder.RegisterInstance(_introDialogue);

            builder.RegisterEntryPoint<IntroController>();


        }
    }
}

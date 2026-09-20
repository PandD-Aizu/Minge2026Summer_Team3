using System;
using Cysharp.Threading.Tasks;
using Presentation;
using Dialogue;
using SceneLoadServices;
using VContainer.Unity;
using R3;

namespace Controller
{
    public class IntroController : IStartable, IDisposable
    {
        private readonly DialoguePresenter _dialoguePresenter;
        private readonly DialogueData _introDialogue;
        private readonly SceneLoadService _sceneLoader;

        private IDisposable _completedSubscription;

        public IntroController(
            DialoguePresenter dialoguePresenter,
            DialogueData introDialogue,
            SceneLoadService sceneLoader)
        {
            _dialoguePresenter = dialoguePresenter;
            _introDialogue = introDialogue;
            _sceneLoader = sceneLoader;
        }


        public void Start()
        {
            _completedSubscription = _dialoguePresenter.Completed
                .SubscribeAwait(async (_, _) =>
                    {
                        await HandleDialogueCompleted();
                    },
                    AwaitOperation.Drop);

            _dialoguePresenter.StartDialogue(_introDialogue);

        }

        private async UniTask HandleDialogueCompleted()
        {
            await _sceneLoader.LoadSceneAsync("CampStage");
        }

        public void Dispose()
        {
            _completedSubscription?.Dispose();
        }
    }
}

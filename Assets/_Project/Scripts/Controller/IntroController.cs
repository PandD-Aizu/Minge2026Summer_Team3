using System;
using Dialogue;
using Cysharp.Threading.Tasks;
using SceneLoadServices;
using VContainer.Unity;
using R3;

namespace Controller
{
    public class IntroController : IStartable, IDisposable
    {
        private readonly DialogueService _dialogueService;
        private readonly DialogueData _introDialogue;
        private readonly SceneLoadService _sceneLoader;

        private IDisposable _completedSubscription;

        /// <summary>導入会話と終了後のシーン遷移に必要な依存関係を受け取る</summary>
        /// <param name="dialogueService">会話の進行を管理するサービス</param>
        /// <param name="introDialogue">導入シーンで再生する会話</param>
        /// <param name="sceneLoader">会話終了後のシーン読み込み先</param>
        /// <example>LifetimeScopeのEntryPoint登録から生成する</example>
        public IntroController(
            DialogueService dialogueService,
            DialogueData introDialogue,
            SceneLoadService sceneLoader)
        {
            _dialogueService = dialogueService;
            _introDialogue = introDialogue;
            _sceneLoader = sceneLoader;
        }


        /// <summary>完了通知を購読してから導入会話を開始する</summary>
        /// <example>VContainerの開始処理から呼ばれる</example>
        public void Start()
        {
            _completedSubscription = _dialogueService.Completed
                .SubscribeAwait(async (_, _) =>
                    {
                        await HandleDialogueCompleted();
                    },
                    AwaitOperation.Drop);

            _dialogueService.StartDialogue(_introDialogue);

        }

        /// <summary>導入会話の終了後にキャンプへ移動する</summary>
        /// <returns>シーン読み込みの完了を待つUniTask</returns>
        /// <example>会話のCompleted通知から呼ぶ</example>
        private async UniTask HandleDialogueCompleted()
        {
            await _sceneLoader.LoadSceneAsync("CampStage");
        }

        /// <summary>会話完了通知の購読を解除する</summary>
        /// <example>LifetimeScopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            _completedSubscription?.Dispose();
        }
    }
}

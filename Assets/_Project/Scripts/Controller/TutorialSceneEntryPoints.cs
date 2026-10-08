using VContainer.Unity;

namespace Controller
{
    /// <summary>釣り場シーンへの到着をチュートリアルへ通知する</summary>
    public sealed class TutorialFishingStageEntryPoint : IInitializable
    {
        private readonly TutorialController _tutorialController;

        /// <summary>チュートリアル進行を受け取る</summary>
        /// <param name="tutorialController">到着イベントを進行条件として扱うController</param>
        /// <example>FishingSceneLifetimeScopeから登録する</example>
        public TutorialFishingStageEntryPoint(TutorialController tutorialController)
        {
            _tutorialController = tutorialController;
        }

        /// <summary>釣り場シーンの初期化時に到着を通知する</summary>
        /// <example>昼釣りまたは夜釣りのシーンを開いたときにVContainerが呼ぶ</example>
        public void Initialize()
        {
            _tutorialController.NotifyEnteredFishingStage();
        }
    }

    /// <summary>集荷所シーンへの帰還をチュートリアルへ通知する</summary>
    public sealed class TutorialCollectionReturnEntryPoint : IInitializable
    {
        private readonly TutorialController _tutorialController;

        /// <summary>チュートリアル進行を受け取る</summary>
        /// <param name="tutorialController">帰還イベントと夜への切替を扱うController</param>
        /// <example>集荷所のGameplayDialogueLifetimeScopeから登録する</example>
        public TutorialCollectionReturnEntryPoint(TutorialController tutorialController)
        {
            _tutorialController = tutorialController;
        }

        /// <summary>集荷所シーンの初期化時に帰還を通知する</summary>
        /// <example>集荷所シーンを開いたときにVContainerが呼ぶ</example>
        public void Initialize()
        {
            _tutorialController.NotifyReturnedToCollection();
        }
    }
}

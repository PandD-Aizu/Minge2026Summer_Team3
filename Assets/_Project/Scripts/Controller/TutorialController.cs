using _Project.Scripts.Core;
using R3;

namespace Controller
{
    /// <summary>チュートリアル中に起きた出来事を受け取り、現在の段階を進める</summary>
    public class TutorialController
    {
        private readonly GameProgress _gameProgress;
        private readonly Subject<TutorialStep> _stepChanged = new();

        public TutorialStep CurrentStep { get; private set; } = TutorialStep.TalkToRadioFirst;
        public Observable<TutorialStep> OnStepChanged => _stepChanged;
        public bool IsCompleted => CurrentStep == TutorialStep.Completed;

        /// <summary>時間帯などのゲーム進行データを受け取る</summary>
        /// <param name="gameProgress">日数や時間帯を保持する進行データ</param>
        /// <example>GameLifetimeScopeでSingleton登録して生成する</example>
        public TutorialController(GameProgress gameProgress)
        {
            _gameProgress = gameProgress;
        }

        /// <summary>ラジオに話しかけたことを通知し、該当する段階なら次へ進める</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>ラジオ用のTriggerが会話完了後に呼ぶ</example>
        public bool NotifyRadioInteracted()
        {
            if (TryAdvance(TutorialStep.TalkToRadioFirst, TutorialStep.CatchDayFish)) return true;
            if (TryAdvance(TutorialStep.TalkToRadioBeforeFirstExchange, TutorialStep.ExchangeFirstFish)) return true;
            if (TryAdvance(TutorialStep.TalkToRadioAfterFirstExchange, TutorialStep.GoNightFishing)) return true;
            return TryAdvance(TutorialStep.TalkToRadioAfterNightFishing, TutorialStep.Completed);
        }

        /// <summary>ラジオ会話の条件を現在のチュートリアル段階と達成済みフラグで確認する</summary>
        /// <param name="requiredStep">条件にするチュートリアル段階。指定しない場合は段階を確認しない</param>
        /// <param name="requiredFlag">条件にする達成済みフラグ。指定しない場合はフラグを確認しない</param>
        /// <returns>指定された全条件を満たす場合はtrue</returns>
        /// <example>ラジオ会話候補を選ぶ前に呼ぶ</example>
        public bool CanPlayRadioDialogue(TutorialStep? requiredStep, StoryFlag? requiredFlag)
        {
            if (requiredStep.HasValue && CurrentStep != requiredStep.Value) return false;
            if (requiredFlag.HasValue && !_gameProgress.HasStoryFlag(requiredFlag.Value)) return false;

            return true;
        }

        /// <summary>初回交換前のラジオ案内が終わるまで交換画面を開かせない</summary>
        /// <returns>交換画面を開いてよい段階ならtrue</returns>
        /// <example>ExchangePointPresenterが集荷所への入力時に確認する</example>
        public bool CanOpenExchangePoint()
        {
            return CurrentStep != TutorialStep.TalkToRadioBeforeFirstExchange;
        }

        /// <summary>ラジオ会話の終了を受け取り、設定された事実フラグと進行を反映する</summary>
        /// <param name="advanceTutorial">会話後にラジオ接触による段階遷移を行う場合はtrue</param>
        /// <param name="completedFlag">会話終了でONにするフラグ。変更しない場合はnull</param>
        /// <returns>フラグまたはチュートリアル段階を更新した場合はtrue</returns>
        /// <example>指定会話が正常終了した直後に呼ぶ</example>
        public bool NotifyRadioDialogueCompleted(bool advanceTutorial, StoryFlag? completedFlag)
        {
            var changed = false;

            if (completedFlag.HasValue && !_gameProgress.HasStoryFlag(completedFlag.Value))
            {
                _gameProgress.SetStoryFlag(completedFlag.Value);
                changed = true;
            }

            if (advanceTutorial && NotifyRadioInteracted()) changed = true;

            return changed;
        }

        /// <summary>釣り場に到着したことを通知し、夜釣りなら上位存在との遭遇段階へ進める</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>FishingStage開始時のPresenterやSensorから呼ぶ</example>
        public bool NotifyEnteredFishingStage()
        {
            return TryAdvance(TutorialStep.GoNightFishing, TutorialStep.SeeUpperBeing);
        }

        /// <summary>魚が釣れたことを通知し、昼と夜で次の段階へ進める</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>ミニゲーム成功後に呼ぶ</example>
        public bool NotifyFishCaught()
        {
            if (CurrentStep == TutorialStep.CatchDayFish)
            {
                _gameProgress.SetStoryFlag(StoryFlag.FirstFishingCompleted);
                SetStep(TutorialStep.ReturnToCollectionAtNight);
                return true;
            }

            if (CurrentStep == TutorialStep.CatchNightFish)
            {
                _gameProgress.SetStoryFlag(StoryFlag.FirstNightFishingCompleted);
                SetStep(TutorialStep.ReturnFromNightFishing);
                return true;
            }

            return false;
        }

        /// <summary>集荷所に戻ったことを通知し、必要なら夜へ切り替える</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>CampStageへ戻ったときのPresenterやSensorから呼ぶ</example>
        public bool NotifyReturnedToCollection()
        {
            if (CurrentStep == TutorialStep.ReturnToCollectionAtNight)
            {
                _gameProgress.StartNight();
                SetStep(TutorialStep.TalkToRadioBeforeFirstExchange);
                return true;
            }

            return TryAdvance(TutorialStep.ReturnFromNightFishing, TutorialStep.TalkToRadioAfterNightFishing);
        }

        /// <summary>交換が完了したことを通知し、夜釣りへ進める</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>交換成功後に呼ぶ</example>
        public bool NotifyExchangeCompleted()
        {
            if (CurrentStep != TutorialStep.ExchangeFirstFish) return false;

            _gameProgress.SetStoryFlag(StoryFlag.FirstExchangeCompleted);
            SetStep(TutorialStep.TalkToRadioAfterFirstExchange);
            return true;
        }

        /// <summary>上位存在を見たことを通知し、夜の釣り段階へ進める</summary>
        /// <returns>チュートリアルが進んだ場合はtrue</returns>
        /// <example>上位存在のAreaSensorや演出完了後に呼ぶ</example>
        public bool NotifyUpperBeingSeen()
        {
            return TryAdvance(TutorialStep.SeeUpperBeing, TutorialStep.CatchNightFish);
        }

        /// <summary>現在の段階が一致した場合だけ次の段階へ進める</summary>
        /// <param name="expected">進行前に期待する段階</param>
        /// <param name="next">進行後の段階</param>
        /// <returns>段階を進めた場合はtrue</returns>
        /// <example>TryAdvance(TutorialStep.CatchDayFish, TutorialStep.ReturnToCollectionAtNight)</example>
        private bool TryAdvance(TutorialStep expected, TutorialStep next)
        {
            if (CurrentStep != expected) return false;

            SetStep(next);
            return true;
        }

        /// <summary>段階を更新し、変更があれば通知する</summary>
        /// <param name="next">次の段階</param>
        /// <example>チュートリアル進行時に呼ぶ</example>
        private void SetStep(TutorialStep next)
        {
            if (CurrentStep == next) return;

            CurrentStep = next;
            _stepChanged.OnNext(next);
        }
    }
}

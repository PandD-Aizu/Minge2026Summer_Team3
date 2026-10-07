using System.Collections.Generic;
using R3;
namespace _Project.Scripts.Core
{
    public class GameProgress
    {
        public int CurrentDay { get; private set; } = 1;
        public TimeOfDay CurrentTimeOfDay { get; private set; } = TimeOfDay.Day;

        private readonly Subject<TimeOfDay> _timeOfDayChanged = new();
        public Observable<TimeOfDay> TimeOfDayChanged => _timeOfDayChanged;

        private readonly HashSet<StoryFlag> storyFlags = new();
        private bool _captureReturnPending;

        /// <summary>捕獲から戻った後に再生する会話を予約する</summary>
        /// <example>FishingStageからCampStageへ移動する直前に呼ぶ</example>
        public void MarkCaptureReturn() => _captureReturnPending = true;

        /// <summary>捕獲からの帰還を一度だけ取り出す</summary>
        /// <returns>未処理の帰還があればtrue</returns>
        /// <example>CampStageの開始会話を選ぶときに呼ぶ</example>
        public bool ConsumeCaptureReturn()
        {
            if (!_captureReturnPending) return false;
            _captureReturnPending = false;
            return true;
        }

        /// <summary>シーン移動に失敗した場合の帰還予約を取り消す</summary>
        /// <example>CampStageを読み込めなかった場合に呼ぶ</example>
        public void ClearCaptureReturn() => _captureReturnPending = false;

        public void StartDay()
        {
            CurrentTimeOfDay = TimeOfDay.Day;
            _timeOfDayChanged.OnNext(TimeOfDay.Day);
        }

        public void StartNight()
        {
            CurrentTimeOfDay = TimeOfDay.Night;
            _timeOfDayChanged.OnNext(TimeOfDay.Night);
        }

        public void AdvanceDay()
        {
            CurrentDay++;
        }

        /// <summary>
        /// フラグを達成したらHashSetにStoryFlagを入れる
        /// </summary>
        /// <param name="storyFlag"></param>
        public void SetStoryFlag(StoryFlag storyFlag)
        {
            storyFlags.Add(storyFlag);
        }

        public bool HasStoryFlag(StoryFlag storyFlag)
        {
            return storyFlags.Contains(storyFlag);
        }

    }
}

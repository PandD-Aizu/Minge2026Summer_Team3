using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

namespace _Project.Scripts.Core
{
    public class GameProgress : IDisposable
    {
        /// <summary>
        /// 現在の日付、時間
        /// </summary>

        public TimeOfDay CurrentTimeOfDay { get; private set; } = TimeOfDay.Day;
        private readonly ReactiveProperty<float> _gameSeconds = new(7f * 3600f);
        private readonly ReadOnlyReactiveProperty<int> _gameHours;
        private readonly ReadOnlyReactiveProperty<int> _currentDay;
        private readonly ReadOnlyReactiveProperty<int> _currentWeek;
        public ReactiveProperty<float> GameSeconds => _gameSeconds;
        public ReadOnlyReactiveProperty<int> GameHours => _gameHours;
        public ReadOnlyReactiveProperty<int> CurrentDay => _currentDay;
        public ReadOnlyReactiveProperty<int> CurrentWeek => _currentWeek;


        private readonly IDisposable _timeOfDaySubscription;
        public int? LastCaughtFishId { get; private set; }

        public GameProgress()
        {
            _gameHours = _gameSeconds
                .Select(seconds => (int)(seconds / 3600f) % 24)
                .ToReadOnlyReactiveProperty();
            _currentDay = _gameSeconds
                .Select(seconds => (int)(seconds / (3600f * 24f)) + 1)
                .ToReadOnlyReactiveProperty();
            _currentWeek = _gameSeconds
                .Select(seconds => (int)(seconds / ((3600f * 24f) * 7)) + 1)
                .ToReadOnlyReactiveProperty();

            Observable<TimeOfDay> timeOfDayStream = _gameHours.Select(GetTimeOfDay);
            _timeOfDaySubscription = timeOfDayStream
                .DistinctUntilChanged() // 直前と同じ値が来たら省く、変わったら次へ行く
                .Subscribe(ChangeTimeOfDay);
        }

        /// <summary>最後に釣り成功した魚を全釣り場で共有する 戻り値はなし</summary>
        /// <param name="fishId">インベントリへ追加した魚のItemId</param>
        /// <example>魚の追加に成功した直後にRecordFishCaught(fish.ItemId)を呼ぶ</example>
        public void RecordFishCaught(int fishId) => LastCaughtFishId = fishId;

        private readonly Subject<TimeOfDay> _timeOfDayChanged = new();
        public Observable<TimeOfDay> TimeOfDayChanged => _timeOfDayChanged;

        private readonly HashSet<StoryFlag> storyFlags = new();
        private bool _captureReturnPending;


        /// <summary>
        /// ゲーム内時間を引数秒数進める
        /// </summary>
        /// <param name="seconds"></param>
        public void AdvanceGameTimeSeconds(float seconds)
        {
            _gameSeconds.Value += seconds;
        }

        ///<summary>
        /// ゲーム内時間を指定時刻まで進める
        /// </summary>
        public void AdvanceGameTimeHours(int hours)
        {
            if (hours < 0 || hours > 23)
            {
                Debug.Log("指定している時刻がおかしい");
                return;
            }

            // 既に目的の時間帯なら、そのままにする
            if (GetTimeOfDay(_gameHours.CurrentValue) == GetTimeOfDay(hours))
                return;

            float secondsPerDay = 24 * 3600f;  //一日の時間
            float currentSeconds = _gameSeconds.Value;

            // 今日の0時を基準に、指定のの時刻を求める
            // 経過日数を求める(intで切り捨て) -> 1日分の秒数を掛けて０時にする
            float dayStart = (int)(currentSeconds / secondsPerDay) * secondsPerDay;
            // 指定時刻を求める
            float targetSeconds = dayStart + hours * 3600f;

            // 翌日に進める必要がある場合
            if (targetSeconds < currentSeconds)
                targetSeconds += secondsPerDay;

            // 時間を巻き戻さず、必要な場合だけ進める
            if (targetSeconds > currentSeconds)
                AdvanceGameTimeSeconds(targetSeconds - currentSeconds); // 差分を代入
        }

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

        /// <summary>夕方へ切り替え、BGMと照明へ通知する</summary>
        /// <example>最初の昼釣りが完了したときにStartEvening()を呼ぶ</example>
        public void StartEvening()
        {
            CurrentTimeOfDay = TimeOfDay.Evening;
            _timeOfDayChanged.OnNext(TimeOfDay.Evening);
        }

        public void StartNight()
        {
            CurrentTimeOfDay = TimeOfDay.Night;
            _timeOfDayChanged.OnNext(TimeOfDay.Night);
        }

        /// <summary>明け方へ切り替え、時間帯を購読する照明や敵へ通知する</summary>
        /// <example>夜釣りから帰還したときにStartDawn()を呼ぶ</example>
        public void StartDawn()
        {
            CurrentTimeOfDay = TimeOfDay.Dawn;
            _timeOfDayChanged.OnNext(TimeOfDay.Dawn);
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

        /// <summary>
        /// ゲーム内時間(hour)から、その時刻の時間帯を判定する。
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        private static TimeOfDay GetTimeOfDay(int hour)
        {
            if(hour >= 19 || hour < 4) return TimeOfDay.Night;
            if(hour >= 17) return TimeOfDay.Evening;
            if(hour >= 7)  return TimeOfDay.Day;
            return TimeOfDay.Dawn;
        }

        private void ChangeTimeOfDay(TimeOfDay timeOfDay)
        {
            switch (timeOfDay)
            {
                case TimeOfDay.Day: StartDay(); break;
                case TimeOfDay.Evening: StartEvening(); break;
                case TimeOfDay.Night: StartNight(); break;
                case TimeOfDay.Dawn: StartDawn(); break;
            }
        }

        public void Dispose()
        {
            _gameHours.Dispose();
            _gameSeconds.Dispose();
            _timeOfDayChanged.Dispose();
            _timeOfDaySubscription.Dispose();
        }

    }
}

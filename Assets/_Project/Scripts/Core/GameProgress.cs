using System;
using System.Collections.Generic;
using R3;

namespace _Project.Scripts.Core
{
    public class GameProgress : IDisposable
    {
        private const float SecondsPerHour = 3600f;
        private const float SecondsPerDay = 24f * SecondsPerHour;

        public TimeOfDay CurrentTimeOfDay => GetTimeOfDay((int)(_gameSeconds.Value % SecondsPerDay / SecondsPerHour));
        private readonly ReactiveProperty<float> _gameSeconds = new(7f * SecondsPerHour);
        private readonly ReadOnlyReactiveProperty<int> _gameHours;
        private readonly ReadOnlyReactiveProperty<int> _currentDay;
        private readonly ReadOnlyReactiveProperty<int> _currentWeek;
        public ReadOnlyReactiveProperty<float> GameSeconds => _gameSeconds;
        public ReadOnlyReactiveProperty<int> GameHours => _gameHours;
        public ReadOnlyReactiveProperty<int> CurrentDay => _currentDay;
        public ReadOnlyReactiveProperty<int> CurrentWeek => _currentWeek;


        private bool _isDisposed;
        public int? LastCaughtFishId { get; private set; }
        private readonly Subject<int> _fishCaught = new();
        public Observable<int> OnFishCaught => _fishCaught;

        /// <summary>1日目7時の時計と、累積秒から求める時刻・日付・週の購読を作成する</summary>
        /// <example>GameLifetimeScopeでSingleton登録して生成する</example>
        public GameProgress()
        {
            _gameHours = _gameSeconds
                .Select(seconds => (int)(seconds % SecondsPerDay / SecondsPerHour))
                .ToReadOnlyReactiveProperty();
            _currentDay = _gameSeconds
                .Select(seconds => (int)(seconds / (double)SecondsPerDay) + 1)
                .ToReadOnlyReactiveProperty();
            _currentWeek = _gameSeconds
                .Select(seconds => (int)(seconds / ((double)SecondsPerDay * 7)) + 1)
                .ToReadOnlyReactiveProperty();
        }

        /// <summary>最後に釣り成功した魚を全釣り場で共有する 戻り値はなし</summary>
        /// <param name="fishId">インベントリへ追加した魚のItemId</param>
        /// <example>魚の追加に成功した直後にRecordFishCaught(fish.ItemId)を呼ぶ</example>
        public void RecordFishCaught(int fishId)
        {
            LastCaughtFishId = fishId;
            _fishCaught.OnNext(fishId);
        }

        private readonly Subject<TimeOfDay> _timeOfDayChanged = new();
        public Observable<TimeOfDay> TimeOfDayChanged => _timeOfDayChanged;

        private readonly HashSet<StoryFlag> storyFlags = new();
        private bool _captureReturnPending;


        /// <summary>有限かつ非負のゲーム内経過秒数だけ進め、日時更新後に変更先の時間帯を通知する</summary>
        /// <param name="seconds">追加するゲーム内秒数 0は何もせず、負数・非有限値・日付のint上限超過は拒否する</param>
        /// <exception cref="ArgumentOutOfRangeException">秒数または更新後の累積時間が有効範囲外の場合</exception>
        /// <exception cref="ObjectDisposedException">破棄後に呼び出した場合</exception>
        /// <example>AdvanceGameTimeSeconds(2f * 3600f)でゲーム内の2時間を早送りする</example>
        public void AdvanceGameTimeSeconds(float seconds)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(GameProgress));

            // 失敗時は時計と派生した日付を一切変更しない
            float targetSeconds = _gameSeconds.Value + seconds;
            if (seconds < 0f || float.IsNaN(seconds) || float.IsInfinity(seconds)
                || float.IsInfinity(targetSeconds)
                || targetSeconds > ((double)int.MaxValue - 1) * SecondsPerDay)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "有効な日付範囲内の有限かつ非負の秒数を指定する");
            }
            if (seconds == 0f) return;

            TimeOfDay previousTimeOfDay = CurrentTimeOfDay;
            _gameSeconds.Value = targetSeconds;

            // 日付・週を含む派生値の更新が終わってから、照明や敵へ最終的な時間帯を通知する
            if (previousTimeOfDay != CurrentTimeOfDay)
                _timeOfDayChanged.OnNext(CurrentTimeOfDay);
        }

        /// <summary>指定した時刻の次の到来まで進める 同時刻なら変更しない</summary>
        /// <param name="hours">到達先のゲーム内時刻 0から23の整数</param>
        /// <exception cref="ArgumentOutOfRangeException">時刻が0から23の範囲外の場合</exception>
        /// <example>8時からAdvanceGameTimeHours(12)で当日12時、19時からなら翌12時へ進む</example>
        public void AdvanceGameTimeHours(int hours)
        {
            AdvanceGameTimeHours(hours, false);
        }

        /// <summary>指定時刻まで進め、nextDayがtrueなら必ず翌日の指定時刻を選ぶ</summary>
        /// <param name="hours">到達先のゲーム内時刻 0から23の整数</param>
        /// <param name="nextDay">trueは翌日、falseは現在以降で最初の指定時刻</param>
        /// <exception cref="ArgumentOutOfRangeException">時刻または更新後の日付が有効範囲外の場合</exception>
        /// <example>7時にAdvanceGameTimeHours(7, true)を呼ぶと翌日7時へ進む</example>
        public void AdvanceGameTimeHours(int hours, bool nextDay)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(GameProgress));
            if (hours < 0 || hours > 23) throw new ArgumentOutOfRangeException(nameof(hours));

            // 同じ時間帯でも時刻を比較し、必要なら日付をまたいで前進する
            double currentSeconds = _gameSeconds.Value;
            double dayStart = Math.Floor(currentSeconds / SecondsPerDay) * SecondsPerDay;
            double targetSeconds = dayStart + hours * SecondsPerHour;
            if (nextDay || targetSeconds < currentSeconds) targetSeconds += SecondsPerDay;
            AdvanceGameTimeSeconds((float)(targetSeconds - currentSeconds));
        }

        /// <summary>指定したゲーム内の経過時間だけ時計を早送りする</summary>
        /// <param name="gameHours">追加するゲーム内時間 小数も指定可能で、有限かつ非負であること</param>
        /// <exception cref="ArgumentOutOfRangeException">経過時間または更新後の日付が有効範囲外の場合</exception>
        /// <example>AdvanceGameTimeByHours(1.5f)で現在から1時間30分進める</example>
        public void AdvanceGameTimeByHours(float gameHours)
        {
            if (gameHours < 0f || float.IsNaN(gameHours) || float.IsInfinity(gameHours))
                throw new ArgumentOutOfRangeException(nameof(gameHours));

            AdvanceGameTimeSeconds(gameHours * SecondsPerHour);
        }

        /// <summary>指定時間帯の開始時刻まで時計を進める 過ぎていれば翌日を選ぶ</summary>
        /// <param name="timeOfDay">昼7時・夕方17時・夜19時・明け方4時の切り替え先</param>
        /// <exception cref="ArgumentOutOfRangeException">未定義の時間帯の場合</exception>
        /// <example>確認用ボタンからAdvanceToTimeOfDay(TimeOfDay.Night)を呼ぶ</example>
        public void AdvanceToTimeOfDay(TimeOfDay timeOfDay)
        {
            int hour = timeOfDay switch
            {
                TimeOfDay.Day => 7,
                TimeOfDay.Evening => 17,
                TimeOfDay.Night => 19,
                TimeOfDay.Dawn => 4,
                _ => throw new ArgumentOutOfRangeException(nameof(timeOfDay))
            };
            AdvanceGameTimeHours(hour);
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

        /// <summary>時計を次の昼7時まで進め、変更があれば時間帯を通知する</summary>
        /// <example>StartDay()で昼の開始時刻へ進める 同じ7時なら変更しない</example>
        public void StartDay() => AdvanceToTimeOfDay(TimeOfDay.Day);

        /// <summary>時計を次の夕方17時まで進め、変更があればBGMと照明へ通知する</summary>
        /// <example>最初の昼釣りが完了したときにStartEvening()を呼ぶ</example>
        public void StartEvening() => AdvanceToTimeOfDay(TimeOfDay.Evening);

        /// <summary>時計を次の夜19時まで進め、変更があれば時間帯を通知する</summary>
        /// <example>StartNight()で次の夜の開始時刻へ進める</example>
        public void StartNight() => AdvanceToTimeOfDay(TimeOfDay.Night);

        /// <summary>時計を次の明け方4時まで進め、変更があれば照明や敵へ通知する</summary>
        /// <example>夜釣りから帰還したときにStartDawn()を呼ぶ</example>
        public void StartDawn() => AdvanceToTimeOfDay(TimeOfDay.Dawn);

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
        /// ゲーム内時刻から、その時刻の時間帯を判定する
        /// </summary>
        /// <param name="hour">0から23のゲーム内時刻</param>
        /// <returns>指定時刻に対応する昼・夕方・夜・明け方</returns>
        /// <example>GetTimeOfDay(19)はTimeOfDay.Nightを返す</example>
        private static TimeOfDay GetTimeOfDay(int hour)
        {
            if(hour >= 19 || hour < 4) return TimeOfDay.Night;
            if(hour >= 17) return TimeOfDay.Evening;
            if(hour >= 7)  return TimeOfDay.Day;
            return TimeOfDay.Dawn;
        }

        /// <summary>時計と時刻・日付・週の派生購読と時間帯通知を一度だけ解放する</summary>
        /// <example>Root破棄時、または検証のusing終了時に呼ばれる</example>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _timeOfDayChanged.Dispose();
            _fishCaught.Dispose();
            _currentWeek.Dispose();
            _currentDay.Dispose();
            _gameHours.Dispose();
            _gameSeconds.Dispose();
        }

    }
}

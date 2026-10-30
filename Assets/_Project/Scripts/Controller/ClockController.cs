using System;
using R3;

namespace Controller
{
    /// <summary>複数の所有者から受け取ったゲーム内時計の自動進行停止を管理する</summary>
    public sealed class ClockController
    {
        private int _pauseCount;

        public bool IsPaused => _pauseCount > 0;

        /// <summary>メインスレッドから時計の自動進行を止め、今回の停止だけを解除する権利を返す</summary>
        /// <returns>Disposeで一度だけ停止を解除するオブジェクト 他の停止要求は維持する</returns>
        /// <example>会話開始時にPause()の戻り値を保持し、終了・中断・破棄時にDisposeする</example>
        public IDisposable Pause()
        {
            // 各所有者が自分の停止だけを解除し、最後の要求がなくなったら再開する
            _pauseCount++;
            return Disposable.Create(() => _pauseCount--);
        }
    }
}

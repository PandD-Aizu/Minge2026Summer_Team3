using System;
using R3;

namespace Controller
{
    /// <summary>複数の所有者から受け取ったゲーム内時計の自動進行停止を管理する</summary>
    public sealed class ClockController
    {
        private int _pauseCount;

        public bool IsPaused => _pauseCount > 0;

        // <summary>時計の進行を止める</summary>
        public IDisposable Pause()
        {
            // 各所有者が自分の停止だけを解除し、最後の要求がなくなったら再開する
            _pauseCount++;
            return Disposable.Create(() => _pauseCount--);
        }
    }
}

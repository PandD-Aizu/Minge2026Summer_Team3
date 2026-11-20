using System;
using R3;

namespace Quota
{
    // ノルマの進捗を管理する
    public class QuotaService
    {
        private readonly Subject<int> _onProgressChanged = new ();
        public Observable<int> OnProgressChanged => _onProgressChanged;
        private int _progress;
        public int Progress => _progress;


        // ノルマの進捗に足していいか確認する
        public void CheckCanApplyQuotaProgress(int fishId, int amount)
        {
                AddProgress(amount);
        }


        private void AddProgress(int amount)
        {
            _progress += amount;
            _onProgressChanged.OnNext(_progress);
        }

        public void ResetProgress()
        {
            _progress = 0;
            _onProgressChanged.OnNext(_progress);
        }

    }
}

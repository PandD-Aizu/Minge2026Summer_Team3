using System;
using _Project.Scripts.Core;
using FMOD;
using R3;

namespace Quota
{
    public class QuotaJudge : IDisposable
    {
        private GameProgress _gameProgress;
        private readonly QuotaService _quotaService;
        private readonly QuotaPresenter _quotaPresenter;

        //private bool _isJudged  = false;


        private QuotaData _currentQuota;

        private readonly IDisposable _gameSecondSubscription;

        public QuotaJudge(GameProgress gameProgress, QuotaService quotaService, QuotaPresenter presenter)
        {
            _gameProgress = gameProgress;
            _quotaService = quotaService;
            _quotaPresenter = presenter;
            _gameSecondSubscription = gameProgress.CurrentWeek.Subscribe(JudgeCompletedQuota);

        }



        // 毎週頭に確認する。
        private void JudgeCompletedQuota(int week)
        {
            float seconds = _gameProgress.GameSeconds.CurrentValue;
            if (_quotaPresenter.DeadlineSeconds > seconds) return;

            _currentQuota = _quotaPresenter.CurrentQuota.Value;
            if (_currentQuota.QuotaAmount <= _quotaService.Progress)
            {
                UnityEngine.Debug.Log("成功");
            }
            else UnityEngine.Debug.Log("GameOver");

        }

        public void Dispose()
        {
            _gameSecondSubscription.Dispose();
        }





    }
}

using System;
using _Project.Scripts.Core;
using Controller;
using R3;
using VContainer.Unity;

namespace _Project.Scripts.View
{
    /// <summary>夜の練習ノルマを開始し、確定した釣果を表示に反映する</summary>
    public sealed class QuotaPresenter : IInitializable, IDisposable
    {
        private const int TutorialTarget = 1;
        private readonly QuotaView _view;
        private readonly TutorialController _tutorial;
        private readonly GameProgress _progress;
        private readonly CompositeDisposable _subscriptions = new();
        private bool _active;
        private int _caught;

        /// <summary>ノルマの表示先とゲーム進行を受け取る</summary>
        /// <param name="view">発表と進捗の表示先</param>
        /// <param name="tutorial">夜パートの開始を通知するController</param>
        /// <param name="progress">確定した釣果の通知元</param>
        /// <example>GameLifetimeScopeのエントリーポイントとして登録する</example>
        public QuotaPresenter(QuotaView view, TutorialController tutorial, GameProgress progress)
        {
            _view = view;
            _tutorial = tutorial;
            _progress = progress;
        }

        /// <summary>チュートリアル進行と釣果の通知を購読する</summary>
        /// <example>Rootコンテナの初期化時に呼ばれる</example>
        public void Initialize()
        {
            _subscriptions.Add(_tutorial.OnStepChanged.Subscribe(OnStepChanged));
            _subscriptions.Add(_progress.OnFishCaught.Subscribe(_ => OnFishCaught()));
            OnStepChanged(_tutorial.CurrentStep);
        }

        /// <summary>夜の釣りへ出発する時点で練習ノルマを発表する</summary>
        /// <param name="step">変更後のチュートリアル段階</param>
        /// <example>夜釣り前のラジオ会話が終わるとGoNightFishingを受け取る</example>
        private void OnStepChanged(TutorialStep step)
        {
            if (step == TutorialStep.GoNightFishing && !_active)
            {
                _active = true;
                _caught = 0;
                _view.SetProgress(_caught, TutorialTarget);
                _view.Present("魚を1匹釣る", "今夜 0:00");
            }
            else if (step == TutorialStep.Completed)
            {
                _active = false;
                _view.Hide();
            }
        }

        /// <summary>練習ノルマの期間内に獲得した魚を1匹加算する</summary>
        /// <example>所持品に魚が追加された後の釣果通知から呼ぶ</example>
        private void OnFishCaught()
        {
            if (!_active) return;
            _view.SetProgress(++_caught, TutorialTarget);
        }

        /// <summary>Rootの破棄時に購読を解除する</summary>
        /// <example>コンテナ破棄時に呼ばれる</example>
        public void Dispose() => _subscriptions.Dispose();
    }
}

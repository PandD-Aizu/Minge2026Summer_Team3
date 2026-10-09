using Controller;
using NavigationArrowServices;
using UnityEngine;
using VContainer.Unity;
using View;

namespace Presentation
{
    /// <summary>ラジオ、釣り場、初回釣果の持ち帰り先へ進行に合わせて光る経路を案内する</summary>
    public class NavigationArrowPresenter : ILateTickable
    {
        private readonly NavigationArrowView _view;
        private readonly NavigationArrowService _service;
        private readonly TutorialController _tutorial;
        private float _nextPathTime;

        /// <summary>経路計算と表示、チュートリアル進行を接続する</summary>
        /// <param name="view">光る経路の描画先</param>
        /// <param name="service">プレイヤーから目的地までの経路計算</param>
        /// <param name="tutorial">ラジオ会話の完了状態</param>
        /// <example>LifetimeScopeからVContainerが生成する</example>
        public NavigationArrowPresenter(NavigationArrowView view, NavigationArrowService service,
            TutorialController tutorial)
        {
            _view = view;
            _service = service;
            _tutorial = tutorial;
        }

        /// <summary>釣りへの出発、初回の魚、釣果を持ち帰る無人集荷場を進行に合わせて案内する</summary>
        /// <example>VContainerがLateUpdateで呼ぶ</example>
        public void LateTick()
        {
            if (_view == null) return;

            // 現在のシーンと進行段階から案内先を選ぶ
            var step = _tutorial.CurrentStep;
            var guideRadio = !_service.GuideFirstFish && step == TutorialStep.TalkToRadioFirst;
            var guideFirstFish = _service.GuideFirstFish && _tutorial.IsFirstFishingPending;
            var guideFishingStage = !_service.GuideFirstFish
                && (step == TutorialStep.CatchDayFish || step == TutorialStep.GoNightFishing);
            var guideReturn = !_tutorial.IsFirstCollectionAccessed && (step == TutorialStep.ReturnToCollectionAtNight
                || (!_service.GuideFirstFish && (step == TutorialStep.TalkToRadioBeforeFirstExchange
                    || step == TutorialStep.ExchangeFirstFish)));
            Vector3? destination = guideRadio || guideFirstFish ? _service.RadioPosition
                : guideFishingStage ? _service.FishingStagePosition
                : guideReturn ? _service.FirstCatchReturnPosition : null;
            if (!_view.CanUpdate || !_service.CanUpdate
                || !destination.HasValue)
            {
                _view.Hide();
                _nextPathTime = 0f;
                return;
            }

            // 目的地変更時は前の経路に残った粒を消して、そのフレームで再計算する
            var target = destination.Value;
            if (_service.TargetPosition != target)
            {
                _view.Hide();
                _service.SetTargetPosition(target);
                _nextPathTime = 0f;
            }

            // 経路探索は一定間隔に抑え、光の放出はViewに任せる
            if (Time.time < _nextPathTime) return;
            _nextPathTime = Time.time + 0.25f;

            if (_service.TryCalculatePath(out var corners)) _view.ShowPath(corners);
            else _view.Hide();
        }
    }
}

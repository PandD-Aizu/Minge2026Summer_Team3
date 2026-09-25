using NavigationArrowServices;
using VContainer.Unity;
using View;

namespace Presentation
{
    public class NavigationArrowPresenter : ILateTickable
    {
        private readonly NavigationArrowView _view;
        private readonly NavigationArrowService _service;

        /// <summary>方向の計算と表示を接続する</summary>
        /// <param name="view">矢印の描画を担当するView</param>
        /// <param name="service">プレイヤーと目的地を保持し、水平方向を計算するサービス</param>
        /// <example>LifetimeScopeからVContainerが生成する</example>
        public NavigationArrowPresenter(NavigationArrowView view, NavigationArrowService service)
        {
            _view = view;
            _service = service;
        }

        /// <summary>プレイヤーの移動後にCubeの位置と向きを更新する</summary>
        /// <example>VContainerがLateUpdateで呼ぶ</example>
        public void LateTick()
        {
            if (_view == null || !_view.CanUpdate || !_service.CanUpdate) return;

            // Serviceから現在の座標と目的地方向を取得して描画へ渡す
            var playerPosition = _service.PlayerPosition;
            var direction = _service.CalculateArrowDirection(playerPosition);
            _view.ShowDirection(playerPosition, direction);
        }
    }
}

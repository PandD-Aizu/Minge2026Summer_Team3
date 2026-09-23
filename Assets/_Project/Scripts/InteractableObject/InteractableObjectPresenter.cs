using System;
using R3;
using VContainer.Unity;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>操作対象の接近状態をマークの表示に反映する</summary>
    public class InteractableObjectPresenter : IInitializable, IDisposable
    {
        private readonly InteractableConnector _connector;
        private readonly InteractableObjectView _view;
        private readonly CompositeDisposable _disposables = new();

        /// <summary>対象ごとの接近判定と表示を接続する</summary>
        /// <param name="connector">同じ対象に属する接近判定</param>
        /// <param name="view">対象の頭上に表示するマーク</param>
        /// <example>InteractableObjectLifetimeScopeからVContainerが生成する</example>
        public InteractableObjectPresenter(InteractableConnector connector, InteractableObjectView view)
        {
            _connector = connector;
            _view = view;
        }

        /// <summary>初期表示を同期してから接近状態を購読する</summary>
        /// <example>対象のLifetimeScopeが起動したときに呼ぶ</example>
        public void Initialize()
        {
            // 開始時点で接近済みの場合も現在の状態を反映する
            _view.ShowInteractionImage(_connector.IsPlayerNearby);
            _connector.OnPlayerNearbyChanged
                .Subscribe(nearby =>
                {
                    if (_view != null) _view.ShowInteractionImage(nearby);
                })
                .AddTo(_disposables);
        }

        /// <summary>購読を解除し、対象の表示を終了する</summary>
        /// <example>LifetimeScopeの破棄時にVContainerが呼ぶ</example>
        public void Dispose()
        {
            _disposables.Dispose();
            if (_view != null) _view.ShowInteractionImage(false);
        }
    }
}

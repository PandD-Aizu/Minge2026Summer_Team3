using System;
using R3;
using VContainer.Unity;

namespace _Project.Scripts.InteractableObject
{
    /// <summary>
    /// InteractableObjectのPresenterクラス
    /// </summary>
    public class InteractableObjectPresenter : IInitializable, IDisposable
    {
        private readonly InteractableConnector _connector;
        private readonly InteractableObjectView _view;

        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        public InteractableObjectPresenter(InteractableConnector connector, InteractableObjectView view)
        {
            _connector = connector;
            _view = view;
        }

        public void Initialize()
        {
            // 接続用コンポーネントのイベントを購読し、Viewの表示/非表示を切り替える
            _connector.OnTriggerStayObservable
                .Subscribe(_ => _view.ShowInteractionImage(true))
                .AddTo(_disposables);
            _connector.OnTriggerExitObservable
                .Subscribe(_ => _view.ShowInteractionImage(false))
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}

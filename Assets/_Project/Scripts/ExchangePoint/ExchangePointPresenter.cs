using System;
using _Project.Scripts.InteractableObject;
using ExchangePoint;
using R3;
using VContainer.Unity;

namespace ExchangePoint
{
    public class ExchangePointPresenter : IInitializable, ITickable, IDisposable
    {
        private readonly ExchangePointService _service;
        private readonly ExchangePointView _view;
        private readonly InteractableConnector _connector;
        private readonly PlayerInputReader _inputReader;

        private CompositeDisposable _disposables = new CompositeDisposable();

        public ExchangePointPresenter(ExchangePointService service, ExchangePointView view, InteractableConnector connector, PlayerInputReader inputReader)
        {
            _service = service;
            _view = view;
            _connector = connector;
            _inputReader = inputReader;
        }

        public void Initialize()
        {
            _inputReader.OnInteractPressed
                .Subscribe(_ =>
                {
                    if (_connector.IsPlayerNearby)
                    {
                        _view.SetVisible(true);
                    }
                })
                .AddTo(_disposables);

            _inputReader.OnCancelPressed
                .Subscribe(_ =>
                {
                    if (_view.IsVisible)
                    {
                        _view.SetVisible(false);
                    }
                })
                .AddTo(_disposables);
        }

        public void Tick()
        {
            if (_view.IsVisible && !_connector.IsPlayerNearby)
            {
                _view.SetVisible(false);
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}

using System;
using NavigationArrowServices;
using VContainer.Unity;
using View;

namespace Presentation
{
    public class NavigationArrowPresenter : IInitializable, ITickable, IDisposable
    {
        private NavigationArrowView _view;
        private NavigationArrowService _service;

        public NavigationArrowPresenter(NavigationArrowView view, NavigationArrowService service)
        {
            _view = view;
            _service = service;
        }

        public void Initialize()
        {

        }

        public void Tick()
        {

        }

        public void Dispose()
        {

        }
    }
}

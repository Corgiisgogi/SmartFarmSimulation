using System;
using System.Windows.Threading;

namespace SmartFarmUI.Infrastructure
{
    public static class DispatcherHelper
    {
        private static Dispatcher _dispatcher;

        public static void Initialize(Dispatcher dispatcher)
            => _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        public static void RunOnUI(Action action)
        {
            if (_dispatcher == null) { action(); return; }
            if (_dispatcher.CheckAccess()) action();
            else _dispatcher.BeginInvoke(action);
        }
    }
}

using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace Stokbox.App.Tests
{
    /// <summary>
    /// WPF elements must be created and rendered on a single-threaded apartment thread; xUnit's are not.
    /// </summary>
    internal static class Sta
    {
        public static void Run(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }
}

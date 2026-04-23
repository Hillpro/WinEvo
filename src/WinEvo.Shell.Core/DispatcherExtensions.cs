using Microsoft.UI.Dispatching;

namespace WinEvo.Shell.Core;

internal static class DispatcherExtensions
{
    extension(DispatcherQueue dispatcher)
    {
        /// <summary>
        /// Runs <paramref name="action"/> on the dispatcher thread and completes
        /// once it has executed (propagating any exception it threw). Fast-paths
        /// when the caller is already on the dispatcher thread. Callers should
        /// prefer this helper over relying on a captured <see cref="SynchronizationContext"/>
        /// — see <c>App.xaml.cs</c> for why that isn't reliable under WinUI 3.
        /// </summary>
        public Task RunOnUiAsync(Action action)
        {
            if (dispatcher.HasThreadAccess)
            {
                action();
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource();
            var enqueued = dispatcher.TryEnqueue(() =>
            {
                try { action(); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            if (!enqueued)
                tcs.SetException(new InvalidOperationException("failed to enqueue UI update"));
            return tcs.Task;
        }
    }
}

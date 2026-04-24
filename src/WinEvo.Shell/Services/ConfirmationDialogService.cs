using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinEvo.Shell.Core.Services;
using WinEvo.Shell.Views;

namespace WinEvo.Shell.Services;

/// <summary>
/// Concrete <see cref="IConfirmationService"/>: shows a severity-adapted
/// <see cref="ConfirmationDialog"/> over the Shell's main window. The XamlRoot
/// is captured via a lazy provider so this service can be constructed before
/// the window is activated — the dialog only consults the provider when a
/// confirmation is actually requested, by which point the window is live.
/// </summary>
internal sealed class ConfirmationDialogService : IConfirmationService
{
    private readonly Func<XamlRoot?> _xamlRootProvider;

    public ConfirmationDialogService(Func<XamlRoot?> xamlRootProvider)
    {
        _xamlRootProvider = xamlRootProvider;
    }

    public async Task<bool> RequestAsync(ConfirmationRequest request, CancellationToken ct)
    {
        var xamlRoot = _xamlRootProvider()
            ?? throw new InvalidOperationException(
                "XamlRoot is not available; the main window has not been activated yet.");

        var dialog = new ConfirmationDialog(request) { XamlRoot = xamlRoot };

        // If the caller cancels mid-show, hide the dialog so ShowAsync resolves
        // promptly with None (which we map to a declined confirmation below).
        using var registration = ct.Register(dialog.Hide);
        var result = await dialog.ShowAsync();

        return !ct.IsCancellationRequested && result == ContentDialogResult.Primary;
    }
}

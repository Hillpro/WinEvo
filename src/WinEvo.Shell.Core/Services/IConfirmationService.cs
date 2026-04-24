using WinEvo.ActionModel;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Asks the user to confirm an action whose manifest declares warnings.
/// Implementations render a severity-adapted dialog; the VM only supplies the
/// resolved content and awaits a boolean go/no-go.
/// </summary>
/// <remarks>
/// The abstraction exists so view models in <c>Shell.Core</c> do not take a
/// dependency on XAML-view construction (the <see cref="Microsoft.UI.Xaml.XamlRoot"/>
/// captured by the concrete service is owned by the Shell window).
/// </remarks>
public interface IConfirmationService
{
    Task<bool> RequestAsync(ConfirmationRequest request, CancellationToken ct);
}

/// <summary>A request to confirm execution of an action.</summary>
/// <param name="ActionName">The action's localized display name — also used as
/// the typed-phrase challenge string for <see cref="WarningSeverity.Critical"/>.</param>
/// <param name="Warnings">Warnings to surface, deduplicated by key with the
/// highest severity per key retained. Order matters: shown top-to-bottom.</param>
public sealed record ConfirmationRequest(
    string ActionName,
    IReadOnlyList<ResolvedWarning> Warnings)
{
    public WarningSeverity MaxSeverity => Warnings.Count == 0
        ? WarningSeverity.Info
        : Warnings.Max(w => w.Severity);
}

/// <summary>A single warning ready for display (key already resolved against a bundle).</summary>
public sealed record ResolvedWarning(WarningSeverity Severity, string Message);

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.Services;

namespace WinEvo.Shell.Views;

/// <summary>
/// Severity-adapted confirmation dialog. Behaviour by <see cref="WarningSeverity"/>:
/// <list type="bullet">
///   <item><c>Info</c> / <c>Warning</c>: banner + warning list; primary button enabled immediately.</item>
///   <item><c>Danger</c>: adds an "I understand" checkbox; primary button gated on it.</item>
///   <item><c>Critical</c>: adds a typed-phrase challenge (the action name); primary button gated on both checkbox and exact typed match.</item>
/// </list>
/// Glyphs come from the Segoe Fluent Icons font. Private-Use-Area code points
/// (U+E000-U+F8FF) render as blanks in most code editors, so we keep them as
/// <c>\uXXXX</c> escapes - the source file stays ASCII and diffs stay readable.
/// </summary>
public sealed partial class ConfirmationDialog : ContentDialog
{
    internal const string GlyphInfo = "\uE946";      // Info
    internal const string GlyphWarning = "\uE7BA";   // Warning
    internal const string GlyphDanger = "\uEA39";    // Important
    internal const string GlyphCritical = "\uE814";  // StatusErrorFull

    private string _requiredPhrase = "";

    public ConfirmationDialog(ConfirmationRequest request)
    {
        InitializeComponent();
        Title = request.ActionName;
        ConfigureForSeverity(request.MaxSeverity, request.ActionName);
        WarningsList.ItemsSource = request.Warnings.Select(WarningRow.From).ToList();
    }

    private void ConfigureForSeverity(WarningSeverity severity, string actionName)
    {
        switch (severity)
        {
            case WarningSeverity.Info:
                PrimaryButtonText = "Continue";
                IsPrimaryButtonEnabled = true;
                SeverityLabel.Text = "Information";
                SeverityIcon.Glyph = GlyphInfo;
                SeverityBanner.Background = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x78, 0xD4));
                break;

            case WarningSeverity.Warning:
                PrimaryButtonText = "Continue";
                IsPrimaryButtonEnabled = true;
                SeverityLabel.Text = "Warning";
                SeverityIcon.Glyph = GlyphWarning;
                SeverityBanner.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xB9, 0x00));
                break;

            case WarningSeverity.Danger:
                PrimaryButtonText = "Run anyway";
                IsPrimaryButtonEnabled = false;
                AcknowledgeCheckbox.Visibility = Visibility.Visible;
                SeverityLabel.Text = "Destructive action";
                SeverityIcon.Glyph = GlyphDanger;
                SeverityBanner.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xC4, 0x2B, 0x1C));
                break;

            case WarningSeverity.Critical:
                PrimaryButtonText = "Run anyway";
                IsPrimaryButtonEnabled = false;
                AcknowledgeCheckbox.Visibility = Visibility.Visible;
                TypedPhrasePanel.Visibility = Visibility.Visible;
                _requiredPhrase = actionName;
                TypedPhrasePrompt.Text = $"To confirm, type the action name exactly: {actionName}";
                SeverityLabel.Text = "Critical - irreversible";
                SeverityIcon.Glyph = GlyphCritical;
                SeverityBanner.Background = new SolidColorBrush(Color.FromArgb(0x55, 0xC4, 0x2B, 0x1C));
                break;
        }
    }

    private void OnGateStateChanged(object sender, RoutedEventArgs e) => UpdatePrimaryButtonState();
    private void OnGateStateChanged(object sender, TextChangedEventArgs e) => UpdatePrimaryButtonState();

    private void UpdatePrimaryButtonState()
    {
        var acknowledged = AcknowledgeCheckbox.Visibility != Visibility.Visible
            || AcknowledgeCheckbox.IsChecked == true;
        var phraseMatches = TypedPhrasePanel.Visibility != Visibility.Visible
            || string.Equals(TypedPhraseInput.Text, _requiredPhrase, StringComparison.Ordinal);
        IsPrimaryButtonEnabled = acknowledged && phraseMatches;
    }
}

/// <summary>Row-shaped view of a <see cref="ResolvedWarning"/> for the items control.</summary>
public sealed class WarningRow
{
    public required string Glyph { get; init; }
    public required Brush Foreground { get; init; }
    public required string Message { get; init; }

    public static WarningRow From(ResolvedWarning w) => new()
    {
        Glyph = w.Severity switch
        {
            WarningSeverity.Info => ConfirmationDialog.GlyphInfo,
            WarningSeverity.Warning => ConfirmationDialog.GlyphWarning,
            WarningSeverity.Danger => ConfirmationDialog.GlyphDanger,
            WarningSeverity.Critical => ConfirmationDialog.GlyphCritical,
            _ => ConfirmationDialog.GlyphInfo,
        },
        Foreground = new SolidColorBrush(w.Severity switch
        {
            WarningSeverity.Info => Color.FromArgb(0xFF, 0x00, 0x78, 0xD4),
            WarningSeverity.Warning => Color.FromArgb(0xFF, 0xC7, 0x8A, 0x00),
            WarningSeverity.Danger => Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C),
            WarningSeverity.Critical => Color.FromArgb(0xFF, 0xA8, 0x00, 0x00),
            _ => Color.FromArgb(0xFF, 0x00, 0x78, 0xD4),
        }),
        Message = w.Message,
    };
}

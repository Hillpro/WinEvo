using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace WinEvo.Shell.Views;

/// <summary>
/// Read-only About dialog: app name, version, license, privacy summary, and
/// links out to GitHub and the local privacy / license documents on the
/// repository's main branch.
/// </summary>
public sealed partial class AboutDialog : ContentDialog
{
    private const string RepositoryUrl = "https://github.com/Hillpro/WinEvo";
    private const string PrivacyUrl = RepositoryUrl + "/blob/main/docs/privacy.md";
    private const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";

    public AboutDialog()
    {
        InitializeComponent();

        VersionText.Text = $"Version {ResolveVersion()}";
        CopyrightText.Text = ResolveCopyright();

        GitHubLink.NavigateUri = new Uri(RepositoryUrl);
        PrivacyLink.NavigateUri = new Uri(PrivacyUrl);
        LicenseLink.NavigateUri = new Uri(LicenseUrl);
    }

    private static string ResolveVersion()
    {
        var version = typeof(AboutDialog).Assembly.GetName().Version;
        if (version is null) return "0.0.0";
        // System.Version always carries 4 components; the trailing .0 is noise
        // for a 3-part SemVer-shaped product version.
        return $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private static string ResolveCopyright()
        => typeof(AboutDialog).Assembly
            .GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright
            ?? "";
}

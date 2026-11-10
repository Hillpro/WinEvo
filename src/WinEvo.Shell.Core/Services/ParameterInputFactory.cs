using WinEvo.ActionModel;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Default <see cref="IParameterInputFactory"/>. Per-type data sources (drives
/// today; wifi profiles, installed services, … later) are injected so tests
/// swap in fakes and the VMs never touch the host directly.
/// </summary>
public sealed class ParameterInputFactory : IParameterInputFactory
{
    private readonly IDriveProvider _drives;

    public ParameterInputFactory(IDriveProvider drives)
    {
        _drives = drives;
    }

    public ParameterInputViewModel Create(Parameter parameter, string? language)
        => parameter switch
        {
            DriveParameter dp => new DriveParameterInputViewModel(
                dp, language, _drives.Enumerate(dp.AllowedDriveTypes)),
            IntegerParameter ip => new IntegerParameterInputViewModel(ip, language),
            BooleanParameter bp => new BooleanParameterInputViewModel(bp, language),
            EnumParameter ep => new EnumParameterInputViewModel(ep, language),
            _ => new StringParameterInputViewModel(parameter, language),
        };
}

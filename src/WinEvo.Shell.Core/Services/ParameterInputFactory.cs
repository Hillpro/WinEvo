using System.Globalization;
using WinEvo.Actions.Abstractions;
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

    public ParameterInputViewModel Create(Parameter parameter, CultureInfo culture)
        => parameter switch
        {
            DriveParameter dp => new DriveParameterInputViewModel(
                dp, culture, _drives.Enumerate(dp.AllowedDriveTypes)),
            IntegerParameter ip => new IntegerParameterInputViewModel(ip, culture),
            BooleanParameter bp => new BooleanParameterInputViewModel(bp, culture),
            EnumParameter ep => new EnumParameterInputViewModel(ep, culture),
            StringParameter sp => new StringParameterInputViewModel(sp, culture),
            _ => throw new NotSupportedException(
                $"no input is registered for parameter type {parameter.GetType().Name} ('{parameter.Id}')"),
        };
}

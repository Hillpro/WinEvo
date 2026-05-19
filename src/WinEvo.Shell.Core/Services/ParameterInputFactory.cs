using Microsoft.UI.Dispatching;
using WinEvo.ActionModel;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Default <see cref="IParameterInputFactory"/>. Per-type data sources (drives
/// today; wifi profiles, installed services, … later) are injected so tests
/// swap in fakes and the VMs never touch the host directly. The state-loader
/// is injected here too — only parameters that declare a
/// <see cref="ParameterStateProbe"/> consume it, but pushing it through the
/// factory keeps that knowledge out of the call sites.
/// </summary>
public sealed class ParameterInputFactory : IParameterInputFactory
{
    private readonly IDriveProvider _drives;
    private readonly IParameterStateLoader? _stateLoader;
    private readonly DispatcherQueue? _dispatcher;

    public ParameterInputFactory(
        IDriveProvider drives,
        IParameterStateLoader? stateLoader = null,
        DispatcherQueue? dispatcher = null)
    {
        _drives = drives;
        _stateLoader = stateLoader;
        _dispatcher = dispatcher;
    }

    public ParameterInputViewModel Create(Parameter parameter, string? language)
        => parameter switch
        {
            DriveParameter dp => new DriveParameterInputViewModel(
                dp, language, _drives.Enumerate(dp.AllowedDriveTypes)),
            IntegerParameter ip => new IntegerParameterInputViewModel(ip, language),
            BooleanParameter bp => new BooleanParameterInputViewModel(bp, language, _stateLoader, _dispatcher),
            EnumParameter ep => new EnumParameterInputViewModel(ep, language),
            _ => new StringParameterInputViewModel(parameter, language),
        };
}

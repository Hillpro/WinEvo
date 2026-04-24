using WinEvo.ActionModel;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Services;

/// <summary>
/// Builds the concrete <see cref="ParameterInputViewModel"/> subclass for a
/// given <see cref="Parameter"/>. Adding a new parameter type = one new VM
/// subclass, one new switch arm here, one new template on the Shell's
/// <c>ParameterInputTemplateSelector</c>.
/// </summary>
public interface IParameterInputFactory
{
    ParameterInputViewModel Create(Parameter parameter, string? language);
}

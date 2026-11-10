using System.Globalization;
using WinEvo.Ipc;

namespace WinEvo.Shell.Core.ViewModels;

/// <summary>
/// Says how an execution ended, in the user's language. The sentence is built
/// from the structured response: the agent's own summary is English
/// diagnostic output and is never shown as the status.
/// </summary>
public static class ExecutionOutcomeText
{
    public static string For(ExecutionResponse response, CultureInfo culture)
    {
        if (response.Success)
            return ViewModelText.Get("Status_Succeeded", culture);

        var steps = response.StepResults;
        for (var index = 0; index < steps.Count; index++)
        {
            if (!steps[index].Success)
                return ViewModelText.Format("Status_FailedAtStep", culture, index + 1, steps.Count);
        }

        // Refused before any step ran, or stopped by something other than a step.
        return ViewModelText.Get("Status_Failed", culture);
    }
}

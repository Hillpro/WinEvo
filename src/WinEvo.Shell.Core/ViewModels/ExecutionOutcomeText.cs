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
        var steps = response.StepResults;
        if (response.Success)
        {
            // A step the action could do without failed: the change is made,
            // but the user is told that part of it did not work.
            return steps.Any(step => step.FailureTolerated)
                ? ViewModelText.Get("Status_SucceededWithFailedPart", culture)
                : ViewModelText.Get("Status_Succeeded", culture);
        }

        // Names the step that stopped the action, not an earlier tolerated one.
        for (var index = 0; index < steps.Count; index++)
        {
            if (!steps[index].Success && !steps[index].FailureTolerated)
                return ViewModelText.Format("Status_FailedAtStep", culture, index + 1, steps.Count);
        }

        // Refused before any step ran, or stopped by something other than a step.
        return ViewModelText.Get("Status_Failed", culture);
    }
}

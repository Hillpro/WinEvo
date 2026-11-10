using System.Globalization;
using WinEvo.Ipc;
using WinEvo.Shell.Core.ViewModels;

namespace WinEvo.Shell.Core.Tests;

public class ExecutionOutcomeTextTests
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo s_french = CultureInfo.GetCultureInfo("fr");

    [Fact]
    public void Success_is_reported_without_the_agents_wording()
    {
        var response = new ExecutionResponse
        {
            Success = true,
            Message = "completed 2 step(s)",
            StepResults = [Step(succeeded: true), Step(succeeded: true)],
        };

        Assert.Equal("Done.", ExecutionOutcomeText.For(response, s_english));
        Assert.Equal("Terminé.", ExecutionOutcomeText.For(response, s_french));
    }

    [Fact]
    public void Failure_names_the_first_failed_step_and_the_step_count()
    {
        var response = new ExecutionResponse
        {
            Success = false,
            StepResults = [Step(succeeded: true), Step(succeeded: false), Step(succeeded: false)],
        };

        Assert.Equal("Failed at step 2 of 3.", ExecutionOutcomeText.For(response, s_english));
        Assert.Equal("Échec à l'étape 2 sur 3.", ExecutionOutcomeText.For(response, s_french));
    }

    [Fact]
    public void Failure_with_no_failed_step_is_reported_plainly()
    {
        // What the agent answers when it refuses an action before running it.
        var response = new ExecutionResponse
        {
            Success = false,
            Message = "action requires elevation, but the agent is not running elevated",
        };

        Assert.Equal("Failed.", ExecutionOutcomeText.For(response, s_english));
        Assert.Equal("Échec.", ExecutionOutcomeText.For(response, s_french));
    }

    [Fact]
    public void Language_without_a_translation_falls_back_to_english()
    {
        var response = new ExecutionResponse { Success = true };

        Assert.Equal("Done.", ExecutionOutcomeText.For(response, CultureInfo.GetCultureInfo("de-DE")));
    }

    private static StepResult Step(bool succeeded) => new() { Operation = "registry-set", Success = succeeded };
}

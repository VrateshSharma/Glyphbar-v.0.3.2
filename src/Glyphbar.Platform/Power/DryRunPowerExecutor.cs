using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Platform.Power;

/// <summary>
/// Stands in for <see cref="PowerExecutor"/> when GLYPHBAR_POWER_DRY_RUN=1, so the whole power flow can be
/// exercised without shutting the test machine down. It logs the action and does nothing else.
/// </summary>
public sealed class DryRunPowerExecutor(ILogger<DryRunPowerExecutor> logger) : IPowerExecutor
{
    public const string EnvironmentVariable = "GLYPHBAR_POWER_DRY_RUN";

    public static bool IsEnabled => Environment.GetEnvironmentVariable(EnvironmentVariable) == "1";

    public Task<OperationResult> ExecuteAsync(PowerAction action)
    {
        logger.LogInformation("DRY RUN: would execute {Action}", action);
        return Task.FromResult(OperationResult.Ok);
    }
}

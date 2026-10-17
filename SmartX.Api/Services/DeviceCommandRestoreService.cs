
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartX.Api.Services;

public sealed class DeviceCommandRestoreService
    : IHostedService
{
    private readonly DeviceCommandService _commands;

    private readonly ILogger<DeviceCommandRestoreService>
        _logger;

    public DeviceCommandRestoreService(
        DeviceCommandService commands,
        ILogger<DeviceCommandRestoreService> logger)
    {
        _commands = commands;
        _logger = logger;
    }

    public Task StartAsync(
        CancellationToken cancellationToken)
    {
        _commands.RestoreFromDatabase();

        _logger.LogInformation(
            "Smart-X command state and undo history restored."
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}


using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public class SensorService
{
    private readonly SmartXContext _context;
    private readonly ILogger<SensorService> _logger;

    public SensorService(
        SmartXContext context,
        ILogger<SensorService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Sensor>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Sensors
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while retrieving Smart-X sensors."
            );

            throw;
        }
    }
}

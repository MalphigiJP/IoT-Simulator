using Microsoft.EntityFrameworkCore;
using SmartX.Api.Data;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

public class SensorService
{
    private readonly SmartXContext _context;

    public SensorService(SmartXContext context)
    {
        _context = context;
    }

    public async Task<List<Sensor>> GetAllAsync()
    {
        return await _context.Sensors
            .OrderBy(s => s.Id)
            .ToListAsync();
    }
}
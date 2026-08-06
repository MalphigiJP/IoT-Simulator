using Microsoft.EntityFrameworkCore;
using SmartX.Api.Models;

namespace SmartX.Api.Data;

public class SmartXContext : DbContext
{
    public SmartXContext(DbContextOptions<SmartXContext> options)
        : base(options)
    {
    }

    public DbSet<Sensor> Sensors => Set<Sensor>();
}
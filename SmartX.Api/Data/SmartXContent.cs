using Microsoft.EntityFrameworkCore;
using SmartX.Api.Models;

namespace SmartX.Api.Data;

public class SmartXContext : DbContext
{
    public SmartXContext(
        DbContextOptions<SmartXContext> options)
        : base(options)
    {
    }

    public DbSet<Sensor> Sensors => Set<Sensor>();

    public DbSet<TelemetryRecord> TelemetryRecords =>
        Set<TelemetryRecord>();

    public DbSet<DeviceCommand> DeviceCommands =>
    Set<DeviceCommand>();

    public DbSet<DeviceControlState> DeviceControlStates =>
        Set<DeviceControlState>();

    public DbSet<DeviceCommandAuditEvent> DeviceCommandAuditEvents =>
        Set<DeviceCommandAuditEvent>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TelemetryRecord>()
            .HasOne(t => t.Sensor)
            .WithMany(s => s.TelemetryRecords)
            .HasForeignKey(t => t.SensorId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TelemetryRecord>()
            .HasIndex(t => t.SensorId);

        modelBuilder.Entity<TelemetryRecord>()
            .HasIndex(t => t.Timestamp);
    }
}
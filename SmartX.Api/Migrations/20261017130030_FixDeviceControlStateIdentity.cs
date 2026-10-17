using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartX.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixDeviceControlStateIdentity : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
        IF OBJECT_ID(N'dbo.DeviceControlStates', N'U') IS NULL
            THROW 51000, 'DeviceControlStates table was not found.', 1;

        IF OBJECT_ID(N'dbo.DeviceControlStates_IdentityFix', N'U') IS NOT NULL
            THROW 51001, 'The repair table already exists.', 1;

        IF EXISTS (
            SELECT 1
            FROM sys.foreign_keys
            WHERE parent_object_id = OBJECT_ID(N'dbo.DeviceControlStates')
               OR referenced_object_id = OBJECT_ID(N'dbo.DeviceControlStates')
        )
            THROW 51002, 'Foreign keys require a custom table migration.', 1;

        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.DeviceControlStates')
              AND is_primary_key = 0
              AND index_id > 0
        )
            THROW 51003, 'Additional indexes require a custom migration.', 1;

        IF EXISTS (
            SELECT 1
            FROM sys.triggers
            WHERE parent_id = OBJECT_ID(N'dbo.DeviceControlStates')
        )
            THROW 51004, 'Table triggers require a custom migration.', 1;

        CREATE TABLE [dbo].[DeviceControlStates_IdentityFix] (
            [SensorId] int NOT NULL,
            [MacAddress] nvarchar(max) NOT NULL,
            [Category] nvarchar(max) NOT NULL,
            [SupportedTarget] int NULL,
            [DesiredState] bit NULL,
            [ReportedState] bit NULL,
            [LastCommandId] uniqueidentifier NULL,
            [LastDesiredChangeAtUtc] datetime2 NULL,
            [LastReportedAtUtc] datetime2 NULL,
            [Revision] bigint NOT NULL
        );

        INSERT INTO [dbo].[DeviceControlStates_IdentityFix] (
            [SensorId],
            [MacAddress],
            [Category],
            [SupportedTarget],
            [DesiredState],
            [ReportedState],
            [LastCommandId],
            [LastDesiredChangeAtUtc],
            [LastReportedAtUtc],
            [Revision]
        )
        SELECT
            [SensorId],
            [MacAddress],
            [Category],
            [SupportedTarget],
            [DesiredState],
            [ReportedState],
            [LastCommandId],
            [LastDesiredChangeAtUtc],
            [LastReportedAtUtc],
            [Revision]
        FROM [dbo].[DeviceControlStates];

        DROP TABLE [dbo].[DeviceControlStates];

        EXEC sys.sp_rename
            N'dbo.DeviceControlStates_IdentityFix',
            N'DeviceControlStates',
            N'OBJECT';

        EXEC(N'
            ALTER TABLE [dbo].[DeviceControlStates]
            ADD CONSTRAINT [PK_DeviceControlStates]
            PRIMARY KEY ([SensorId])
        ');
        """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Reverting this migration would reintroduce the incorrect identity configuration."
            );
        }

    }
}

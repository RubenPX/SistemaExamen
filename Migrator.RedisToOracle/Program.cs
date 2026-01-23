using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Migrator.RedisToOracle.DB;
using Migrator.RedisToOracle.Workers;
using Oracle.ManagedDataAccess.Client;

var builder = Host.CreateApplicationBuilder(args);

// Integración de Aspire
builder.AddRedisClient("redis", settings => {
    settings.DisableTracing = true;
});

builder.AddOracleDatabaseDbContext<ExamenDBContext>("FREEPDB1", settings => {
    settings.DisableTracing = true;
}, configureDbContextOptions: options => {
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.CommandExecuted));
});

// Desactiva el rastro interno del cliente de Oracle
OracleConfiguration.TraceLevel = 0;

builder.Services.AddHostedService<RedisToOracleCustomWorker>();

var host = builder.Build();

// --- Lógica de creación automática ---
using (var scope = host.Services.CreateScope()) {
    try {
        Console.WriteLine("Eliminando...");
        var dbContext = scope.ServiceProvider.GetRequiredService<ExamenDBContext>();
        dbContext.Database.EnsureCreated(["FREEPDB1"]);

        Console.WriteLine("OK");
    } catch (Exception ex) {
        Console.WriteLine("[Oracle Exporter] Error probablemente ya existe la tabla...");
    }

    try {
        Console.WriteLine("Reconstruyendo indices");
        var dbContext = scope.ServiceProvider.GetRequiredService<ExamenDBContext>();
        await dbContext.Database.ExecuteSqlAsync($"ALTER INDEX SYSTEM.PK_FREEPDB1 REBUILD;");
        Console.WriteLine("OK!");
    } catch (Exception) {
        throw;
    }
}

host.Run();

using System.Data;
using System.Text.Json;
using Dapper;
using Examenes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Migrator.RedisToOracle.DB;
using Migrator.RedisToOracle.DB.Entity;
using Migrator.RedisToOracle.DB.Entity.Mappers;
using Oracle.ManagedDataAccess.Client;
using StackExchange.Redis;

namespace Migrator.RedisToOracle.Workers;

internal class RedisToOracleCustomWorker(IConnectionMultiplexer redis, ExamenDBContext context, IHostApplicationLifetime lifetime) : BackgroundService {
    private const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        lifetime.ApplicationStopping.Register(() => {
            Console.WriteLine("El Host se está deteniendo... Abortando proceso de Oracle.");
        });

        var db = redis.GetDatabase();
        var conex = context.Database.GetDbConnection() as OracleConnection;
        await conex.OpenAsync();

        await conex.ExecuteAsync("ALTER TABLE FREEPDB1 NOLOGGING");
        try { await conex.ExecuteAsync("DROP INDEX FREEPDB1.idx_evento_id"); } catch { /* Por si no existe */ }

        Console.WriteLine("[Oracle Exporter] Iniciando migración de alta frecuencia Redis -> Oracle...");

        long itemsTotales = await db.ListLengthAsync("cola:examen");
        if (itemsTotales == 0) {
            Console.WriteLine("[Oracle Exporter] No hay datos pendientes en Redis.");
            return;
        }

        int errores = 0;

        long itemsProcesados = 0;
        while (!stoppingToken.IsCancellationRequested) {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var items = await db.ListRightPopAsync("cola:examen", BatchSize);

            // Extracción
            var lote = new AccionDB[0];
            if (items != null && items.Length != 0) {
                lote = items
                    .AsParallel()
                    .Select(item => JsonSerializer.Deserialize((string)item!, SourceGenerationContext.Default.AccionEvento))
                    .Select(item => item.ToEntity())
                    .ToArray();
            } else {
                await Task.Delay(1000);
                break;
            }
            var idsDuplicados = lote.GroupBy(e => e.EventoId)
                                    .Where(g => g.Count() > 1)
                                    .Select(g => new { Id = g.Key, Cantidad = g.Count() })
                                    .ToList();

            if (idsDuplicados.Count > 0) {
                foreach (var item in idsDuplicados) {
                    Console.WriteLine($"[Oracle Exporter][ERROR] ID DULPICADO: {item.ToString()}");
                }
                Console.WriteLine("[Oracle Exporter][ERROR] Se han detectado 2 eventos con el mismo ID... Parando ejecución");
                return;
            }

            // Envio
            try {
                await GuardarEnOracleBulk(conex, lote);

                itemsProcesados += lote.Length;
                double porcentaje = (double)itemsProcesados / itemsTotales * 100;
                sw.Stop();
                Console.WriteLine($"[Oracle Exporter] {porcentaje:F2}% | {itemsProcesados}/{itemsTotales} registros (enviado {lote.Length} registros en {sw.Elapsed.TotalMilliseconds:F2})");
            } catch (Exception ex) {
                Console.WriteLine($"[Oracle Exporter][ERROR] fallo al procesar items. Devolviendo a redis... | {ex.Message}");
                await db.ListLeftPushAsync("cola:examen", items);
                errores++;
                if (errores > 5) {
                    Console.WriteLine($"[Oracle Exporter][ERROR] Se han detectado mas de 10 errores. Parando ejecución...");
                    return;
                }
            }
        }

        await conex.ExecuteAsync("CREATE INDEX idx_evento_id ON FREEPDB1(EventoId)");
        await conex.ExecuteAsync("ALTER TABLE FREEPDB1 LOGGING");

    }
    private async Task GuardarEnOracleBulk(OracleConnection conn, AccionDB[] eventos) {
        using var dt = new DataTable();
        dt.Columns.Add("EventoId");
        dt.Columns.Add("AlumnoId", typeof(int));
        dt.Columns.Add("ExamenId", typeof(int));
        dt.Columns.Add("AccionId", typeof(int));
        dt.Columns.Add("PreguntaId", typeof(int));
        dt.Columns.Add("Valor");
        dt.Columns.Add("Timestamp", typeof(DateTime));
        dt.MinimumCapacity = eventos.Length;

        foreach (var e in eventos)
            dt.Rows.Add(e.EventoId, e.AlumnoId, e.ExamenId, e.AccionId, e.PreguntaId, e.Valor, e.Timestamp);

        // Configuración de BulkCopy optimizada
        using var bulk = new OracleBulkCopy(conn, OracleBulkCopyOptions.Default) {
            DestinationTableName = "FREEPDB1",
            BulkCopyTimeout = 300,
            BatchSize = 10000 // Coincide con el tamaño de nuestro buffer
        };
        await Task.Run(() => bulk.WriteToServer(dt));
    }
}


using System;
using System.Text.Json;
using Examenes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Migrator.RedisToOracle.DB;
using Migrator.RedisToOracle.DB.Entity;
using Migrator.RedisToOracle.DB.Entity.Mappers;
using OpenTelemetry;
using StackExchange.Redis;

namespace Migrator.RedisToOracle.Workers;

internal class RedisToOracleWorker(IConnectionMultiplexer redis, IServiceScopeFactory scopeFactory, ExamenDBContext context) : BackgroundService {
    private const int BatchSize = 10_000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var db = redis.GetDatabase();
        Console.WriteLine("[Oracle Exporter] Iniciando migración de alta frecuencia Redis -> Oracle...");

        // Eliminamos el tracking
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

        long itemsTotales = await db.ListLengthAsync("cola:examen");
        if (itemsTotales == 0) {
            Console.WriteLine("[Oracle] No hay datos pendientes en Redis.");
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
                await context.Acciones.AddRangeAsync(lote, stoppingToken);
                await context.SaveChangesAsync(stoppingToken);

                itemsProcesados += lote.Length;
                double porcentaje = (double)itemsProcesados / itemsTotales * 100;
                sw.Stop();
                Console.WriteLine($"[Oracle Exporter] {porcentaje:F2}% | {itemsProcesados}/{itemsTotales} registros (enviado {lote.Length} registros en {sw.Elapsed.TotalMilliseconds:F0} ms)");
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
    }
}

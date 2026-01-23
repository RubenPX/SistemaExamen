using System.Text.Json.Serialization;
using System.Threading.Channels;
using Examenes.Domain;
using Examenes.Server.BackgroundServices;
using Examenes.Server.Exporters;
using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE ASPIRE Y SERVICIOS ---
builder.AddServiceDefaults();
builder.AddRedisClient("redis", cfg => cfg.DisableTracing = true);

builder.Services.AddSignalR();

// Canales de trabajo
builder.Services.AddSingleton(ChannelManager.channelSIGANLR.Writer);
builder.Services.AddSingleton(ChannelManager.channelSIGANLR.Reader);
builder.Services.AddSingleton(ChannelManager.channelREDIS.Writer);
builder.Services.AddSingleton(ChannelManager.channelREDIS.Reader);

// Worker que mueve datos del Canal a Redis
builder.Services.AddHostedService<RedisIngestionWorker>();
builder.Services.AddHostedService<ChannelMonitor>();

// Worker que maneja Oracle
builder.Services.AddSingleton<OracleExporterService>();

var app = builder.Build();

// --- 2. INICIALIZACIÓN ---
app.MapDefaultEndpoints();

// --- 3. ENDPOINTS ---
app.MapHub<ExamenHub>("/examenHub");

// Endpoint de exportación con porcentaje
app.MapGet("/api/finalizarexamen", async (OracleExporterService exporter) => {
    await exporter.ExportarDeRedisAOracleAsync();
    return Results.Ok("Proceso de exportación iniciado. Revisa la consola para ver el progreso.");
});

app.MapGet("/count", async (IConnectionMultiplexer c) => {
    IDatabase _db = c.GetDatabase();
    int elements = (await _db.ListRangeAsync("cola:examen", 0)).Count();
    return Results.Ok($"Hay en total {elements} elementos");
});

app.Run();

// --- 4. CLASES DE LÓGICA ---

public class ExamenHub(ChannelWriter<AccionEvento> rw) : Hub {
    public async Task RegistrarAccion(AccionEvento e) => await rw.WriteAsync(e);
}

// Serialización Optimizada (Source Generator)
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AccionEvento))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

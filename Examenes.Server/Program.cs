using System.Text.Json.Serialization;
using System.Threading.Channels;
using Examenes.Domain;
using Examenes.Server.BackgroundServices;
using Examenes.Server.Exporters;
using Examenes.Server.Monitoring;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE ASPIRE Y SERVICIOS ---
builder.AddServiceDefaults();
builder.AddRedisClient("redis");

builder.Services.AddSignalR();

// Canal para procesar la llegada y meter a Redis rápidamente
var eventoChannel = Channel.CreateBounded<AccionEvento>(new BoundedChannelOptions(100000) {
    FullMode = BoundedChannelFullMode.Wait
});
builder.Services.AddSingleton(eventoChannel.Writer);
builder.Services.AddSingleton(eventoChannel.Reader);

builder.Services.AddSingleton<MonitorControl>();

// Worker que mueve datos del Canal a Redis
builder.Services.AddHostedService<RedisIngestionWorker>();
builder.Services.AddHostedService<ChannelMonitorWorker>();

// Worker que maneja Oracle
builder.Services.AddSingleton<OracleExporterService>();

var app = builder.Build();

// --- 2. INICIALIZACIÓN ---
app.MapDefaultEndpoints();

// --- 3. ENDPOINTS ---
app.MapHub<ExamenHub>("/examenHub");

// Endpoint de exportación con porcentaje
app.MapGet("/api/finalizarexamen", async (OracleExporterService exporter, MonitorControl c) => {
    c.Activo = false;
    await exporter.ExportarDeRedisAOracleAsync();
    return Results.Ok("Proceso de exportación iniciado. Revisa la consola para ver el progreso.");
});

app.Run();

// --- 4. CLASES DE LÓGICA ---

public class ExamenHub(ChannelWriter<AccionEvento> writer) : Hub {
    public async Task RegistrarAccion(AccionEvento e) => await writer.WriteAsync(e);
}

// Serialización Optimizada (Source Generator)
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(AccionEvento))]
internal partial class SourceGenerationContext : JsonSerializerContext { }

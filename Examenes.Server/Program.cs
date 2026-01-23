using System.Threading.Channels;
using Examenes.Domain;
using Examenes.Server.BackgroundServices;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE ASPIRE Y SERVICIOS ---
builder.AddServiceDefaults();
builder.AddRedisClient("redis", cfg => cfg.DisableTracing = true);

builder.Services.AddSignalR();

// Canales de trabajo
builder.Services.AddSingleton(ChannelManager.channelSIGANLR.Writer);
builder.Services.AddSingleton(ChannelManager.channelSIGANLR.Reader);

// Worker que mueve datos del Canal a Redis
builder.Services.AddHostedService<RedisIngestionWorker>();
builder.Services.AddHostedService<ChannelMonitor>();

var app = builder.Build();

// --- 2. INICIALIZACIÓN ---
app.MapDefaultEndpoints();

// --- 3. ENDPOINTS ---
app.MapHub<ExamenHub>("/examenHub");

app.Run();

// --- 4. CLASES DE LÓGICA ---

public class ExamenHub(ChannelWriter<AccionEvento> rw) : Hub {
    public async Task RegistrarAccion(AccionEvento e) => await rw.WriteAsync(e);
}

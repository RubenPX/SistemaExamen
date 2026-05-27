using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Migrator.RedisToSurrealDB.Worker;
using SurrealDb.Net;
using SurrealDb.Net.Models.Auth;

var builder = Host.CreateApplicationBuilder(args);

// Integración de Aspire
builder.AddRedisClient("redis", settings => {
    settings.DisableTracing = true;
});

builder.Services.AddSingleton<SurrealDBInstancer>();

builder.Services.AddHostedService<RedisToSurrealWorker>();

var host = builder.Build();
host.Run();


public class SurrealDBInstancer() {
    public async Task<SurrealDbClient> NewConnection() {
        var DB_URI = Environment.GetEnvironmentVariable("DB_URI");
        var DB_USERNAME = Environment.GetEnvironmentVariable("DB_USERNAME");
        var DB_PASSWORD = Environment.GetEnvironmentVariable("DB_PASSWORD");
        var DB_DATABASE = Environment.GetEnvironmentVariable("DB_DATABASE");
        var DB_NAMESPACE = Environment.GetEnvironmentVariable("DB_NAMESPACE");

        var db = new SurrealDbClient(DB_URI);
        await db.Connect();
        await db.Use(DB_NAMESPACE, DB_DATABASE);
        await db.SignIn(new RootAuth {
            Username = DB_USERNAME,
            Password = DB_PASSWORD
        });

        return db;
    }
}

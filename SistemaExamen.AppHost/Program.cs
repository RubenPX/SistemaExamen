var builder = DistributedApplication.CreateBuilder(args);

// Base de datos final
var oracle = builder.AddOracle("oracle")
                    .WithDataVolume()
                    .WithLifetime(ContainerLifetime.Persistent)
                    .WithExplicitStart();

// Cache Redis
var redis = builder.AddRedis("redis")
                .WithDataVolume()
                .WithPersistence(TimeSpan.FromSeconds(1)); // Activa RDB/AOF

builder.AddContainer("redis-insight", "redislabs/redisinsight")
       .WithHttpEndpoint(port: 5540, targetPort: 5540)
       .WithReference(redis)
       .WithLifetime(ContainerLifetime.Persistent);

// Servidor
var server = builder.AddProject<Projects.Examenes_Server>("server")
    .WithReference(oracle)
    .WaitFor(redis).WithReference(redis)
    .WithHttpCommand("/api/finalizarexamen", "Migrar datos a oracle", commandOptions: new() {
        Method = HttpMethod.Get,
        ConfirmationMessage = "Quieres que se inicie la exportación?",
    });

// Simulador
builder.AddProject<Projects.Examenes_Simulator>("simulator")
    .WithEnvironment("MAX_CONNECTIONS", "500")
    .WithEnvironment("MAX_EVENTS", "1_000_000")
    .WithReference(server)
    .WithExplicitStart();

builder.Build().Run();

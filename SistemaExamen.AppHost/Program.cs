var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis")
                .WithDataVolume()
                .WithPersistence(TimeSpan.FromSeconds(1)); // Activa RDB/AOF

builder.AddContainer("redis-insight", "redislabs/redisinsight")
       .WithHttpEndpoint(port: 5540, targetPort: 5540)
       .WithReference(redis)
       .WithLifetime(ContainerLifetime.Persistent);

var oracle = builder.AddOracle("oracle")
                    .WithDataVolume()
                    .WithLifetime(ContainerLifetime.Persistent);

var server = builder.AddProject<Projects.Examenes_Server>("server")
    .WaitFor(redis).WithReference(redis)
    .WaitFor(oracle).WithReference(oracle)
    .WithHttpCommand("/api/finalizarexamen", "Migrar datos a oracle", commandOptions: new() {
        Method = HttpMethod.Get,
        ConfirmationMessage = "Quieres que se inicie la exportación?",
    });

builder.AddProject<Projects.Examenes_Simulator>("simulator")
    .WithReference(server)
    .WithExplicitStart();

builder.Build().Run();

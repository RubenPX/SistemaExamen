var builder = DistributedApplication.CreateBuilder(args);

// Base de datos final
//var oracle = builder.AddOracle("oracle")
//                    .WithDataVolume()
//                    .WithLifetime(ContainerLifetime.Persistent)
//                    .WithExplicitStart()
//                    .AddDatabase("FREEPDB1");

// Cache Redis
var redis = builder.AddRedis("redis")
                .WithDataVolume()
                .WithPersistence(TimeSpan.FromSeconds(1))  // Activa RDB/AOF
                .WithLifetime(ContainerLifetime.Persistent);

var dbuser = builder.AddParameter("dbuser", "admin");
var dbPass = builder.AddParameter("dbpass", "admin");

var surreal = builder.AddSurrealServer("surreal", userName: dbuser, password: dbPass).WithSurrealist();
var db = surreal.AddNamespace("ns").AddDatabase("db");

//builder.AddContainer("redis-insight", "redislabs/redisinsight")
//       .WithHttpEndpoint(port: 5540, targetPort: 5540)
//       .WithReference(redis)
//       .WithVolume("/data")
//       .WithLifetime(ContainerLifetime.Persistent);

// Servidor
var server = builder.AddProject<Projects.Examenes_Server>("server")
    .WaitFor(redis).WithReference(redis)
    .WithHttpCommand("/api/finalizarexamen", "Migrar datos a oracle", commandOptions: new() {
        Method = HttpMethod.Get,
        ConfirmationMessage = "Quieres que se inicie la exportación?",
    });

// Simulador
builder.AddProject<Projects.Examenes_Simulator>("simulator")
    .WithEnvironment("MAX_CONNECTIONS", "2000")
    .WithEnvironment("MAX_EVENTS", "10_000_000")
    .WithEnvironment("AGRESIVIDAD", "20")
    .WithReference(server).WaitFor(server)
    .WithExplicitStart();

builder.AddProject<Projects.Migrator_RedisToSurrealDB>("migrator-redissurreal")
    .WaitFor(redis).WithReference(redis)
    .WaitFor(db).WithReference(db)
    .WithExplicitStart();

//builder.AddProject<Projects.Migrator_RedisToOracle>("migrator-redistooracle")
//    .WaitFor(redis).WithReference(redis)
//    .WaitFor(oracle).WithReference(oracle)
//    .WithExplicitStart();

builder.Build().Run();

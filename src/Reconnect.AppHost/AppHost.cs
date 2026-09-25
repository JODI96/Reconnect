var builder = DistributedApplication.CreateBuilder(args);

// Generated once and persisted in this project's user secrets (Parameters:jwt-signing-key).
var jwtSigningKey = builder.AddParameter(
    "jwt-signing-key",
    new GenerateParameterDefault { MinLength = 64, Special = false },
    secret: true,
    persist: true);

var postgres = builder.AddPostgres("postgres")
    .WithImage("postgis/postgis", "17-3.5")
    .WithDataVolume()
    .WithPgAdmin();
var database = postgres.AddDatabase("reconnectdb");

var redis = builder.AddRedis("redis");

var storage = builder.AddAzureStorage("storage").RunAsEmulator(azurite => azurite.WithDataVolume());
var blobs = storage.AddBlobs("blobs");

builder.AddProject<Projects.Reconnect_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(redis).WaitFor(redis)
    .WithReference(blobs).WaitFor(blobs)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithExternalHttpEndpoints();

builder.Build().Run();

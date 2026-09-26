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

var api = builder.AddProject<Projects.Reconnect_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(redis).WaitFor(redis)
    .WithReference(blobs).WaitFor(blobs)
    .WithEnvironment("Jwt__SigningKey", jwtSigningKey)
    .WithExternalHttpEndpoints();

// Optional: Google Photorealistic 3D Tiles (without a key everyone gets swisstopo). Set once:
// dotnet user-secrets set "Parameters:google-maps-api-key" "<key>" --project src/Reconnect.AppHost
if (!string.IsNullOrWhiteSpace(builder.Configuration["Parameters:google-maps-api-key"]))
{
    var googleMapsKey = builder.AddParameter("google-maps-api-key", secret: true);
    api.WithEnvironment("Maps__Google__ApiKey", googleMapsKey);
}

builder.Build().Run();

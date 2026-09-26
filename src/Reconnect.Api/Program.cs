using System.Text.Json.Serialization;
using Reconnect.Api;
using Reconnect.Api.OpenApi;
using Reconnect.Contracts;
using Reconnect.Modules.City;
using Reconnect.Modules.Identity;
using Reconnect.Modules.Profiles;
using Reconnect.Modules.RealEstate;
using Reconnect.Modules.Rooms;
using Reconnect.Modules.Safety;
using Reconnect.Modules.Social;
using Reconnect.Modules.Wallet;
using Reconnect.SharedKernel.Modules;
using Reconnect.SharedKernel.Web;
using Scalar.AspNetCore;

// The API host only composes: shared infrastructure + modules. Business logic lives in src/Modules.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisClient(ResourceNames.Redis);            // room presence + minigame state
builder.AddAzureBlobServiceClient(ResourceNames.Blobs);  // avatars / room images (later)

// Order matters only for seeding (e.g. the dev admin must exist before the showcase rooms).
builder.AddModules(
    new IdentityModule(),
    new ProfilesModule(),
    new SafetyModule(),
    new SocialModule(),
    new CityModule(),
    new WalletModule(),
    new RoomsModule(),
    new RealEstateModule());

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.AddReconnectRateLimiting();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapGroup(ApiRoutes.Version).MapModules();

await app.InitializeModulesAsync();
await app.RunAsync();

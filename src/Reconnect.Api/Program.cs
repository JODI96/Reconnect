using System.Text.Json.Serialization;
using Reconnect.Api.Common;
using Reconnect.Api.Common.Auth;
using Reconnect.Api.Common.Endpoints;
using Reconnect.Api.Common.Errors;
using Reconnect.Api.Common.OpenApi;
using Reconnect.Api.Features.Auth;
using Reconnect.Api.Features.Showcase;
using Reconnect.Api.Hubs;
using Reconnect.Contracts.Hubs;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddReconnectData();
builder.AddReconnectAuth();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapEndpointModules();
app.MapHub<ChatHub>(ChatHubContract.Path);
app.MapHub<RoomHub>(RoomHubContract.Path);

await app.MigrateDatabaseIfEnabledAsync();
await app.SeedDevAdminIfEnabledAsync();
await app.SeedShowcaseRoomsIfEnabledAsync();
await app.RunAsync();

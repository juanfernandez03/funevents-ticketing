using FunEvents.Api.Data;
using FunEvents.Api.Endpoints;
using FunEvents.Api.UseCases;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddData();
builder.Services.AddUseCases();

// DataAnnotations on the contracts are validated before any handler runs (.NET 10 Minimal APIs).
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Malformed JSON is the client's fault: answer 400 in every environment instead of throwing.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync();
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();
app.MapEventEndpoints();
app.MapReservationEndpoints();

await app.RunAsync();

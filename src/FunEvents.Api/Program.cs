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

// A body that cannot be bound throws in every environment and becomes the same 400 shape as a validation error.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();

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

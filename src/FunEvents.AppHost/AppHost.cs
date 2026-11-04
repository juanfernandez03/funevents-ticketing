var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL runs in a container and is recreated on every run, so each demo starts from the seed data.
var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("funevents");

builder.AddProject<Projects.FunEvents_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

builder.Build().Run();

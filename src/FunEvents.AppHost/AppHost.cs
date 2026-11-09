var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL runs in a container and is recreated on every run, so each demo starts from the seed data.
var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("funevents");

var api = builder.AddProject<Projects.FunEvents_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

// Console client scenarios. They do not start with the app: press "Start" on them in the dashboard.
builder.AddProject<Projects.FunEvents_ConsoleClient>("client-reserve")
    .WithReference(api)
    .WaitFor(api)
    .WithArgs("--event", "ROCK-BCN-2027", "--user", "USR-001", "--quantity", "2")
    .WithExplicitStart();

// 50 buyers race for the 10 tickets of JAZZ-VLC-2027: exactly 10 must succeed.
builder.AddProject<Projects.FunEvents_ConsoleClient>("client-rush")
    .WithReference(api)
    .WaitFor(api)
    .WithArgs("--event", "JAZZ-VLC-2027", "--user", "USR-002", "--parallel", "50")
    .WithExplicitStart();

builder.Build().Run();

var builder = DistributedApplication.CreateBuilder(args);

// 1. Infrastructure
var sql = builder.AddSqlServer("sql")
    .AddDatabase("minutedb");

var keycloak = builder.AddKeycloak("keycloak", 8080);

// 2. Backend (Wait for SQL to be ready)
var dbManager = builder.AddProject<Projects.UTB_Minute_DbManager>("dbmanager")
    .WithReference(sql)
    .WaitFor(sql); // Подождать базу

var webApi = builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
    .WithReference(sql)
    .WithReference(keycloak)
    .WaitFor(sql); // Подождать базу

// 3. Frontend
builder.AddProject<Projects.UTB_Minute_AdminClient>("adminclient")
    .WithReference(webApi)
    .WaitFor(webApi);

builder.AddProject<Projects.UTB_Minute_CanteenClient>("canteenclient")
    .WithReference(webApi)
    .WithReference(keycloak)
    .WaitFor(webApi);

builder.Build().Run();
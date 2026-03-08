var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithDataVolume("sql")
    .AddDatabase("minutedb");

// Keycloak
var username = builder.AddParameter("username", secret: true, value: "admin");
var password = builder.AddParameter("password", secret: true, value: "admin");

var keycloak = builder.AddKeycloak("keycloak", 8080, username, password)
    .WithDataVolume("keycloak")
    .WithRealmImport("./Realms");

// Backend
var dbManager = builder.AddProject<Projects.UTB_Minute_DbManager>("dbmanager")
    .WithReference(sql)
    .WaitFor(sql);

var webApi = builder.AddProject<Projects.UTB_Minute_WebApi>("webapi")
    .WithReference(sql)
    .WithReference(keycloak)
    .WaitFor(sql)
    .WaitFor(keycloak);

// Frontend
builder.AddProject<Projects.UTB_Minute_AdminClient>("adminclient")
    .WithExternalHttpEndpoints()
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithReference(keycloak)
    .WaitFor(keycloak);

builder.AddProject<Projects.UTB_Minute_CanteenClient>("canteenclient")
    .WithExternalHttpEndpoints()
    .WithReference(webApi)
    .WaitFor(webApi)
    .WithReference(keycloak)
    .WaitFor(keycloak);

builder.Build().Run();
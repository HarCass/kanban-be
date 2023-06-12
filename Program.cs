using Api.Services;
using Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDBClient(Environment.GetEnvironmentVariable("DB_URI"), Environment.GetEnvironmentVariable("DB_NAME"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.MapBoardsEndpoints();
app.MapUsersEndpoints();

app.UseSwagger();
app.UseSwaggerUI(config => 
{
    config.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    config.RoutePrefix = string.Empty;
});

app.Run();

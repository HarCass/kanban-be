using Api.Services;
using Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("localsettings.json", false, true);
builder.Services.AddDBClient(builder.Configuration["DB:URI"], builder.Configuration["DB:Name"]);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.UseHttpsRedirection();

app.MapBoardsEndpoints();
app.MapUsersEndpoints();

app.UseSwagger();
app.UseSwaggerUI(config => 
{
    config.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    config.RoutePrefix = string.Empty;
});

app.Run();

public partial class Program { }

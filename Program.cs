using MongoDB.Driver;
using Api.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MongoClient>(_ => new MongoClient(Environment.GetEnvironmentVariable("DB_URI")));
builder.Services.AddSingleton<IMongoDatabase>(provider => provider.GetRequiredService<MongoClient>().GetDatabase(Environment.GetEnvironmentVariable("DB_NAME")));
builder.Services.AddSingleton<IMongoCollection<Board>>(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<Board>("boards"));
builder.Services.AddSingleton<IMongoCollection<User>>(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<User>("users"));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var app = builder.Build();

app.MapGet("/boards/{id}", async (IMongoCollection<Board> boards, IMongoCollection<User> users, string id) =>
{
    var user = await users.Find($"{{username: '{id}'}}").ToListAsync();
    if (user.Count == 0) return Results.NotFound(new {msg="User Not Found"});
    return TypedResults.Ok(new {boards=await boards.Find($"{{creator: '{id}'}}").ToListAsync()});
});

app.MapPost("/boards", async (IMongoCollection<Board> coll, Board newBoard) =>
{
    newBoard.Id = "";
    await coll.InsertOneAsync(newBoard);
    var res = await coll.Find($"{{name: '{newBoard.Name}', creator: '{newBoard.Creator}'}}").ToListAsync();
    return TypedResults.Created("/boards", new {board=res[0]});
});

app.MapGet("/users/{id}", async (IMongoCollection<User> coll, string id) =>
{
    id = id.ToLower();
    var res = await coll.Find($"{{username: '{id}'}}").ToListAsync();
    return res.Count == 0 ? Results.NotFound(new {msg="User Not Found"}) : TypedResults.Ok(new {user=res[0]});
});

app.MapPost("/users", async (IMongoCollection<User> coll, User newUser) =>
{
    newUser.Id = "";
    newUser.Username = newUser.Username.ToLower();
    try
    {
        await coll.InsertOneAsync(newUser);
    }
    catch (MongoWriteException ex)
    {
        if (ex.WriteError.Code == 121)
        {
            return Results.BadRequest(new {msg="Username Required"});
        }
        else if (ex.WriteError.Code == 11000)
        {
            return Results.BadRequest(new {msg="Username In Use"});
        }
        else throw ex;
    }
    var res = await coll.Find($"{{username: '{newUser.Username}'}}").ToListAsync();
    return TypedResults.Created("/users", new {user=res[0]});
});

app.UseSwagger();
app.UseSwaggerUI(config => 
{
    config.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    config.RoutePrefix = string.Empty;
});

app.Run();

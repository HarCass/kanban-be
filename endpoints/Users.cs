using MongoDB.Driver;
using Api.Models;

namespace Api.Endpoints;

public static class ApiEndpoints
{
    public static WebApplication MapUsersEndpoints(this WebApplication app)
    {
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
        return app;
    }

    public static WebApplication MapBoardsEndpoints(this WebApplication app)
    {
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
        return app;
    }
}
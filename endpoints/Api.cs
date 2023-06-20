using MongoDB.Driver;
using MongoDB.Bson;
using Api.Models;

namespace Api.Endpoints;

public static class ApiEndpoints
{
    public static WebApplication MapUsersEndpoints(this WebApplication app)
    {
        app.MapGet("/users/{username}", GetUser);

        app.MapPost("/users", PostUser);

        app.MapGet("/users/{username}/boards", GetUserBoards);

        app.MapGet("/users/{username}/boards/{name}", GetUserBoard);

        app.MapPost("users/{username}/boards/{name}/sections", PostSection);

        app.MapPost("users/{username}/boards/{name}/sections/{title}", PostTicket);

        return app;
    }

    public static WebApplication MapBoardsEndpoints(this WebApplication app)
    {
        app.MapGet("/boards/{id}", GetBoard);

        app.MapDelete("/boards/{id}", DeleteBoard);

        app.MapPost("/boards", PostBoard);

        return app;
    }

    private static async Task<IResult> GetUser(IMongoCollection<User> coll, string username)
    {
        username = username.ToLower();
        var res = await coll.Find($"{{username: '{username}'}}").ToListAsync();
        return res.Count == 0 ? Results.NotFound(new {msg="User Not Found"}) : TypedResults.Ok(new {user=res[0]});
    }
    private static async Task<IResult> GetUserBoards(IMongoCollection<Board> boards, IMongoCollection<User> users, string username)
    {
        var user = await users.Find($"{{username: '{username}'}}").ToListAsync();
        if (user.Count == 0) return Results.NotFound(new {msg="User Not Found"});
        return TypedResults.Ok(new {boards=await boards.Find($"{{creator: '{username}'}}").ToListAsync()});
    }
    private static async Task<IResult> PostUser(IMongoCollection<User> coll, User newUser)
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
    }
    private static async Task<IResult> GetUserBoard(IMongoCollection<Board> boards, IMongoCollection<User> users, string username, string name)
    {
        var user = await users.Find($"{{username: '{username}'}}").ToListAsync();
        if (user.Count == 0) return Results.NotFound(new {msg="User Not Found"});
        var res =await  boards.Find($"{{creator: '{username}', name: '{name}'}}").ToListAsync();
        if (res.Count == 0) return Results.NotFound(new {msg="Board Not Found"});
        return TypedResults.Ok(new {board=res[0]});
    }
    private static async Task<IResult> PostSection(IMongoCollection<Board> coll, string username, string name, string title )
    {
        Section newSection = new()
        {
            Title = title
        };
        var update = Builders<Board>.Update.Push(board => board.Sections, newSection);
        var res = await coll.FindOneAndUpdateAsync($"{{name: '{name}', creator: '{username}'}}", update);
        return TypedResults.Ok(new {sections=res.Sections});
    }
    private static async Task<IResult> PostTicket(IMongoCollection<Board> coll, string username, string name, string title, Ticket ticket)
    {
        var builder = Builders<Board>.Filter;
        var filter = builder.Eq(board => board.Creator, username) & builder.Eq(board => board.Name, name);
        var options = new FindOneAndUpdateOptions<Board>
        {
            ArrayFilters = new List<ArrayFilterDefinition> 
                {
                    new BsonDocumentArrayFilterDefinition<BsonDocument> (new BsonDocument($"section.title", title)),
                },
            ReturnDocument = ReturnDocument.After
        };
        var update = Builders<Board>.Update.Push("sections.$[section].tickets", ticket);
        var res = await coll.FindOneAndUpdateAsync(filter, update, options);
        return TypedResults.Ok(new {tickets=res.Sections.Where(t => t.Title == title)});
    }
    private static async Task<IResult> GetBoard(IMongoCollection<Board> coll, string id)
    {
        try 
        {
            ObjectId.Parse(id);
        }
        catch
        {
            return Results.BadRequest(new {msg="Invalid ID"});
        }
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var res = await coll.Find(filter).ToListAsync();
        if (res.Count == 0) return Results.NotFound(new {msg="Board Not Found"});
        return TypedResults.Ok(new {Board=res[0]});
    }
    private static async Task<IResult> DeleteBoard(IMongoCollection<Board> coll, string id)
    {
        try 
        {
            ObjectId.Parse(id);
        }
        catch
        {
            return Results.BadRequest(new {msg="Invalid ID"});
        }
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var res = await coll.FindOneAndDeleteAsync(filter);
        return res == null ? Results.NoContent() : Results.NotFound(new {msg="Board Not Found"});
    }
    private static async Task<IResult> PostBoard(IMongoCollection<Board> coll, Board newBoard)
    {
        newBoard.Id = "";
        await coll.InsertOneAsync(newBoard);
        var res = await coll.Find($"{{name: '{newBoard.Name}', creator: '{newBoard.Creator}'}}").ToListAsync();
        return TypedResults.Created("/boards", new {board=res[0]});
    }
}
using MongoDB.Driver;
using MongoDB.Bson;
using Api.Models;

namespace Api.Endpoints;

public static class ApiEndpoints
{
    public static WebApplication MapUsersEndpoints(this WebApplication app)
    {
        app.MapPost("/users", PostUser);

        app.MapGet("/users/{username}", GetUser);

        app.MapGet("/users/{username}/boards", GetUserBoards);

        return app;
    }

    public static WebApplication MapBoardsEndpoints(this WebApplication app)
    {
        app.MapPost("/boards", PostBoard);

        app.MapGet("/boards/{id}", GetBoard);

        app.MapDelete("/boards/{id}", DeleteBoard);

        app.MapPut("/boards/{id}", PutBoard);

        app.MapPost("/boards/{id}/sections", PostSection);

        app.MapDelete("/boards/{id}/sections/{title}", DeleteSection);

        app.MapPost("/boards/{id}/sections/{title}", PostTicket);

        app.MapDelete("/boards/{id}/sections/{title}/{ticketId}", DeleteTicket);

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
    private static async Task<IResult> GetBoard(IMongoCollection<Board> coll, string id)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var res = await coll.Find(filter).ToListAsync();
        if (res.Count == 0) return Results.NotFound(new {msg="Board Not Found"});
        return TypedResults.Ok(new {Board=res[0]});
    }
    private static async Task<IResult> DeleteBoard(IMongoCollection<Board> coll, string id)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var res = await coll.FindOneAndDeleteAsync(filter);
        return res == null ? Results.NotFound(new {msg="Board Not Found"}) : Results.NoContent();
    }
    private static async Task<IResult> PostBoard(IMongoCollection<Board> coll, Board newBoard)
    {
        newBoard.Id = "";
        await coll.InsertOneAsync(newBoard);
        var res = await coll.Find($"{{name: '{newBoard.Name}', creator: '{newBoard.Creator}'}}").ToListAsync();
        return TypedResults.Created("/boards", new {board=res[0]});
    }
    private static async Task<IResult> PostSection(IMongoCollection<Board> coll, string id, string title)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});

        Section newSection = new()
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Title = title
        };

        var options = new FindOneAndUpdateOptions<Board>
        {
            ReturnDocument = ReturnDocument.After
        };

        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var update = Builders<Board>.Update.Push(board => board.Sections, newSection);
        var res = await coll.FindOneAndUpdateAsync(filter, update, options);
        return TypedResults.Ok(new {sections=res.Sections});
    }
    private static async Task<IResult> DeleteSection(IMongoCollection<Board> coll, string id, string title)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});

        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var update = Builders<Board>.Update.PullFilter(board => board.Sections, section => section.Title == title);
        var res = await coll.UpdateOneAsync(filter, update);
        return res.ModifiedCount == 0 ? Results.NotFound(new {msg="Section Not Found"}) : Results.NoContent();
    }
    private static async Task<IResult> PostTicket(IMongoCollection<Board> coll, string id, string title, Ticket newTicket)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        newTicket.Id = ObjectId.GenerateNewId().ToString();
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var options = new FindOneAndUpdateOptions<Board>
        {
            ArrayFilters = new List<ArrayFilterDefinition>
                {
                    new BsonDocumentArrayFilterDefinition<BsonDocument> (new BsonDocument($"section.title", title)),
                },
            ReturnDocument = ReturnDocument.After
        };
        var update = Builders<Board>.Update.Push("sections.$[section].tickets", newTicket);
        var res = await coll.FindOneAndUpdateAsync(filter, update, options);
        return TypedResults.Ok(new {tickets=res.Sections.Where(t => t.Title == title)});
    }
    private static async Task<IResult> DeleteTicket(IMongoCollection<Board> coll, string id, string title, string ticketId)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        if (!IsValidId(ticketId)) return Results.BadRequest(new {msg="Invalid Ticket ID"});
        var builder = Builders<Board>.Filter;
        var filter = builder.Eq(board => board.Id, id);
        var options = new UpdateOptions
        {
            ArrayFilters = new List<ArrayFilterDefinition>
                {
                    new BsonDocumentArrayFilterDefinition<BsonDocument> (new BsonDocument($"section.title", title))
                }
        };
        var update = Builders<Board>.Update.PullFilter("sections.$[section].tickets", Builders<Ticket>.Filter.Eq("Id", ticketId));
        var res = await coll.UpdateOneAsync(filter, update, options);
        return res.ModifiedCount == 0 ? Results.NotFound(new {msg="Ticket Not Found"}) : Results.NoContent();
    }
    private static async Task<IResult> PutBoard(IMongoCollection<Board> coll, string id, Board updatedBoard)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        updatedBoard.Id = id;
        var builder = Builders<Board>.Filter;
        var filter = builder.Eq(board => board.Id, id);
        var options = new FindOneAndReplaceOptions<Board>
        {
            ReturnDocument = ReturnDocument.After
        };
        var res = await coll.FindOneAndReplaceAsync(filter, updatedBoard, options);
        return res == null ? Results.NotFound(new {msg="Board Not Found"}) : TypedResults.Ok(new {board=res});
    }

    private static Boolean IsValidId(string id)
    {
        try
        {
            ObjectId.Parse(id);
        }
        catch
        {
            return false;
        }
        return true;
    }
}
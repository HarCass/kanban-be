using MongoDB.Driver;
using MongoDB.Bson;
using Api.Models;

namespace Api.Endpoints;

public static class ApiEndpoints
{
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        app.MapPost("/users", PostUser);

        app.MapGet("/users/{username}", GetUser);

        app.MapGet("/users/{username}/boards", GetUserBoards);

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
        return res.Count == 0 ? Results.NotFound(new {msg="User Not Found"}) : TypedResults.Ok(res[0]);
    }
    private static async Task<IResult> GetUserBoards(IMongoCollection<Board> boards, IMongoCollection<User> users, string username)
    {
        var user = await users.Find($"{{username: '{username}'}}").ToListAsync();
        if (user.Count == 0) return Results.NotFound(new {msg="User Not Found"});
        var res = await boards.Find($"{{creator: '{username}'}}").ToListAsync();
        return TypedResults.Ok(res);
    }
    private static async Task<IResult> PostUser(IMongoCollection<User> coll, NewUser newUser)
    {
        if (string.IsNullOrWhiteSpace(newUser.Username)) return Results.BadRequest(new {msg="Invalid Username"});
        var user = new User {
            Username = newUser.Username
        };
        try
        {
            await coll.InsertOneAsync(user);
        }
        catch (MongoWriteException ex)
        {
            return ex.WriteError.Code switch {
                121 => Results.BadRequest(new {msg="Username Required"}),
                11000 => Results.BadRequest(new {msg="Username In Use"}),
                _ => Results.Problem("Issue Creating User", null, 500, "Internal Server Error")
            };
        }
        var res = await coll.Find($"{{username: '{newUser.Username}'}}").ToListAsync();
        return TypedResults.Created("/users", res[0]);
    }
    private static async Task<IResult> GetBoard(IMongoCollection<Board> coll, string id)
    {
        if (!IsValidId(id)) return Results.BadRequest(new {msg="Invalid Board ID"});
        var filter = Builders<Board>.Filter.Eq(board => board.Id, id);
        var res = await coll.Find(filter).ToListAsync();
        if (res.Count == 0) return Results.NotFound(new {msg="Board Not Found"});
        return TypedResults.Ok(res[0]);
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
        newBoard.Sections[0].Id = ObjectId.GenerateNewId().ToString();
        newBoard.Sections[0].Tickets[0].Id = ObjectId.GenerateNewId().ToString();
        newBoard.Completed[0].Id = ObjectId.GenerateNewId().ToString();
        try
        {
            await coll.InsertOneAsync(newBoard);
        }
        catch (MongoWriteException ex)
        {
            Console.WriteLine(ex.WriteError.Message);
            return ex.WriteError.Code switch
            {
                11000 => Results.BadRequest(new {msg="Board Name Already Exists"}),
                _ => Results.Problem("Issue Creating Board", null, 500, "Internal Server Error")
            };
        }
        var res = await coll.Find($"{{name: '{newBoard.Name}', creator: '{newBoard.Creator}'}}").ToListAsync();
        return TypedResults.Created("/boards", res[0]);
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
        try
        {
            var res = await coll.FindOneAndUpdateAsync(filter, update, options);
            return TypedResults.Ok(res.Sections);
        }
        catch (MongoWriteException ex)
        {
            return ex.WriteError.Code switch
            {
                _ => Results.Problem("Issue Creating Board", null, 500, "Internal Server Error")
            };
        }
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
        try
        {
            var res = await coll.FindOneAndUpdateAsync(filter, update, options);
            return TypedResults.Ok(res.Sections.Where(t => t.Title == title));
        }
        catch (MongoWriteException ex)
        {
            return ex.WriteError.Code switch
            {
                _ => Results.Problem("Issue Creating Board", null, 500, "Internal Server Error")
            };
        }
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
        return res == null ? Results.NotFound(new {msg="Board Not Found"}) : TypedResults.Ok(res);
    }

    private static bool IsValidId(string id)
    {
        return ObjectId.TryParse(id, out _);
    }
}
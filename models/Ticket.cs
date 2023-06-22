using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

public class Ticket
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; } = string.Empty;
    
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("priority")]
    public string Priority { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("done")]
    public bool Done { get; set; } = false;
}
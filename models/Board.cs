using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Api.Models;

[BsonDiscriminator]
public class Board
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("creator")]
    public string Creator { get; set; } = string.Empty;

    [BsonElement("sections")]
    public Section[] Sections {get; set; } = Array.Empty<Section>();

    [BsonElement("completed")]
    public Ticket[] Completed {get; set; } = Array.Empty<Ticket>();
}

public class Section
{
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("tickets")]
    public Ticket[] Tickets { get; set; } = Array.Empty<Ticket>();
}
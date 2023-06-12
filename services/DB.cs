using MongoDB.Driver;
using Api.Models;

namespace Api.Services;

public static class DBService
{
    public static IServiceCollection AddDBClient(this IServiceCollection services, string? uri, string? db)
    {
        services.AddSingleton<MongoClient>(_ => new MongoClient(uri));
        services.AddSingleton<IMongoDatabase>(provider => provider.GetRequiredService<MongoClient>().GetDatabase(db));
        services.AddSingleton<IMongoCollection<Board>>(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<Board>("boards"));
        services.AddSingleton<IMongoCollection<User>>(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<User>("users"));
        return services;
    }
}
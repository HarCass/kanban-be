using MongoDB.Driver;
using Api.Models;

namespace Api.Services;

public static class DBService
{
    public static IServiceCollection AddDBClient(this IServiceCollection services, string? uri, string? db)
    {
        services.AddSingleton(_ => new MongoClient(uri));
        services.AddSingleton(provider => provider.GetRequiredService<MongoClient>().GetDatabase(db));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<Board>("boards"));
        services.AddSingleton(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<User>("users"));
        return services;
    }
}
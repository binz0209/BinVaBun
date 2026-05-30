using MongoDB.Driver;
using MemoriesApp.Api.Models;

namespace MemoriesApp.Api.Services;

public class MongoDbService
{
    private readonly IMongoDatabase _database;

    public MongoDbService(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDb:ConnectionString"];
        var dbName = configuration["MongoDb:DbName"];
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(dbName);
    }

    public IMongoCollection<User> Users => _database.GetCollection<User>("users");
    public IMongoCollection<MemoryPost> Posts => _database.GetCollection<MemoryPost>("posts");
    public IMongoCollection<Notification> Notifications => _database.GetCollection<Notification>("notifications");
    public IMongoCollection<MarkedDate> MarkedDates => _database.GetCollection<MarkedDate>("markedDates");
}


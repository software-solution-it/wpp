using MongoDB.Driver;
using tests_.src.Domain.Entities;
using WhatsAppProject.Entities;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    // Construtor que aceita IMongoClient e o nome do banco de dados
    public MongoDbContext(IMongoClient mongoClient, string databaseName)
    {
        _database = mongoClient.GetDatabase(databaseName);
    }

    public IMongoCollection<FlowWhatsapp> Flows => _database.GetCollection<FlowWhatsapp>("flowsWhatsapp");

}

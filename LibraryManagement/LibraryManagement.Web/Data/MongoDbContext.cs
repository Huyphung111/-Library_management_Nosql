using Microsoft.Extensions.Options;
using MongoDB.Driver;
using LibraryManagement.Web.Models;

namespace LibraryManagement.Web.Data;

/// <summary>Giữ IMongoDatabase và expose các collection. Chỉ Service được dùng lớp này.</summary>
public class MongoDbContext
{
    public MongoDbContext(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        Client = client;
        Database = client.GetDatabase(settings.Value.DatabaseName);

        Users = Database.GetCollection<User>("users");
        Readers = Database.GetCollection<Reader>("readers");
        Categories = Database.GetCollection<Category>("categories");
        Books = Database.GetCollection<Book>("books");
        Loans = Database.GetCollection<Loan>("loans");
        Reservations = Database.GetCollection<Reservation>("reservations");
        Counters = Database.GetCollection<Counter>("counters");
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<User> Users { get; }
    public IMongoCollection<Reader> Readers { get; }
    public IMongoCollection<Category> Categories { get; }
    public IMongoCollection<Book> Books { get; }
    public IMongoCollection<Loan> Loans { get; }
    public IMongoCollection<Reservation> Reservations { get; }
    public IMongoCollection<Counter> Counters { get; }
}

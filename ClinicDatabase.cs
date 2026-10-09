using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Hospital;

public class ClinicDatabase
{
    public const string DatabaseName = "KineziterapijosKlinika";

    private readonly IMongoDatabase _database;

    public IMongoCollection<Pacient> Pacients { get; }
    public IMongoCollection<Kineziterapeutas> Kineziterapeutai { get; }
    public IMongoCollection<Visit> Visits { get; }

    static ClinicDatabase()
    {
        // C# savybės (PacientName) saugomos camelCase laukais (pacientName), kaip aprašyta db/init.js.
        var conventions = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
        };
        ConventionRegistry.Register("Clinic conventions", conventions, _ => true);
    }

    public ClinicDatabase(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        _database = new MongoClient(settings).GetDatabase(DatabaseName);

        Pacients = _database.GetCollection<Pacient>("Pacients");
        Kineziterapeutai = _database.GetCollection<Kineziterapeutas>("Kineziterapeutai");
        Visits = _database.GetCollection<Visit>("Visits");
    }

    public void Ping() => _database.RunCommand<BsonDocument>(new BsonDocument("ping", 1));

    public bool IsInitialized()
    {
        var names = _database.ListCollectionNames().ToList();
        return names.Contains("Pacients") && names.Contains("Kineziterapeutai") && names.Contains("Visits");
    }
}

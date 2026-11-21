namespace LibraryManagement.Web.Data;

/// <summary>Map từ mục "MongoDb" trong appsettings.json.</summary>
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017/?replicaSet=rs0";
    public string DatabaseName { get; set; } = "library_management";
}

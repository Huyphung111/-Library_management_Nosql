using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace LibraryManagement.Web.Data;

/// <summary>
/// Quy ước ánh xạ giữa class C# và document MongoDB.
/// Phải gọi Register() một lần ở đầu Program.cs, trước khi dùng bất kỳ collection nào.
/// </summary>
public static class MongoConventions
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        var pack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),          // field camelCase
            new EnumRepresentationConvention(BsonType.String), // enum lưu dạng string
            new IgnoreExtraElementsConvention(true)        // bỏ qua field thừa trong document
        };
        ConventionRegistry.Register("LibraryConventions", pack, _ => true);

        // DateTime đọc ra luôn có Kind = Utc (mọi thời điểm lưu UTC).
        BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));
    }
}

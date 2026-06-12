namespace Aetheria.State.Migration;

public static class LegacyMigrationBoundary
{
    public const string LegacyCacheNamespace = "Assets/Scripts/ServerShared/CultCache";
    public const string LegacyRethinkNamespace = "Assets/Scripts/ServerShared/NIH/RethinkDb";
    public const string LegacyGameDataFile = "GameData/AetherDB.msgpack";

    public static string[] ForbiddenLiveAuthorities { get; } =
    [
        "DatabaseEntry",
        "DatabaseCache",
        "JsonKnownTypes",
        "Newtonsoft.Json",
        "RethinkDb.Driver",
        "RethinkTableAttribute"
    ];
}

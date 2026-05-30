// Spike 1 – Microsoft.Data.Sqlite reference probe
using Microsoft.Data.Sqlite;
Console.WriteLine($"Microsoft.Data.Sqlite: {typeof(SqliteConnection).Assembly.GetName().Version}");

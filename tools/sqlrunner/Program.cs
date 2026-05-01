using System;
using System.IO;
using Microsoft.Data.Sqlite;

if (args.Length < 2)
{
	Console.WriteLine("Usage: SqlRunner <migrations.sql> <target-db-path>");
	return 2;
}

var scriptPath = args[0];
var dbPath = args[1];

if (!File.Exists(scriptPath))
{
	Console.Error.WriteLine($"SQL script not found: {scriptPath}");
	return 3;
}

var sql = File.ReadAllText(scriptPath);
// Normalize SQL Server types to SQLite-friendly types
sql = System.Text.RegularExpressions.Regex.Replace(sql, @"nvarchar\(\s*max\s*\)", "TEXT", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
sql = System.Text.RegularExpressions.Regex.Replace(sql, @"nvarchar\([^)]*\)", "TEXT", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\bbit\b", "INTEGER", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\bdatetime2\b", "TEXT", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
sql = System.Text.RegularExpressions.Regex.Replace(sql, @"\bint\b", "INTEGER", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
try
{
	using var conn = new SqliteConnection(connectionString);
	conn.Open();
	using var cmd = conn.CreateCommand();
	cmd.CommandText = sql;
	cmd.ExecuteNonQuery();
	Console.WriteLine($"Applied script to {dbPath}");
	return 0;
}
catch (Exception ex)
{
	Console.Error.WriteLine("Failed to apply SQL script:");
	Console.Error.WriteLine(ex.ToString());
	return 4;
}

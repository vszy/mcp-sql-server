using MCP.Services;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace MCP.Tools;

[McpServerToolType]
public class GetTableSampleDataTool
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public GetTableSampleDataTool(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    [McpServerTool, Description("Returns the top 5 rows from a table to preview its actual data.")]
    public async Task<string> GetTableSampleData(string tableName)
    {
        await using var conn = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await conn.OpenAsync();

        var parts = tableName.Split('.');
        var schema = parts.Length == 2 ? parts[0].Trim('[', ']') : null;
        var table = parts.Length == 2 ? parts[1].Trim('[', ']') : tableName.Trim('[', ']');
        var fullName = schema != null ? $"[{schema}].[{table}]" : $"[{table}]";

        // Verify table exists
        var checkSql = schema != null
            ? "SELECT 1 FROM sys.tables t INNER JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = @schema AND t.name = @table"
            : "SELECT 1 FROM sys.tables t WHERE t.name = @table";

        await using var checkCmd = new SqlCommand(checkSql, conn);
        checkCmd.Parameters.AddWithValue("@table", table);
        if (schema != null)
            checkCmd.Parameters.AddWithValue("@schema", schema);

        var exists = await checkCmd.ExecuteScalarAsync();
        if (exists == null)
            return $"Table '{tableName}' not found.";

        // Get columns
        await using var colCmd = new SqlCommand(@"
SELECT c.name, UPPER(ty.name) AS DataType
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE c.object_id = OBJECT_ID(@fullName)
ORDER BY c.column_id", conn);
        colCmd.Parameters.AddWithValue("@fullName", fullName);

        var columns = new List<(string Name, string Type)>();
        await using var colReader = await colCmd.ExecuteReaderAsync();
        while (await colReader.ReadAsync())
            columns.Add((colReader.GetString(0), colReader.GetString(1)));
        await colReader.CloseAsync();

        if (columns.Count == 0)
            return $"Table '{tableName}' has no columns.";

        var colList = string.Join(", ", columns.Select(c => $"[{c.Name}]"));

        // Order by PK for consistent results
        var orderBy = "";
        await using var pkCmd = new SqlCommand(@"
SELECT TOP 1 c.name
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID(@fullName2) AND i.is_primary_key = 1
ORDER BY ic.key_ordinal", conn);
        pkCmd.Parameters.AddWithValue("@fullName2", fullName);
        var pkCol = await pkCmd.ExecuteScalarAsync();
        if (pkCol != null)
            orderBy = $" ORDER BY [{pkCol}]";

        // Get top 5
        await using var dataCmd = new SqlCommand($"SELECT TOP 5 {colList} FROM {fullName}{orderBy}", conn);
        await using var dataReader = await dataCmd.ExecuteReaderAsync();

        var sb = new StringBuilder();
        sb.AppendLine($"Top 5 rows from {fullName}:");
        sb.AppendLine();

        // Header
        sb.AppendLine(string.Join(" | ", columns.Select(c => c.Name)));
        sb.AppendLine(string.Join(" | ", columns.Select(c => new string('-', c.Name.Length))));

        // Rows
        while (await dataReader.ReadAsync())
        {
            var row = new List<string>();
            for (int i = 0; i < dataReader.FieldCount; i++)
            {
                var val = dataReader.IsDBNull(i) ? "NULL" : dataReader.GetValue(i)?.ToString() ?? "";
                row.Add(val.Length > 50 ? val[..50] + "..." : val);
            }
            sb.AppendLine(string.Join(" | ", row));
        }

        return sb.ToString();
    }
}

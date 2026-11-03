using MCP.Services;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCP.Tools;

[McpServerToolType]
public class GetStoredProcedureDefinitionTool
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public GetStoredProcedureDefinitionTool(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    [McpServerTool, Description("Gets the full definition (CREATE script) of a stored procedure or function.")]
    public async Task<string> GetStoredProcedureDefinition(string procedureName)
    {
        await using var conn = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await conn.OpenAsync();

        var parts = procedureName.Split('.');
        var name = parts.Length == 2 ? parts[1].Trim('[', ']') : procedureName.Trim('[', ']');
        var schema = parts.Length == 2 ? parts[0].Trim('[', ']') : null;

        var sql = schema != null
            ? @"SELECT sm.definition FROM sys.sql_modules sm
INNER JOIN sys.objects o ON sm.object_id = o.object_id
INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name = @schema AND o.name = @name AND o.type IN ('P','PC','FN','IF','TF','FS','FT')"
            : @"SELECT sm.definition FROM sys.sql_modules sm
INNER JOIN sys.objects o ON sm.object_id = o.object_id
WHERE o.name = @name AND o.type IN ('P','PC','FN','IF','TF','FS','FT')";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", name);
        if (schema != null)
            cmd.Parameters.AddWithValue("@schema", schema);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? $"Stored procedure or function '{procedureName}' not found.";
    }
}

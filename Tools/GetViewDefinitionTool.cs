using MCP.Services;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MCP.Tools;

[McpServerToolType]
public class GetViewDefinitionTool
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public GetViewDefinitionTool(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    [McpServerTool, Description("Gets the full definition (CREATE script) of a view.")]
    public async Task<string> GetViewDefinition(string viewName)
    {
        await using var conn = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await conn.OpenAsync();

        var parts = viewName.Split('.');
        var name = parts.Length == 2 ? parts[1].Trim('[', ']') : viewName.Trim('[', ']');
        var schema = parts.Length == 2 ? parts[0].Trim('[', ']') : null;

        var sql = schema != null
            ? @"SELECT sm.definition FROM sys.sql_modules sm
INNER JOIN sys.views v ON sm.object_id = v.object_id
INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
WHERE s.name = @schema AND v.name = @name"
            : @"SELECT sm.definition FROM sys.sql_modules sm
INNER JOIN sys.views v ON sm.object_id = v.object_id
WHERE v.name = @name";

        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name", name);
        if (schema != null)
            cmd.Parameters.AddWithValue("@schema", schema);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? $"View '{viewName}' not found.";
    }
}

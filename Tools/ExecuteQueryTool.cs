using MCP.Services;
using Microsoft.Data.SqlClient;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;

namespace MCP.Tools;

[McpServerToolType]
public class ExecuteQueryTool
{
    private static readonly Regex WriteStatementPattern = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|GRANT|REVOKE|DENY|EXEC|EXECUTE|sp_executesql)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly IAccessModeProvider _accessModeProvider;
    private readonly ILogger<ExecuteQueryTool> _logger;

    public ExecuteQueryTool(IConnectionStringProvider connectionStringProvider, IAccessModeProvider accessModeProvider, ILogger<ExecuteQueryTool> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _accessModeProvider = accessModeProvider;
        _logger = logger;
    }

    [McpServerTool(ReadOnly = false), Description("Executes a SQL statement against the database. By default the server runs in ReadOnly mode and only SELECT/WITH queries are allowed; sending the 'X-Access-Mode: Full' header on the MCP connection lifts the restriction and allows data manipulation (INSERT/UPDATE/DELETE/MERGE) and DDL/permission statements (CREATE/ALTER/DROP/TRUNCATE/GRANT/REVOKE/etc.).")]
    public async Task<string> ExecuteQuery(McpServer server, string query, CancellationToken cancellationToken)
    {
        if (_accessModeProvider.GetAccessMode() == AccessMode.ReadOnly)
        {
            var trimmed = query.TrimStart();
            var isReadStatement = trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                                   trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);

            if (!isReadStatement || WriteStatementPattern.IsMatch(query))
            {
                throw new McpException("This connection is in ReadOnly mode. Only SELECT/WITH queries are allowed. Reconnect with the 'X-Access-Mode: Full' header to enable data manipulation and DDL statements.");
            }
        }

        await using var conn = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new SqlCommand(query, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var sb = new StringBuilder();
        var rowCount = 0;
        var hasResultSet = false;

        do
        {
            if (reader.FieldCount == 0)
                continue;

            hasResultSet = true;

            var columnNames = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToArray();

            sb.AppendLine(string.Join(" | ", columnNames));
            sb.AppendLine(string.Join(" | ", columnNames.Select(c => new string('-', c.Length))));

            while (await reader.ReadAsync(cancellationToken))
            {
                rowCount++;
                var row = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var val = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.ToString() ?? "";
                    row.Add(val.Length > 100 ? val[..100] + "..." : val);
                }
                sb.AppendLine(string.Join(" | ", row));
            }

            sb.AppendLine();
        } while (await reader.NextResultAsync(cancellationToken));

        if (!hasResultSet)
            return $"Query executed successfully. Rows affected: {reader.RecordsAffected}";

        sb.Insert(0, $"{rowCount} row(s) returned.\n\n");

        return sb.ToString();
    }
}

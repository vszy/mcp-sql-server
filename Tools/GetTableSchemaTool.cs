using MCP.Services;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text;

namespace MCP.Tools;

[McpServerToolType]
public class GetTableSchemaTool
{
    private readonly IConnectionStringProvider _connectionStringProvider;

    public GetTableSchemaTool(IConnectionStringProvider connectionStringProvider)
    {
        _connectionStringProvider = connectionStringProvider;
    }

    [McpServerTool, Description("Gets the full schema of a specific table: columns, types, nullability, identity, defaults, primary key, foreign keys, and indexes.")]
    public async Task<string> GetTableSchema(string tableName)
    {
        var sb = new StringBuilder();

        await using var conn = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await conn.OpenAsync();

        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries);

        string schema;
        string table;

        if (parts.Length == 2)
        {
            schema = parts[0].Trim('[', ']');
            table = parts[1].Trim('[', ']');
        }
        else
        {
            schema = "dbo";
            table = tableName.Trim('[', ']');
        }

        var fullName = $"[{schema}].[{table}]";

        var existsCmd = new SqlCommand("SELECT OBJECT_ID(@name)", conn);
        existsCmd.Parameters.AddWithValue("@name", fullName);

        var objectId = await existsCmd.ExecuteScalarAsync();

        if (objectId == DBNull.Value || objectId == null)
            return $"Table '{fullName}' not found.";

        sb.AppendLine($"Table: {fullName}");
        sb.AppendLine();

        const string columnsSql = @"
SELECT
    c.column_id,
    c.name,
    t.name,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable,
    c.is_identity,
    dc.definition,
    CASE WHEN pk.column_id IS NOT NULL THEN 1 ELSE 0 END AS IsPrimaryKey
FROM sys.columns c
JOIN sys.types t
    ON c.user_type_id = t.user_type_id
LEFT JOIN sys.default_constraints dc
    ON c.default_object_id = dc.object_id
LEFT JOIN
(
    SELECT ic.object_id, ic.column_id
    FROM sys.indexes i
    JOIN sys.index_columns ic
        ON i.object_id = ic.object_id
       AND i.index_id = ic.index_id
    WHERE i.is_primary_key = 1
) pk
    ON pk.object_id = c.object_id
   AND pk.column_id = c.column_id
WHERE c.object_id = OBJECT_ID(@table)
ORDER BY c.column_id;";

        await using (var cmd = new SqlCommand(columnsSql, conn))
        {
            cmd.Parameters.AddWithValue("@table", fullName);

            await using var reader = await cmd.ExecuteReaderAsync();

            sb.AppendLine("Columns:");

            while (await reader.ReadAsync())
            {
                var column = reader.GetString(1);
                var type = reader.GetString(2).ToUpperInvariant();
                var maxLength = reader.GetInt16(3);
                var precision = reader.GetByte(4);
                var scale = reader.GetByte(5);
                var nullable = reader.GetBoolean(6);
                var identity = reader.GetBoolean(7);
                var defaultValue = reader.IsDBNull(8) ? null : reader.GetString(8);
                var isPk = reader.GetInt32(9) == 1;

                var typeString = type;

                switch (type)
                {
                    case "VARCHAR":
                    case "CHAR":
                    case "BINARY":
                    case "VARBINARY":
                        typeString += maxLength == -1 ? "(MAX)" : $"({maxLength})";
                        break;

                    case "NVARCHAR":
                    case "NCHAR":
                        typeString += maxLength == -1 ? "(MAX)" : $"({maxLength / 2})";
                        break;

                    case "DECIMAL":
                    case "NUMERIC":
                        typeString += $"({precision},{scale})";
                        break;
                }

                sb.Append($"  {column} {typeString}");

                if (isPk)
                    sb.Append(" PK");

                if (identity)
                    sb.Append(" IDENTITY");

                sb.Append(nullable ? " NULL" : " NOT NULL");

                if (!string.IsNullOrWhiteSpace(defaultValue))
                    sb.Append($" DEFAULT {defaultValue}");

                sb.AppendLine();
            }
        }

        const string fkSql = @"
SELECT
    fk.name,
    pc.name,
    SCHEMA_NAME(rt.schema_id) + '.' + rt.name,
    rc.name
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc
    ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables pt
    ON fk.parent_object_id = pt.object_id
JOIN sys.columns pc
    ON pc.object_id = fkc.parent_object_id
   AND pc.column_id = fkc.parent_column_id
JOIN sys.tables rt
    ON rt.object_id = fkc.referenced_object_id
JOIN sys.columns rc
    ON rc.object_id = fkc.referenced_object_id
   AND rc.column_id = fkc.referenced_column_id
WHERE pt.object_id = OBJECT_ID(@table)
ORDER BY fk.name;";

        await using (var cmd = new SqlCommand(fkSql, conn))
        {
            cmd.Parameters.AddWithValue("@table", fullName);

            await using var reader = await cmd.ExecuteReaderAsync();

            bool hasRows = false;

            while (await reader.ReadAsync())
            {
                if (!hasRows)
                {
                    sb.AppendLine();
                    sb.AppendLine("Foreign Keys:");
                    hasRows = true;
                }

                sb.AppendLine(
                    $"  {reader.GetString(0)}: {reader.GetString(1)} -> {reader.GetString(2)}({reader.GetString(3)})");
            }
        }

        const string indexSql = @"
SELECT
    i.name,
    i.type_desc,
    i.is_unique,
    i.is_primary_key,
    STRING_AGG(
        c.name +
        CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE ' ASC' END,
        ', '
    ) WITHIN GROUP (ORDER BY ic.key_ordinal) AS Columns
FROM sys.indexes i
JOIN sys.index_columns ic
    ON i.object_id = ic.object_id
   AND i.index_id = ic.index_id
JOIN sys.columns c
    ON c.object_id = ic.object_id
   AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(@table)
AND i.name IS NOT NULL
GROUP BY
    i.name,
    i.type_desc,
    i.is_unique,
    i.is_primary_key
ORDER BY
    i.is_primary_key DESC,
    i.name;";

        await using (var cmd = new SqlCommand(indexSql, conn))
        {
            cmd.Parameters.AddWithValue("@table", fullName);

            await using var reader = await cmd.ExecuteReaderAsync();

            bool hasRows = false;

            while (await reader.ReadAsync())
            {
                if (!hasRows)
                {
                    sb.AppendLine();
                    sb.AppendLine("Indexes:");
                    hasRows = true;
                }

                var name = reader.GetString(0);
                var type = reader.GetString(1);
                var unique = reader.GetBoolean(2);
                var pk = reader.GetBoolean(3);
                var cols = reader.IsDBNull(4) ? "" : reader.GetString(4);

                var label = pk
                    ? "PRIMARY KEY"
                    : unique
                        ? $"UNIQUE {type}"
                        : type;

                sb.AppendLine($"  {label}: {name} ({cols})");
            }
        }

        return sb.ToString();
    }
}

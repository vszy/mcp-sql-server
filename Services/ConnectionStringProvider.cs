using ModelContextProtocol;

namespace MCP.Services;

public class ConnectionStringProvider : IConnectionStringProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IConfiguration _configuration;

    public ConnectionStringProvider(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
    }

    public string GetConnectionString()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HTTP context to resolve a database from.");

        var db = httpContext.GetRouteValue("db") as string;
        if (string.IsNullOrWhiteSpace(db))
            throw new McpException("No database was specified in the request URL. Connect to /mcp/{db} using one of the configured database names.");

        var connectionString = _configuration.GetConnectionString(db);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new McpException($"Unknown database '{db}'. It has no matching entry under ConnectionStrings in configuration.");

        return connectionString;
    }
}

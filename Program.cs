using System.Security.Cryptography;
using System.Text;
using MCP.Services;
using MCP.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IConnectionStringProvider, ConnectionStringProvider>();
builder.Services.AddScoped<IAccessModeProvider, AccessModeProvider>();

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<GetTableSchemaTool>()
    .WithTools<GetStoredProcedureDefinitionTool>()
    .WithTools<GetViewDefinitionTool>()
    .WithTools<GetFunctionDefinitionTool>()
    .WithTools<GetTableSampleDataTool>()
    .WithTools<GetTableLatestRecordsTool>()
    .WithTools<ExecuteQueryTool>();

var app = builder.Build();

var apiKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
builder.Configuration.GetSection("ApiKeys").Bind(apiKeys);
if (apiKeys.Count == 0)
{
    throw new InvalidOperationException("ApiKeys is not configured. Set \"ApiKeys\" in appsettings.json with one entry per database name.");
}

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/mcp", out var remaining))
    {
        await next();
        return;
    }

    var db = remaining.Value?.Trim('/').Split('/')[0];

    if (string.IsNullOrEmpty(db) || !apiKeys.TryGetValue(db, out var expectedKey))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Unknown database.");
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-Api-Key", out var providedKey) ||
        !CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(providedKey.ToString()),
            Encoding.UTF8.GetBytes(expectedKey)))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
        return;
    }

    // Defaults to ReadOnly unless the caller explicitly opts into ReadWrite.
    var accessMode = AccessMode.ReadOnly;
    if (context.Request.Headers.TryGetValue("X-Access-Mode", out var accessModeHeader) &&
        Enum.TryParse<AccessMode>(accessModeHeader.ToString(), ignoreCase: true, out var parsedMode))
    {
        accessMode = parsedMode;
    }
    context.Items["AccessMode"] = accessMode;

    await next();
});

app.MapMcp("/mcp/{db}");

app.Run();

```
  ____   ___  _       ____  _____ ______     _______ ____    __  __  ____ ____
 / ___| / _ \| |     / ___|| ____|  _ \ \   / / ____|  _ \  |  \/  |/ ___|  _ \
 \___ \| | | | |     \___ \|  _| | |_) \ \ / /|  _| | |_) | | |\/| | |   | |_) |
  ___) | |_| | |___   ___) | |___|  _ < \ V / | |___|  _ <  | |  | | |___|  __/
 |____/ \__\_\_____| |____/|_____|_| \_\ \_/  |_____|_| \_\ |_|  |_|\____|_|
```

An ASP.NET Core (.NET 8) [Model Context Protocol](https://modelcontextprotocol.io) server that lets AI clients (Claude Code, etc.) inspect and query multiple SQL Server databases over HTTP.

## Features

- One endpoint per database: `/mcp/{db}`, each with its own API key and connection string.
- **ReadOnly by default**; opt in to write/DDL per connection with the `X-Access-Mode: Full` header.
- Tools:

| Tool | Purpose |
|------|---------|
| `get_table_schema` | Columns, types, keys, foreign keys, indexes |
| `get_view_definition` | CREATE script of a view |
| `get_stored_procedure_definition` | CREATE script of a stored procedure |
| `get_function_definition` | CREATE script of a function |
| `get_table_sample_data` | Top 5 rows of a table |
| `get_table_latest_records` | 5 most recently inserted rows |
| `execute_query` | Run SQL (SELECT/WITH only unless `Full`) |

## Setup

1. Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download) and network access to your SQL Server(s).
2. Copy the sample config and fill it in:
   ```powershell
   Copy-Item appsettings.sample.json appsettings.json
   Copy-Item .mcp.sample.json .mcp.json   # optional, for Claude Code
   ```
3. In `appsettings.json`, add one entry per database under **both** `ApiKeys` and `ConnectionStrings`, using the same name (the `{db}` in the URL). Generate keys with e.g. `openssl rand -hex 16`.
4. Run:
   ```powershell
   dotnet run
   ```

`appsettings.json` and `.mcp.json` hold secrets and are git-ignored. Never commit them.

## Connecting a client

```json
{
  "mcpServers": {
    "my-database": {
      "type": "http",
      "url": "http://localhost:5042/mcp/my-database",
      "headers": {
        "X-Api-Key": "<key>",
        "X-Access-Mode": "ReadOnly"
      }
    }
  }
}
```

| Header | Required | Description |
|--------|----------|-------------|
| `X-Api-Key` | yes | Must match `ApiKeys:{db}` |
| `X-Access-Mode` | no | `ReadOnly` (default) or `Full` |

Unknown `{db}` returns 404; a wrong key returns 401.

## Project layout

```
Program.cs      Host setup, API-key + access-mode middleware, MCP routing
Services/       Per-request connection string and access mode providers
Tools/          MCP tool implementations
```

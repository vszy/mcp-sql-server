# AGENTS.md

Guidance for AI coding agents working in this repository.

## Overview

ASP.NET Core (.NET 8) MCP server exposing SQL Server inspection/query tools over HTTP at `/mcp/{db}`. Uses `ModelContextProtocol.AspNetCore` and `Microsoft.Data.SqlClient`.

## Commands

- Build: `dotnet build`
- Run: `dotnet run` (default dev URL is in `Properties/launchSettings.json`)

There is no test project yet.

## Structure

- `Program.cs`: DI, tool registration (`.WithTools<T>()`), middleware that validates `X-Api-Key` per `{db}` and reads `X-Access-Mode`.
- `Services/`: `ConnectionStringProvider` (resolves `ConnectionStrings:{db}` from the route) and `AccessModeProvider` (ReadOnly/Full per request).
- `Tools/`: one class per MCP tool. New tools must be registered in `Program.cs`.

## Rules

- **Never commit secrets.** `appsettings.json` and `.mcp.json` are git-ignored; they contain real API keys and DB passwords. Do not print, copy or echo their values into code, docs, commits or logs.
- When adding config keys, update `appsettings.sample.json` with placeholder values only.
- Keep the server **ReadOnly by default**. Any write/DDL path must be gated on `AccessMode.Full`.
- Always use parameterized queries or properly quoted identifiers (`QUOTENAME`) for user-supplied table/object names. Never concatenate raw input into SQL.
- Each database needs matching names in both `ApiKeys` and `ConnectionStrings`.
- Match existing style: file-scoped namespaces, nullable enabled, constructor-injected services, `[McpServerTool]` + `[Description]` on tool methods.
- Update `README.md` when adding or changing tools or configuration.

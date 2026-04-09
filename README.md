# SqlCli

A .NET 10 command-line tool for executing SQL Server queries with AST-based statement filtering, flexible authentication (including SSPI authentication), and configurable safety guardrails. Designed for use by AI agents and humans.

## Goals

- Provide a safe way for AI agents to run investigative SQL queries against SQL Server
- Prevent destructive operations through whitelist-based filtering using a real T-SQL parser
- Support Windows domain authentication without requiring the parent process to run under `runas /netonly`
- Produce machine-parseable output (CSV, JSON) that agents can reason over
- Be usable by humans too, with clear help and readable table output

## Features

### Statement Filtering

Queries are parsed using Microsoft's [ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom) T-SQL parser and validated against a whitelist of allowed statement types. The same parser is used for both validation and execution, eliminating parser differential attacks.

The default whitelist allows only `SelectStatement` (which includes CTEs). The whitelist is configured in `sqlcli.config.jsonc` next to the executable and cannot be overridden via CLI.

Blocked queries are logged to a structured JSON audit log with timestamps and PIDs.

### Authentication

Three mutually exclusive auth modes (exactly one required):

| Mode | Flags | Description |
|------|-------|-------------|
| **Windows Auth** | `--windows-auth` | Uses current Windows identity |
| **Domain Auth** | `--domain` `--user` `--password-stdin` | SSPI with explicit credentials (netonly-equivalent via NegotiateAuthentication) |
| **SQL Auth** | `--sql-user` `--sql-password` | SQL Server login |

Domain auth credentials can also be set via environment variables (`SQLCLI_DOMAIN`, `SQLCLI_USER`, `SQLCLI_PASSWORD`). If all three are set, domain auth activates automatically without any CLI auth flags.

**Credential security:** `--password-stdin` reads the password from stdin (not visible in process list). There is no `--password` flag — use `--password-stdin` or the `SQLCLI_PASSWORD` env var.

### Output Formats

| Format | Flag | Multiple Result Sets | Best For |
|--------|------|---------------------|----------|
| **CSV** (default) | `--format csv` | Error | Agents (token-efficient) |
| **JSON** | `--format json` | Supported | Agents (structured) |
| **Table** | `--format table` | Supported | Humans (terminal) |

All data goes to stdout. All errors go to stderr as a JSON array.

### Safety Guardrails

- **Statement whitelist** -- only allowed T-SQL statement types can execute (default: SELECT only)
- **Row limit** -- configurable max rows per result set (default: 100, override with `--max-rows`)
- **Input size limits** -- `--query` hard limit of 1 KB, `--file` default limit of 50 KB
- **TLS by default** -- `TrustServerCertificate` defaults to false (opt-in with `--trust-server-certificate`)
- **Audit log** -- all blocked queries logged as JSON lines with PID
- **Error truncation** -- SQL text in error messages truncated to 200 characters

### Exit Codes

Exit codes are flags (combined via bitwise OR):

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | SQL error (connection failed, query failed, max rows exceeded) |
| 2 | Filter rejection (blocked statement type) |
| 4 | Invalid arguments (missing flags, conflicting auth, invalid format, input size exceeded) |
| 8 | Config error (invalid JSON) |
| 16 | Auth error (SSPI/credentials failed) |
| 32 | Timeout (query or connection) |

Use `--error-codes` to print this reference.

## Usage

```bash
# Windows auth -- query a table
sqlcli --server myhost --database mydb --windows-auth --query "SELECT TOP 10 * FROM Orders"

# Domain auth via stdin (netonly-equivalent)
echo secret | sqlcli --server myhost --database mydb --domain CORP --user jdoe --password-stdin --query "SELECT @@VERSION"

# SQL auth with JSON output
sqlcli --server myhost --database mydb --sql-user sa --sql-password pass --query "SELECT @@VERSION" --format json

# Execute a SQL file with table output
sqlcli --server myhost --database mydb --windows-auth --file "report.sql" --format table

# Agent bootstrapping guide
sqlcli --agent-help
```

### Environment Variables

Domain auth credentials can be set as environment variables. When all three are set, domain auth activates automatically — no auth flags needed:

```powershell
$env:SQLCLI_DOMAIN = "MYDOMAIN"
$env:SQLCLI_USER = "myuser"
$env:SQLCLI_PASSWORD = "mypassword"

# Domain auth activates automatically:
sqlcli --server myhost --database mydb --query "SELECT 1"
```

These are the only environment variables supported. All other settings (server, database, timeouts, etc.) come from config files or CLI args.

### Agent Integration

Run `sqlcli --agent-help` to get a comprehensive guide formatted for inclusion in `CLAUDE.md` or similar agent configuration files. This covers auth modes, output schemas, error handling, whitelist configuration, and common query patterns.

## Configuration

The file `sqlcli.config.jsonc` must be in the same directory as the executable. If missing, a safe default is generated automatically.

```json
{
  "security": {
    "filterMode": "whitelist",
    "allowedStatements": ["SelectStatement"],
    "allowedSelectFeatures": [],
    "audit": { "enabled": true, "path": "sqlcli-audit.log" }
  },
  "operational": {
    "timeout": 30,
    "connectTimeout": 15,
    "maxRows": 100,
    "maxFileSize": 51200,
    "format": "csv"
  },
  "app": {
    "server": null,
    "database": null,
    "trustServerCertificate": false,
    "noEncrypt": false
  }
}
```

### Allowed Select Features

When `SelectStatement` is in the whitelist, these sub-features are still blocked by default. Add values to `allowedSelectFeatures` to permit them:

| Config Value | What It Allows | Risk |
|---|---|---|
| `IntoClause` | `SELECT INTO` (creates new tables) | Creates tables, consumes disk space |
| `OpenRowset` | `OPENROWSET(BULK ...)` (reads server files, connects to external servers) | File system access, external server connections |
| `OpenDatasource` | `OPENDATASOURCE(...)` (ad-hoc external data source connections) | Connects to arbitrary external data sources |
| `OpenQuery` | `OPENQUERY(LinkedServer, 'query')` (linked server queries) | Executes arbitrary SQL on linked servers |
| `UnlimitedMaxRecursion` | `OPTION (MAXRECURSION 0)` (unlimited CTE recursion) | Can cause runaway queries and resource exhaustion |

### Whitelist Statement Types

The whitelist uses [ScriptDom](https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom) `TSqlStatement` class names:

| Config Value | SQL It Allows |
|---|---|
| `SelectStatement` | SELECT, WITH...SELECT (CTEs) |
| `ExecuteStatement` | EXEC, EXECUTE (enables dynamic SQL -- use with caution) |
| `InsertStatement` | INSERT |
| `UpdateStatement` | UPDATE |
| `DeleteStatement` | DELETE |
| `MergeStatement` | MERGE |
| `CreateTableStatement` | CREATE TABLE |
| `DropTableStatement` | DROP TABLE |
| `TruncateTableStatement` | TRUNCATE TABLE |

## Security Model

The statement filter is **defense-in-depth**, not a complete security boundary. Server-side permissions (e.g., `db_datareader`) are the primary guardrail.

**What the filter catches:**
- DDL statements (CREATE, DROP, ALTER, TRUNCATE)
- DML mutations (INSERT, UPDATE, DELETE, MERGE)
- EXEC/EXECUTE (dynamic SQL, stored procedures)
- SELECT INTO (table creation via SELECT) -- blocked by default, configurable via `allowedSelectFeatures`
- OPENROWSET (external file/server access) -- blocked by default, configurable via `allowedSelectFeatures`
- OPENDATASOURCE (ad-hoc external data source access) -- blocked by default, configurable via `allowedSelectFeatures`

**What the filter cannot catch:**
- **Views, functions, and CLR objects with side effects** -- a SELECT from a view or function that internally modifies data cannot be detected at parse time
- **Linked server queries** -- `SELECT * FROM LinkedServer.db.dbo.Table` is indistinguishable from a local table query at the AST level
- **Server-side exploits** -- if the connected user has elevated permissions, the filter cannot prevent server-side damage from allowed statement types

**Resource limits:**
- `--max-rows` enforces a minimum of 1 (0 or negative values are rejected)
- `--timeout` and `--connect-timeout` enforce a range of 1-300 seconds

## Security Mode (Build-Time)

SqlCli must be built with an explicit `SecurityMode` property. The build fails if this is not set, ensuring a conscious choice.

### Standard Mode

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Standard
```

Security whitelists are loaded from `sqlcli.config.jsonc` at runtime. This is the default for development and flexible deployments where an administrator manages the config file.

### Hardened Mode

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Hardened
```

Security whitelists are compiled into the binary. The config file's security section is ignored. This prevents an AI agent (or any process with file system access) from escalating its SQL permissions by modifying the config file before invoking SqlCli.

Hardened mode compiles in:
- `AllowedStatements`: `["SelectStatement"]`
- `AllowedSelectFeatures`: `[]` (all dangerous features blocked)
- Audit logging enabled

Operational and app settings (timeouts, server, database, etc.) are still loaded from config files and CLI args in both modes.

## Limitations

- **Windows only** -- uses `NegotiateAuthentication` SSPI for domain authentication, targets `net10.0-windows`
- **No interactive mode** -- single query per invocation (by design, for agent use)
- **Row limit is per result set** -- a multi-statement batch could return `maxRows` per statement
- **CSV doesn't support multiple result sets** -- use JSON or run queries separately
- **Regex filter removed** -- only the ScriptDom AST filter is available (this is intentional for security)
- **Config file must be writable on first run** -- generates a default if missing

## Building

SecurityMode is required. Choose Standard (config-file whitelists) or Hardened (compiled-in whitelists):

```bash
dotnet build SqlCli.slnx -p:SecurityMode=Standard
dotnet build SqlCli.slnx -p:SecurityMode=Hardened
```

## Testing

```bash
dotnet test SqlCli.slnx -p:SecurityMode=Standard
```

## Dependencies

- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient) -- SQL Server connectivity
- [Microsoft.SqlServer.TransactSql.ScriptDom](https://www.nuget.org/packages/Microsoft.SqlServer.TransactSql.ScriptDom) -- T-SQL AST parser
- [System.CommandLine](https://www.nuget.org/packages/System.CommandLine) -- CLI argument parsing
- [CsvHelper](https://www.nuget.org/packages/CsvHelper) -- RFC 4180 CSV output
- [MSTest](https://www.nuget.org/packages/MSTest) + [NSubstitute](https://www.nuget.org/packages/NSubstitute) -- testing

## License

MIT License. See [LICENSE](LICENSE) for details.

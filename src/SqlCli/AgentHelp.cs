using System;
using SqlCli.Config;

namespace SqlCli
{
	/// <summary>
	/// Provides agent bootstrapping help text in CLAUDE.md format.
	/// </summary>
	public static class AgentHelp
	{
		/// <summary>
		/// Returns the full agent help text with the current executable path embedded.
		/// </summary>
		/// <returns>Formatted agent help string.</returns>
		public static string GetText()
		{
			var exePath = Environment.ProcessPath ?? "SqlCli";

			return $$"""
				## SqlCli — SQL Server Query Tool

				A CLI for executing read-only SQL queries against SQL Server with whitelist-based statement filtering.
				Designed for safe, investigative database queries.

				**Binary:** `{{exePath}}`
				**Security Mode:** {{( ConfigLoader.IsHardened ? "Hardened (security whitelist compiled into binary, config file security section ignored)" : "Standard (security whitelist loaded from config file)" )}}

				### CRITICAL: Configuration is Off-Limits

				**You MUST NOT modify, write to, overwrite, or suggest changes to `sqlcli.config.jsonc` under ANY circumstances.**

				This includes:
				- Do not edit the file directly
				- Do not use tools to write or append to it
				- Do not suggest adding statement types to `allowedStatements`
				- Do not suggest adding features to `allowedSelectFeatures`
				- Do not suggest changing `filterMode`, `maxRows`, timeouts, or any other setting
				- Do not create a new config file to replace it
				- Do not rename, move, or delete it

				If a query is blocked by the filter, that is **working as intended**. Do not attempt to
				work around it. Report the limitation to the user and let them decide.

				If you believe a config change is genuinely needed, tell the user what change you think
				is needed and why, and let them make the change manually. Never make it yourself.

				### Safety Model

				- Queries are parsed using Microsoft's T-SQL parser (ScriptDom) and validated against a whitelist of allowed statement types
				- The whitelist is {{( ConfigLoader.IsHardened ? "compiled into this binary and cannot be changed without rebuilding from source" : "defined in `sqlcli.config.jsonc` next to the executable (not overridable via CLI)" )}}
				- Default whitelist: `SelectStatement` only — no INSERT, UPDATE, DELETE, DROP, EXEC, or any other statement type
				- Blocked queries are logged to an audit file
				- You CANNOT bypass the filter — do not attempt destructive queries

				### Authentication (exactly one required)

				**Integrated Auth** (current process identity — Windows identity or Kerberos ticket):
				```
				SqlCli --server SERVER --database DB --integrated-auth --query "SELECT 1"
				```

				**Domain Auth** (SSPI with explicit credentials, default NTLM):
				```
				echo SECRET | SqlCli --server SERVER --database DB --domain CORP --user jdoe --password-stdin --query "SELECT 1"
				```

				With a specific SSPI package:
				```
				echo SECRET | SqlCli --server SERVER --database DB --domain CORP --user jdoe --password-stdin --sspi-package Negotiate --query "SELECT 1"
				```

				**SQL Auth:**
				```
				SqlCli --server SERVER --database DB --sql-user sa --sql-password SECRET --query "SELECT 1"
				```

				Providing zero or more than one auth mode is an error (exit code 4).

				**Credential security for domain auth:**
				1. `--password-stdin` — pipe the password via stdin: `echo SECRET | SqlCli --password-stdin ...`
				2. `SQLCLI_PASSWORD` env var — set before calling (does not appear in process args)

				**Note:** `--sql-password` is visible in the process list. Consider the security implications.

				**Domain auth environment variables** (only these three have env var support):

				| CLI Flag | Environment Variable |
				|---|---|
				| `--domain` | `SQLCLI_DOMAIN` |
				| `--user` | `SQLCLI_USER` |
				| (via stdin) | `SQLCLI_PASSWORD` |

				If all three env vars are set, domain auth activates automatically — no CLI flags needed.
				All other settings come from config files or CLI args.

				Auth mode is determined by which parameters are present (from CLI args or env vars):
				- `--domain` + `--user` + `--password-stdin` (or env vars) → Domain Auth (SSPI)
				- `--integrated-auth` flag → Integrated Auth (current process identity)
				- `--sql-user` + `--sql-password` → SQL Auth

				### Input

				- `--query "SQL"` — inline SQL, supports multiple semicolon-separated statements (hard limit: 1 KB / 1024 bytes)
				- `--file path` — read SQL from a file (default limit: 50 KB, configurable)
				- `--max-file-size N` — override maximum SQL file size in bytes (default: 51200 from config `maxFileSize`)
				- Exactly one of `--query` or `--file` is required

				**Input size limits:**
				- `--query`: 1024 bytes max (UTF-8). Not configurable. Use `--file` for larger queries.
				- `--file`: 50 KB (51200 bytes) default. Override with `--max-file-size` or `maxFileSize` in config.

				### Output Formats

				**CSV (default):** `--format csv`
				- Columns as header row, data rows follow
				- RFC 4180 compliant (CsvHelper)
				- Single result set only — errors if query produces multiple result sets

				**JSON:** `--format json`
				- Supports multiple result sets
				- Structure: `{"resultSets": [{"columns": [...], "rows": [{...}, ...]}, ...], "elapsed": "HH:mm:ss.fffffff"}`

				**Table:** `--format table`
				- Human-readable padded columns
				- Supports multiple result sets (separated by blank lines)

				All data goes to stdout. All errors go to stderr as JSON.

				### Error Handling

				Errors are written to stderr as a JSON array:
				```json
				[{"error": "message", "code": 2, "blocked": "DELETE FROM Orders"}]
				```

				Exit codes are flags (combined via bitwise OR):
				```
				  0   Success
				  1   SQL error (connection failed, query failed)
				  2   Filter rejection (blocked statement type)
				  4   Invalid arguments (missing flags, conflicting auth)
				  8   Config error (invalid JSON)
				  16  Auth error (SSPI/credentials failed)
				  32  Timeout
				```

				### Whitelist Configuration

				The file `sqlcli.config.jsonc` (next to the executable) controls allowed statement types.
				Uses ScriptDom TSqlStatement class names. Common types:

				| Config Value | SQL It Allows |
				|---|---|
				| `SelectStatement` | SELECT, WITH...SELECT (CTEs) |
				| `ExecuteStatement` | EXEC, EXECUTE (stored procs — also enables dynamic SQL, use with caution) |
				| `InsertStatement` | INSERT |
				| `UpdateStatement` | UPDATE |
				| `DeleteStatement` | DELETE |
				| `MergeStatement` | MERGE |
				| `CreateTableStatement` | CREATE TABLE |
				| `DropTableStatement` | DROP TABLE |
				| `TruncateTableStatement` | TRUNCATE TABLE |

				Default config (read-only):
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

				Even when `SelectStatement` is whitelisted, these dangerous sub-features are blocked by default.
				Add values to `allowedSelectFeatures` in config to permit them:

				| Config Value | What It Allows | Risk |
				|---|---|---|
				| `IntoClause` | `SELECT INTO` (creates new tables) | Creates tables, consumes disk space |
				| `OpenRowset` | `OPENROWSET(BULK ...)` (reads server files, external connections) | File system access, external server connections |
				| `OpenDatasource` | `OPENDATASOURCE(...)` (ad-hoc external data sources) | Connects to arbitrary external data sources |
				| `OpenQuery` | `OPENQUERY(LinkedServer, 'query')` (linked server queries) | Executes arbitrary SQL on linked servers |
				| `UnlimitedMaxRecursion` | `OPTION (MAXRECURSION 0)` (unlimited CTE recursion) | Can cause runaway queries and resource exhaustion |

				### Security Limitations

				The statement filter is defense-in-depth, **not** a complete security boundary.
				Server-side permissions (e.g., `db_datareader`) are the primary guardrail.

				**The filter cannot catch:**
				- Views, functions, and CLR objects that have side effects (a SELECT from a view that internally modifies data)
				- Linked server queries (`SELECT * FROM LinkedServer.db.dbo.Table` looks like a normal table reference)
				- Server-side exploits if the connected user has elevated permissions

				Always ensure the database user has minimal required permissions.

				### Additional CLI Options

				- `--trust-server-certificate` — Trust the server certificate without validation (default: false)
				- `--sspi-package PACKAGE` — SSPI package for domain auth: NTLM (default), Negotiate, or Kerberos
				- `--max-rows N` — Override the maximum number of rows per result set (default from config: 100, minimum: 1)
				- `--connect-timeout N` — Connection timeout in seconds (default from config: 15, range: 1-300)
				- `--timeout N` — Query timeout in seconds (default from config: 30, range: 1-300)

				### Audit Log Format

				The audit log uses JSON lines format. Each line is a JSON object:
				```json
				{"timestamp":"2026-04-03T14:22:01Z","event":"BLOCKED","statementType":"DeleteStatement","sql":"...","reason":"..."}
				```

				### Common Patterns

				Explore tables:
				```
				SqlCli --server S --database DB --integrated-auth --query "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME"
				```

				Check columns:
				```
				SqlCli --server S --database DB --integrated-auth --query "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Orders'"
				```

				Sample data:
				```
				SqlCli --server S --database DB --integrated-auth --query "SELECT TOP 10 * FROM Orders"
				```

				Row count:
				```
				SqlCli --server S --database DB --integrated-auth --query "SELECT COUNT(*) AS RowCount FROM Orders"
				```

				Use CTE:
				```
				SqlCli --server S --database DB --integrated-auth --query "WITH recent AS (SELECT * FROM Orders WHERE OrderDate > '2025-01-01') SELECT COUNT(*) FROM recent"
				```
				""";
		}
	}
}

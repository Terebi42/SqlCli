using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Immutable security configuration loaded exclusively from the config file.
	/// These settings cannot be overridden via CLI arguments or environment variables.
	/// </summary>
	public sealed class SecurityConfig
	{
		/// <summary>
		/// Gets the filter mode. Only "whitelist" is currently supported.
		/// </summary>
		[ConfigComment( "Only 'whitelist' mode is supported. Queries are parsed by ScriptDom and\nonly statement types listed in allowedStatements are permitted to execute." )]
		[JsonPropertyName( "filterMode" )]
		public string FilterMode { get; init; } = "whitelist";

		/// <summary>
		/// Gets the list of allowed TSqlStatement type names.
		/// Empty list means block all statements (safe default).
		/// </summary>
		[ConfigComment( """
			Allowed TSqlStatement type names. Only these statement types can execute.
			Default when generated: ["SelectStatement"] — SELECT and WITH...SELECT (CTEs) only.

			DO NOT add types here without understanding the security implications.
			Each type you add allows that entire category of SQL operations.
			To enable a type, add its name as a string to the array below.

			Available types:
			  "ExecuteStatement"         — EXEC/EXECUTE (WARNING: enables dynamic SQL)
			  "InsertStatement"          — INSERT
			  "UpdateStatement"          — UPDATE
			  "DeleteStatement"          — DELETE
			  "MergeStatement"           — MERGE
			  "CreateTableStatement"     — CREATE TABLE
			  "AlterTableStatement"      — ALTER TABLE
			  "DropTableStatement"       — DROP TABLE
			  "TruncateTableStatement"   — TRUNCATE TABLE
			  "CreateViewStatement"      — CREATE VIEW
			  "CreateProcedureStatement" — CREATE PROCEDURE
			  "DeclareVariableStatement" — DECLARE @var
			  "SetVariableStatement"     — SET @var = ...

			Example: to allow SELECT and INSERT:
			  "allowedStatements": ["SelectStatement", "InsertStatement"]
			""" )]
		[JsonPropertyName( "allowedStatements" )]
		public List<string> AllowedStatements { get; init; } = new();

		/// <summary>
		/// Gets the list of allowed SELECT sub-features (IntoClause, OpenRowset, OpenDatasource).
		/// </summary>
		[ConfigComment( """
			Dangerous sub-features within SELECT that are blocked by default.
			Even when SelectStatement is allowed, these require explicit opt-in.
			To enable a feature, add its name as a string to the array below.

			DO NOT enable these unless you understand the risks.

			Available features:
			  "IntoClause"              — SELECT INTO (creates new tables, consumes disk)
			  "OpenRowset"              — OPENROWSET (reads server files, connects to external servers)
			  "OpenDatasource"          — OPENDATASOURCE (connects to arbitrary external data sources)
			  "OpenQuery"               — OPENQUERY (executes arbitrary SQL on linked servers)
			  "UnlimitedMaxRecursion"   — OPTION (MAXRECURSION 0) (allows unlimited CTE recursion)

			Example: to allow SELECT INTO:
			  "allowedSelectFeatures": ["IntoClause"]
			""" )]
		[JsonPropertyName( "allowedSelectFeatures" )]
		public List<string> AllowedSelectFeatures { get; init; } = new();

		/// <summary>
		/// Gets the audit log configuration.
		/// </summary>
		[ConfigComment( "Logs all blocked query attempts as JSON lines. Path is relative to the\nexecutable directory." )]
		[JsonPropertyName( "audit" )]
		public AuditConfig Audit { get; init; } = new();
	}

	/// <summary>
	/// Configuration for the audit log that records blocked statements.
	/// </summary>
	public sealed class AuditConfig
	{
		/// <summary>
		/// Gets whether audit logging is enabled.
		/// </summary>
		public bool Enabled { get; init; } = true;

		/// <summary>
		/// Gets the file path for the audit log, relative to the executable directory.
		/// </summary>
		public string Path { get; init; } = "sqlcli-audit.log";
	}
}

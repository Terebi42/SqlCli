using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using SqlCli.Config;

namespace SqlCli.Filtering
{
	/// <summary>
	/// Filters SQL statements using the ScriptDom T-SQL parser for accurate statement type detection.
	/// </summary>
	public class ScriptDomStatementFilter : IStatementFilter
	{
		private readonly HashSet<string> _allowedTypes;
		private readonly HashSet<string> _allowedSelectFeatures;
		private readonly string _auditLogPath;

		/// <summary>
		/// Maximum length of SQL text included in violation messages before truncation.
		/// </summary>
		private const int MaxSqlDisplayLength = 200;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptDomStatementFilter"/> class
		/// from a <see cref="SecurityConfig"/> and an audit log path.
		/// </summary>
		/// <param name="security">Immutable security configuration.</param>
		/// <param name="auditLogPath">Path to the audit log file, or null to disable logging.</param>
		public ScriptDomStatementFilter( SecurityConfig security, string auditLogPath )
		{
			_allowedTypes = new HashSet<string>( security.AllowedStatements, StringComparer.OrdinalIgnoreCase );
			_allowedSelectFeatures = new HashSet<string>( security.AllowedSelectFeatures ?? [], StringComparer.OrdinalIgnoreCase );
			_auditLogPath = auditLogPath;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptDomStatementFilter"/> class.
		/// </summary>
		/// <param name="allowedTypes">List of allowed ScriptDom statement type names (case-insensitive).</param>
		/// <param name="auditLogPath">Path to the audit log file, or null to disable logging.</param>
		/// <param name="allowedSelectFeatures">List of allowed SELECT sub-features (IntoClause, OpenRowset, OpenDatasource).</param>
		public ScriptDomStatementFilter( List<string> allowedTypes, string auditLogPath, List<string> allowedSelectFeatures = null )
		{
			_allowedTypes = new HashSet<string>( allowedTypes, StringComparer.OrdinalIgnoreCase );
			_allowedSelectFeatures = new HashSet<string>( allowedSelectFeatures ?? [], StringComparer.OrdinalIgnoreCase );
			_auditLogPath = auditLogPath;
		}

		/// <summary>
		/// Validates the SQL text by parsing with ScriptDom and checking each statement type.
		/// </summary>
		/// <param name="sql">Raw SQL text to validate.</param>
		/// <returns>Filter result indicating whether all statements are allowed.</returns>
		public FilterResult Validate( string sql )
		{
			if ( string.IsNullOrWhiteSpace( sql ) )
			{
				return FilterResult.Success();
			}

			var parser = new TSql170Parser( initialQuotedIdentifiers: true );
			using var reader = new StringReader( sql );
			var fragment = parser.Parse( reader, out var errors );

			if ( errors.Count > 0 )
			{
				var violations = errors.Select( e =>
					new FilterViolation(
						TruncateSql( sql ),
						"(parse error)",
						$"SQL parse error at line {e.Line}, column {e.Column}: {e.Message}" ) )
					.ToList();
				WriteAuditLog( violations );
				return FilterResult.Blocked( violations );
			}

			if ( fragment is not TSqlScript script )
			{
				return FilterResult.Success();
			}

			var allViolations = new List<FilterViolation>();
			var allStatements = new List<string>();

			foreach ( var batch in script.Batches )
			{
				foreach ( var statement in batch.Statements )
				{
					var typeName = statement.GetType().Name;
					var statementText = GetStatementText( sql, statement );
					allStatements.Add( statementText );

					if ( !_allowedTypes.Contains( typeName ) )
					{
						allViolations.Add( new FilterViolation(
							TruncateSql( statementText ),
							typeName,
							$"{typeName} not in whitelist" ) );
					}
					else if ( statement is SelectStatement selectStatement )
					{
						var selectViolations = CheckSelectFeatures( selectStatement, statementText );
						allViolations.AddRange( selectViolations );
					}
				}
			}

			if ( allViolations.Count > 0 )
			{
				WriteAuditLog( allViolations );
				return FilterResult.Blocked( allViolations );
			}

			return FilterResult.Success( allStatements );
		}

		/// <summary>
		/// Truncates SQL text to the maximum display length, appending a suffix if truncated.
		/// </summary>
		/// <param name="sql">SQL text to truncate.</param>
		/// <returns>Truncated SQL text.</returns>
		internal static string TruncateSql( string sql )
		{
			if ( sql is null || sql.Length <= MaxSqlDisplayLength )
			{
				return sql;
			}

			return sql[..MaxSqlDisplayLength] + "(truncated)";
		}

		/// <summary>
		/// Extracts the text of a single statement from the full SQL script.
		/// </summary>
		/// <param name="fullSql">Complete SQL text.</param>
		/// <param name="statement">Parsed statement with offset information.</param>
		/// <returns>Trimmed statement text.</returns>
		private static string GetStatementText( string fullSql, TSqlStatement statement )
		{
			var start = statement.StartOffset;
			var length = statement.FragmentLength;
			if ( start >= 0 && start + length <= fullSql.Length )
			{
				return fullSql.Substring( start, length ).Trim().TrimEnd( ';' ).Trim();
			}

			return "(unknown)";
		}

		/// <summary>
		/// Inspects a SelectStatement AST for dangerous sub-features (INTO, OPENROWSET, OPENDATASOURCE).
		/// </summary>
		/// <param name="selectStatement">The parsed SELECT statement.</param>
		/// <param name="statementText">The original SQL text of the statement.</param>
		/// <returns>List of violations for blocked features.</returns>
		private List<FilterViolation> CheckSelectFeatures( SelectStatement selectStatement, string statementText )
		{
			var violations = new List<FilterViolation>();
			var visitor = new DangerousSelectFeatureVisitor();
			selectStatement.Accept( visitor );

			if ( visitor.HasIntoClause && !_allowedSelectFeatures.Contains( "IntoClause" ) )
			{
				violations.Add( new FilterViolation(
					TruncateSql( statementText ),
					"SelectInto",
					"SELECT INTO is blocked. Add \"IntoClause\" to allowedSelectFeatures in config to allow." ) );
			}

			if ( visitor.HasOpenRowset && !_allowedSelectFeatures.Contains( "OpenRowset" ) )
			{
				violations.Add( new FilterViolation(
					TruncateSql( statementText ),
					"OpenRowset",
					"OPENROWSET is blocked. Add \"OpenRowset\" to allowedSelectFeatures in config to allow." ) );
			}

			if ( visitor.HasOpenDatasource && !_allowedSelectFeatures.Contains( "OpenDatasource" ) )
			{
				violations.Add( new FilterViolation(
					TruncateSql( statementText ),
					"OpenDatasource",
					"OPENDATASOURCE is blocked. Add \"OpenDatasource\" to allowedSelectFeatures in config to allow." ) );
			}

			if ( visitor.HasOpenQuery && !_allowedSelectFeatures.Contains( "OpenQuery" ) )
			{
				violations.Add( new FilterViolation(
					TruncateSql( statementText ),
					"OpenQuery",
					"OPENQUERY is blocked. Add \"OpenQuery\" to allowedSelectFeatures in config to allow." ) );
			}

			if ( visitor.HasUnlimitedMaxRecursion && !_allowedSelectFeatures.Contains( "UnlimitedMaxRecursion" ) )
			{
				violations.Add( new FilterViolation(
					TruncateSql( statementText ),
					"MaxRecursion0",
					"OPTION (MAXRECURSION 0) is blocked because it allows unlimited recursion. Add \"UnlimitedMaxRecursion\" to allowedSelectFeatures in config to allow." ) );
			}

			return violations;
		}

		/// <summary>
		/// Writes blocked statements to the audit log file as JSON lines.
		/// </summary>
		/// <param name="violations">List of violations to log.</param>
		private void WriteAuditLog( List<FilterViolation> violations )
		{
			if ( _auditLogPath is null )
			{
				return;
			}

			var pid = Environment.ProcessId;
			var lines = violations.Select( v =>
				JsonSerializer.Serialize( new
				{
					timestamp = DateTime.UtcNow.ToString( "O" ),
					pid,
					@event = "BLOCKED",
					statementType = v.Keyword,
					sql = v.Statement,
					reason = v.Reason
				} ) );

			try
			{
				File.AppendAllLines( _auditLogPath, lines );
			}
			catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException )
			{
				Console.Error.WriteLine( $"WARNING: Failed to write audit log to {_auditLogPath}: {ex.Message}" );
			}
		}

		/// <summary>
		/// Visitor that walks a SelectStatement AST to detect dangerous sub-features.
		/// </summary>
		private sealed class DangerousSelectFeatureVisitor : TSqlFragmentVisitor
		{
			/// <summary>Gets whether a SELECT INTO clause was found.</summary>
			public bool HasIntoClause { get; private set; }

			/// <summary>Gets whether an OPENROWSET table reference was found.</summary>
			public bool HasOpenRowset { get; private set; }

			/// <summary>Gets whether an OPENDATASOURCE table reference was found.</summary>
			public bool HasOpenDatasource { get; private set; }

			/// <summary>Gets whether an OPENQUERY table reference was found.</summary>
			public bool HasOpenQuery { get; private set; }

			/// <summary>Gets whether OPTION (MAXRECURSION 0) was found (unlimited recursion).</summary>
			public bool HasUnlimitedMaxRecursion { get; private set; }

			/// <inheritdoc />
			public override void ExplicitVisit( SelectStatement node )
			{
				if ( node.Into is not null )
				{
					HasIntoClause = true;
				}

				if ( node.OptimizerHints is not null )
				{
					foreach ( var hint in node.OptimizerHints )
					{
						if ( hint is LiteralOptimizerHint literalHint
							&& literalHint.HintKind == OptimizerHintKind.MaxRecursion
							&& literalHint.Value is not null
							&& literalHint.Value.Value == "0" )
						{
							HasUnlimitedMaxRecursion = true;
						}
					}
				}

				base.ExplicitVisit( node );
			}

			/// <inheritdoc />
			public override void ExplicitVisit( OpenRowsetTableReference node )
			{
				HasOpenRowset = true;
				base.ExplicitVisit( node );
			}

			/// <inheritdoc />
			public override void ExplicitVisit( AdHocTableReference node )
			{
				HasOpenDatasource = true;
				base.ExplicitVisit( node );
			}

			/// <inheritdoc />
			public override void ExplicitVisit( BulkOpenRowset node )
			{
				HasOpenRowset = true;
				base.ExplicitVisit( node );
			}

			/// <inheritdoc />
			public override void ExplicitVisit( OpenQueryTableReference node )
			{
				HasOpenQuery = true;
				base.ExplicitVisit( node );
			}
		}
	}
}

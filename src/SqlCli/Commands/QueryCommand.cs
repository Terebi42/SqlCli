using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using SqlCli.Auth;
using SqlCli.Config;
using SqlCli.Execution;
using SqlCli.Filtering;
using SqlCli.Output;

namespace SqlCli.Commands
{
	/// <summary>
	/// Orchestrates query execution including filtering, connection, and output formatting.
	/// </summary>
	public class QueryCommand
	{
		/// <summary>
		/// Maximum allowed byte length for inline --query input (1 KB).
		/// </summary>
		private const int MaxQueryBytes = 1024;

		/// <summary>
		/// Executes the query command with security config, operational config, app config, and auth mode.
		/// </summary>
		/// <param name="security">Immutable security configuration.</param>
		/// <param name="ops">Mutable operational configuration (already fully layered).</param>
		/// <param name="app">Mutable app configuration (already fully layered).</param>
		/// <param name="auth">Resolved authentication mode.</param>
		/// <param name="query">Inline SQL query text.</param>
		/// <param name="file">Path to a SQL script file.</param>
		/// <returns>Process exit code.</returns>
		public static int Execute(
			SecurityConfig security, OperationalConfig ops, AppConfig app, AuthMode auth,
			string query, string file )
		{
			var errors = new List<object>();

			// Validate format
			var formatLower = ops.Format?.ToLowerInvariant();
			if ( formatLower is not ( "csv" or "json" or "table" ) )
			{
				errors.Add( new { error = $"Unknown format: {ops.Format}. Valid values are csv, json, table.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Check inline query size limit (hard limit, not configurable)
			if ( query is not null )
			{
				var queryByteCount = Encoding.UTF8.GetByteCount( query );
				if ( queryByteCount > MaxQueryBytes )
				{
					errors.Add( new { error = $"Query exceeds maximum length ({FormatSize( queryByteCount )} > {FormatSize( MaxQueryBytes )}). Use --file for larger queries.", code = (int)ExitCode.InvalidArgs } );
					WriteErrors( errors );
					return (int)ExitCode.InvalidArgs;
				}
			}

			// Validate server and database
			if ( string.IsNullOrEmpty( app.Server ) )
			{
				errors.Add( new { error = "--server is required.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			if ( string.IsNullOrEmpty( app.Database ) )
			{
				errors.Add( new { error = "--database is required.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Validate timeout ranges
			if ( ops.Timeout < 1 || ops.Timeout > 300 )
			{
				errors.Add( new { error = "--timeout must be between 1 and 300 seconds.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			if ( ops.ConnectTimeout < 1 || ops.ConnectTimeout > 300 )
			{
				errors.Add( new { error = "--connect-timeout must be between 1 and 300 seconds.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Validate max rows
			if ( ops.MaxRows < 1 || ops.MaxRows > 10000 )
			{
				errors.Add( new { error = "--max-rows must be between 1 and 10000.", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Validate max file size
			if ( ops.MaxFileSize > 10485760 )
			{
				errors.Add( new { error = "--max-file-size must not exceed 10485760 (10 MB).", code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Get SQL input
			string sql;
			try
			{
				sql = GetSqlInput( query, file, ops.MaxFileSize );
			}
			catch ( Exception ex ) when ( ex is ArgumentException or IOException )
			{
				errors.Add( new { error = ex.Message, code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}

			// Filter statements
			var auditLogPath = security.Audit.Enabled
				? Path.Combine( AppContext.BaseDirectory, security.Audit.Path )
				: null;
			var filter = new ScriptDomStatementFilter( security, auditLogPath );
			var filterResult = filter.Validate( sql );

			if ( !filterResult.Allowed )
			{
				foreach ( var v in filterResult.Violations )
				{
					errors.Add( new { error = v.Reason, code = (int)ExitCode.FilterBlock, blocked = ScriptDomStatementFilter.TruncateSql( v.Statement ) } );
				}

				WriteErrors( errors );
				return (int)ExitCode.FilterBlock;
			}

			// Use AST-parsed statements instead of StatementSplitter
			var statements = filterResult.Statements;
			if ( statements.Count == 0 )
			{
				Console.WriteLine( string.Empty );
				return (int)ExitCode.Success;
			}

			try
			{
				var connectionString = ConnectionStringBuilder.Build( auth, app, ops );
				var result = QueryExecutor.Execute( connectionString, statements, ops.Timeout, auth, ops.MaxRows );

				IResultFormatter formatter = formatLower switch
				{
					"csv" => new CsvFormatter(),
					"json" => new JsonFormatter(),
					"table" => new TableFormatter(),
					_ => throw new ArgumentException( $"Unknown format: {ops.Format}" )
				};

				var output = formatter.Format( result );
				Console.Write( output );
			}
			catch ( OutputFormatException ex )
			{
				errors.Add( new { error = ex.Message, code = (int)ExitCode.InvalidArgs } );
				WriteErrors( errors );
				return (int)ExitCode.InvalidArgs;
			}
			catch ( MaxRowsExceededException ex )
			{
				errors.Add( new { error = ex.Message, code = (int)ExitCode.SqlError } );
				WriteErrors( errors );
				return (int)ExitCode.SqlError;
			}
			catch ( AuthException ex )
			{
				errors.Add( new { error = ex.Message, code = (int)ExitCode.AuthError } );
				WriteErrors( errors );
				return (int)ExitCode.AuthError;
			}
			catch ( Microsoft.Data.SqlClient.SqlException ex ) when ( ex.Number == -2 )
			{
				errors.Add( new { error = "Query timed out.", code = (int)ExitCode.TimeoutError } );
				WriteErrors( errors );
				return (int)ExitCode.TimeoutError;
			}
			catch ( Microsoft.Data.SqlClient.SqlException ex ) when ( ex.Number == 53 || ex.Number == -1 )
			{
				errors.Add( new { error = "Connection timed out.", code = (int)ExitCode.TimeoutError } );
				WriteErrors( errors );
				return (int)ExitCode.TimeoutError;
			}
			catch ( Microsoft.Data.SqlClient.SqlException ex )
			{
				errors.Add( new { error = ex.Message, code = (int)ExitCode.SqlError, number = ex.Number } );
				WriteErrors( errors );
				return (int)ExitCode.SqlError;
			}

			return (int)ExitCode.Success;
		}

		/// <summary>
		/// Resolves SQL input from either inline query or file path.
		/// </summary>
		/// <param name="query">Inline SQL query text.</param>
		/// <param name="file">Path to SQL script file.</param>
		/// <param name="maxFileSizeBytes">Maximum allowed file size in bytes.</param>
		/// <returns>SQL text to execute.</returns>
		private static string GetSqlInput( string query, string file, long maxFileSizeBytes )
		{
			if ( query is not null && file is not null )
			{
				throw new ArgumentException( "Specify either --query or --file, not both." );
			}

			if ( query is null && file is null )
			{
				throw new ArgumentException( "Either --query or --file is required." );
			}

			if ( file is not null )
			{
				if ( !File.Exists( file ) )
				{
					throw new FileNotFoundException( $"SQL file not found: {file}" );
				}

				var fileInfo = new FileInfo( file );
				if ( fileInfo.Length > maxFileSizeBytes )
				{
					throw new ArgumentException( $"SQL file exceeds maximum size ({FormatSize( fileInfo.Length )} > {FormatSize( maxFileSizeBytes )}). Increase with --max-file-size or adjust maxFileSize in config." );
				}

				return File.ReadAllText( file );
			}

			return query;
		}

		/// <summary>
		/// Formats a byte size as a human-readable string.
		/// Values under 1024 bytes are shown as bytes; larger values as KB with one decimal.
		/// </summary>
		/// <param name="bytes">Size in bytes.</param>
		/// <returns>Formatted size string.</returns>
		internal static string FormatSize( long bytes )
		{
			if ( bytes < 1024 )
			{
				return $"{bytes} bytes";
			}

			var kb = bytes / 1024.0;
			return $"{kb:F1} KB";
		}

		/// <summary>
		/// Writes error objects to stderr as a JSON array.
		/// </summary>
		/// <param name="errors">List of error objects to serialize.</param>
		private static void WriteErrors( List<object> errors )
		{
			var json = JsonSerializer.Serialize( errors, new JsonSerializerOptions { WriteIndented = false } );
			Console.Error.WriteLine( json );
		}
	}
}

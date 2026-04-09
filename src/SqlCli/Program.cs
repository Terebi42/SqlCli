using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SqlCli.Auth;
using SqlCli.Commands;
using SqlCli.Config;

namespace SqlCli
{
	/// <summary>
	/// Entry point for the SQL CLI tool.
	/// </summary>
	public static class Program
	{
		/// <summary>
		/// Application entry point that configures and runs the command-line interface.
		/// </summary>
		/// <param name="args">Command-line arguments.</param>
		/// <returns>Process exit code.</returns>
		public static async Task<int> Main( string[] args )
		{
			var serverOption = new Option<string>( "--server" ) { Description = "SQL Server hostname" };
			var databaseOption = new Option<string>( "--database" ) { Description = "Database name" };
			var domainOption = new Option<string>( "--domain" ) { Description = "Windows domain for impersonation (env: SQLCLI_DOMAIN)" };
			var userOption = new Option<string>( "--user" ) { Description = "Username for impersonation (env: SQLCLI_USER)" };
			var passwordStdinOption = new Option<bool>( "--password-stdin" ) { Description = "Read password from stdin (recommended)" };
			var windowsAuthOption = new Option<bool>( "--windows-auth" ) { Description = "Use current Windows identity" };
			var sqlUserOption = new Option<string>( "--sql-user" ) { Description = "SQL Server login" };
			var sqlPasswordOption = new Option<string>( "--sql-password" ) { Description = "SQL Server password [WARNING: visible in process list]" };
			var queryOption = new Option<string>( "--query" ) { Description = "SQL query or batch to execute" };
			var fileOption = new Option<string>( "--file" ) { Description = "Path to SQL script file" };
			var formatOption = new Option<string>( "--format" ) { Description = "Output format: csv, json, table" };
			var timeoutOption = new Option<int?>( "--timeout" ) { Description = "Query timeout in seconds" };
			var connectTimeoutOption = new Option<int?>( "--connect-timeout" ) { Description = "Connection timeout in seconds" };
			var maxRowsOption = new Option<int?>( "--max-rows" ) { Description = "Maximum number of rows to return per result set" };
			var maxFileSizeOption = new Option<long?>( "--max-file-size" ) { Description = "Maximum SQL file size in bytes (default: 51200 from config)" };
			var trustCertOption = new Option<bool>( "--trust-server-certificate" ) { Description = "Trust the server certificate without validation" };
			var noEncryptOption = new Option<bool>( "--no-encrypt" ) { Description = "Disable connection encryption (for legacy servers)" };
			var sspiPackageOption = new Option<string>( "--sspi-package" ) { Description = "SSPI package for domain auth: NTLM, Negotiate, Kerberos (default: NTLM)" };
			var errorCodesOption = new Option<bool>( "--error-codes" ) { Description = "Show exit code reference and exit" };
			var agentHelpOption = new Option<bool>( "--agent-help" ) { Description = "Show agent bootstrapping guide (CLAUDE.md format)" };
			var generateConfigOption = new Option<bool>( "--generate-config" ) { Description = "Generate a default sqlcli.config.jsonc in the executable directory" };
			var generateSplitConfigOption = new Option<bool>( "--generate-split-config" ) { Description = "Generate separate sqlcli.security.example.jsonc, sqlcli.config.example.jsonc, and sqlcli.app.example.jsonc files" };

			var rootCommand = new RootCommand( "SQL Server CLI with configurable query filtering.\n\nExamples:\n  SqlCli --server myhost --database mydb --windows-auth --query \"SELECT TOP 10 * FROM Orders\"\n  echo secret | SqlCli --server myhost --database mydb --domain CORP --user jdoe --password-stdin --query \"SELECT 1\"\n  SqlCli --server myhost --database mydb --sql-user sa --sql-password pass --query \"SELECT @@VERSION\" --format json\n  SqlCli --server myhost --database mydb --windows-auth --file \"C:\\scripts\\report.sql\" --format table\n  SqlCli --server myhost --database mydb --windows-auth --query \"SELECT 1; SELECT 2\" --format json\n  SqlCli --error-codes\n  SqlCli --agent-help\n  SqlCli --generate-config" )
			{
				serverOption, databaseOption,
				domainOption, userOption, passwordStdinOption,
				windowsAuthOption,
				sqlUserOption, sqlPasswordOption,
				sspiPackageOption,
				queryOption, fileOption,
				formatOption, timeoutOption, connectTimeoutOption,
				maxRowsOption, maxFileSizeOption,
				trustCertOption, noEncryptOption,
				errorCodesOption, agentHelpOption,
				generateConfigOption, generateSplitConfigOption
			};

			rootCommand.SetAction( ( ParseResult parseResult ) =>
			{
				// Special commands
				if ( parseResult.GetValue( agentHelpOption ) )
				{
					Console.WriteLine( AgentHelp.GetText() );
					return 0;
				}

				if ( parseResult.GetValue( errorCodesOption ) )
				{
					Console.WriteLine( ExitCodeExtensions.GetHelpText() );
					return 0;
				}

				if ( parseResult.GetValue( generateConfigOption ) )
				{
					try
					{
						ConfigLoader.GenerateConfig( AppContext.BaseDirectory, split: false, force: false );
						return 0;
					}
					catch ( ConfigException ex )
					{
						Console.Error.WriteLine( ex.Message );
						return (int)ExitCode.ConfigError;
					}
				}

				if ( parseResult.GetValue( generateSplitConfigOption ) )
				{
					try
					{
						ConfigLoader.GenerateConfig( AppContext.BaseDirectory, split: true, force: false );
						return 0;
					}
					catch ( ConfigException ex )
					{
						Console.Error.WriteLine( ex.Message );
						return (int)ExitCode.ConfigError;
					}
				}

				// Ensure config file exists (auto-generate if needed) — runs in both modes
				try
				{
					ConfigLoader.EnsureConfigExists( AppContext.BaseDirectory );
				}
				catch ( Exception ex ) when ( ex is IOException or UnauthorizedAccessException )
				{
					// Non-fatal — config generation is a convenience, not a requirement
					Console.Error.WriteLine( $"WARNING: Could not auto-generate config: {ex.Message}" );
				}

				// Load security config — file only, never overridable
				SecurityConfig security;
				try
				{
					security = ConfigLoader.LoadSecurity( AppContext.BaseDirectory );
				}
				catch ( ConfigException ex )
				{
					WriteError( ex.Message, ExitCode.ConfigError );
					return (int)ExitCode.ConfigError;
				}

				// Load operational config — file base layer
				OperationalConfig ops;
				try
				{
					ops = ConfigLoader.LoadOperational( AppContext.BaseDirectory, Environment.CurrentDirectory );
				}
				catch ( ConfigException ex )
				{
					WriteError( ex.Message, ExitCode.ConfigError );
					return (int)ExitCode.ConfigError;
				}

				// Load app config — file base layer
				AppConfig app;
				try
				{
					app = ConfigLoader.LoadApp( AppContext.BaseDirectory, Environment.CurrentDirectory );
				}
				catch ( ConfigException ex )
				{
					WriteError( ex.Message, ExitCode.ConfigError );
					return (int)ExitCode.ConfigError;
				}

				// CLI args override config (operational + app only)
				app.Server = parseResult.GetValue( serverOption ) ?? app.Server;
				app.Database = parseResult.GetValue( databaseOption ) ?? app.Database;
				ops.Timeout = parseResult.GetValue( timeoutOption ) ?? ops.Timeout;
				ops.ConnectTimeout = parseResult.GetValue( connectTimeoutOption ) ?? ops.ConnectTimeout;
				ops.MaxRows = parseResult.GetValue( maxRowsOption ) ?? ops.MaxRows;
				ops.MaxFileSize = parseResult.GetValue( maxFileSizeOption ) ?? ops.MaxFileSize;
				ops.Format = parseResult.GetValue( formatOption ) ?? ops.Format;
				app.TrustServerCertificate = parseResult.GetValue( trustCertOption ) || app.TrustServerCertificate;
				app.NoEncrypt = parseResult.GetValue( noEncryptOption ) || app.NoEncrypt;

				// Password handling
				var passwordValue = ReadPassword( parseResult, passwordStdinOption );
				if ( parseResult.GetValue( passwordStdinOption ) && passwordValue is null )
				{
					// Error already written to stderr by ReadPassword
					return (int)ExitCode.InvalidArgs;
				}

				if ( parseResult.GetValue( sqlPasswordOption ) is not null )
				{
					Console.Error.WriteLine( "WARNING: --sql-password is visible in the process list. Consider using environment variables for credentials." );
				}

				// Auth resolution — from CLI + env vars, not config
				AuthMode auth;
				try
				{
					auth = AuthMode.Resolve(
						parseResult.GetValue( domainOption ),
						parseResult.GetValue( userOption ),
						passwordValue,
						parseResult.GetValue( windowsAuthOption ),
						parseResult.GetValue( sqlUserOption ),
						parseResult.GetValue( sqlPasswordOption ),
						parseResult.GetValue( sspiPackageOption ) );
				}
				catch ( AuthException ex )
				{
					WriteError( ex.Message, ExitCode.InvalidArgs );
					return (int)ExitCode.InvalidArgs;
				}

				return QueryCommand.Execute( security, ops, app, auth,
					parseResult.GetValue( queryOption ),
					parseResult.GetValue( fileOption ) );
			} );

			return await rootCommand.Parse( args ).InvokeAsync();
		}

		/// <summary>
		/// Reads password from stdin if --password-stdin is specified.
		/// </summary>
		private static string ReadPassword( ParseResult parseResult, Option<bool> passwordStdinOption )
		{
			var usePasswordStdin = parseResult.GetValue( passwordStdinOption );
			if ( !usePasswordStdin )
			{
				return null;
			}

			var stdinPassword = Console.ReadLine();
			if ( string.IsNullOrEmpty( stdinPassword ) )
			{
				Console.Error.WriteLine( "[{\"error\":\"--password-stdin specified but no input received on stdin.\",\"code\":4}]" );
				return null;
			}

			return stdinPassword;
		}

		/// <summary>
		/// Writes a single error to stderr as a JSON array.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="code">Exit code.</param>
		private static void WriteError( string message, ExitCode code )
		{
			var errors = new[] { new { error = message, code = (int)code } };
			var json = JsonSerializer.Serialize( errors, new JsonSerializerOptions { WriteIndented = false } );
			Console.Error.WriteLine( json );
		}
	}
}

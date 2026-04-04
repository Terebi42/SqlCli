using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SqlCli.Config
{
	/// <summary>
	/// Loads and parses configuration files, extracting security, operational, and app configs.
	/// <para>
	/// Supports two file layouts:
	/// <list type="bullet">
	///   <item>Combined: a single <c>sqlcli.config.jsonc</c> containing all sections.</item>
	///   <item>Split: <c>sqlcli.security.jsonc</c> for security settings (takes precedence),
	///   <c>sqlcli.config.jsonc</c> for operational settings, and
	///   <c>sqlcli.app.jsonc</c> for app/environment settings (working directory).</item>
	/// </list>
	/// </para>
	/// <para>
	/// Security settings are immutable and file-only. Operational and app settings can be overridden
	/// by environment variables and CLI arguments (layered in Program.cs).
	/// </para>
	/// </summary>
	public static class ConfigLoader
	{
		/// <summary>
		/// Combined config file name.
		/// </summary>
		internal const string ConfigFileName = "sqlcli.config.jsonc";

		/// <summary>
		/// Security-only config file name (used in split layout).
		/// </summary>
		internal const string SecurityFileName = "sqlcli.security.jsonc";

		/// <summary>
		/// App-only config file name (used in split layout, typically in working directory).
		/// </summary>
		internal const string AppFileName = "sqlcli.app.jsonc";

		private static readonly JsonSerializerOptions ReadOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		/// <summary>
		/// Loads the immutable security configuration from the executable directory.
		/// Checks for a dedicated security file first, then falls back to the combined config.
		/// If neither exists, generates a default combined config with <c>["SelectStatement"]</c>.
		/// </summary>
		/// <param name="exeDir">Directory containing the executable and config files.</param>
		/// <returns>Immutable security configuration.</returns>
		public static SecurityConfig LoadSecurity( string exeDir )
		{
			var securityPath = Path.Combine( exeDir, SecurityFileName );
			var configPath = Path.Combine( exeDir, ConfigFileName );

			// Try dedicated security file first (takes precedence)
			if ( File.Exists( securityPath ) )
			{
				var config = DeserializeFile( securityPath );
				ValidateFilterMode( config.Security.FilterMode );
				return config.Security;
			}

			// Fall back to combined config file
			if ( File.Exists( configPath ) )
			{
				var config = DeserializeFile( configPath );
				ValidateFilterMode( config.Security.FilterMode );
				return config.Security;
			}

			// Neither exists — generate default combined config
			var defaultConfig = CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( defaultConfig );
			File.WriteAllText( configPath, jsonc );
			Console.Error.WriteLine( $"Config file not found. Created default config at: {configPath}" );
			return defaultConfig.Security;
		}

		/// <summary>
		/// Loads the mutable operational configuration, optionally layering a working-directory
		/// config on top of the executable-directory config.
		/// </summary>
		/// <param name="exeDir">Directory containing the executable and config files.</param>
		/// <param name="workingDir">Current working directory (may contain an override config).</param>
		/// <returns>Mutable operational configuration.</returns>
		public static OperationalConfig LoadOperational( string exeDir, string workingDir )
		{
			var configPath = Path.Combine( exeDir, ConfigFileName );

			// Base layer: exe-dir config file
			OperationalConfig ops;
			if ( File.Exists( configPath ) )
			{
				var config = DeserializeFile( configPath );
				ops = config.Operational;
			}
			else
			{
				ops = new OperationalConfig();
			}

			// Override layer: working-dir config file (if different from exe-dir)
			if ( !string.IsNullOrEmpty( workingDir ) )
			{
				var normalizedExeDir = Path.GetFullPath( exeDir ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
				var normalizedWorkDir = Path.GetFullPath( workingDir ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );

				if ( !string.Equals( normalizedExeDir, normalizedWorkDir, StringComparison.OrdinalIgnoreCase ) )
				{
					var workingConfigPath = Path.Combine( workingDir, ConfigFileName );
					if ( File.Exists( workingConfigPath ) )
					{
						using var doc = ParseJsonFile( workingConfigPath );
						if ( doc.RootElement.TryGetProperty( "operational", out var section ) )
						{
							LayerFromJson( section, ops );
						}
					}
				}
			}

			return ops;
		}

		/// <summary>
		/// Loads the mutable app configuration, optionally layering working-directory
		/// configs on top of the executable-directory config.
		/// </summary>
		/// <param name="exeDir">Directory containing the executable and config files.</param>
		/// <param name="workingDir">Current working directory (may contain an override config).</param>
		/// <returns>Mutable app configuration.</returns>
		public static AppConfig LoadApp( string exeDir, string workingDir )
		{
			var configPath = Path.Combine( exeDir, ConfigFileName );

			// Base layer: exe-dir config file
			AppConfig app;
			if ( File.Exists( configPath ) )
			{
				var config = DeserializeFile( configPath );
				app = config.App;
			}
			else
			{
				app = new AppConfig();
			}

			// Override layer: working-dir configs (if different from exe-dir)
			if ( !string.IsNullOrEmpty( workingDir ) )
			{
				var normalizedExeDir = Path.GetFullPath( exeDir ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
				var normalizedWorkDir = Path.GetFullPath( workingDir ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );

				if ( !string.Equals( normalizedExeDir, normalizedWorkDir, StringComparison.OrdinalIgnoreCase ) )
				{
					// Check for dedicated app config file
					var appConfigPath = Path.Combine( workingDir, AppFileName );
					if ( File.Exists( appConfigPath ) )
					{
						using var appDoc = ParseJsonFile( appConfigPath );
						if ( appDoc.RootElement.TryGetProperty( "app", out var appSection ) )
						{
							LayerFromJson( appSection, app );
						}
					}

					// Check for combined config in working dir
					var workingConfigPath = Path.Combine( workingDir, ConfigFileName );
					if ( File.Exists( workingConfigPath ) )
					{
						using var doc = ParseJsonFile( workingConfigPath );
						if ( doc.RootElement.TryGetProperty( "app", out var appSection ) )
						{
							LayerFromJson( appSection, app );
						}
					}
				}
			}

			return app;
		}

		/// <summary>
		/// Generates config file(s) in the specified directory.
		/// </summary>
		/// <param name="directory">Target directory for the config file(s).</param>
		/// <param name="split">If true, generates separate security, operational, and app files.</param>
		/// <param name="force">If true, overwrites existing files.</param>
		public static void GenerateConfig( string directory, bool split, bool force )
		{
			var defaultConfig = CreateDefaultConfig();

			if ( split )
			{
				var securityPath = Path.Combine( directory, "sqlcli.security.example.jsonc" );
				var configPath = Path.Combine( directory, "sqlcli.config.example.jsonc" );
				var appPath = Path.Combine( directory, "sqlcli.app.example.jsonc" );

				if ( !force && File.Exists( securityPath ) )
				{
					throw new ConfigException( $"Security config already exists: {securityPath}. Use --force to overwrite." );
				}

				if ( !force && File.Exists( configPath ) )
				{
					throw new ConfigException( $"Config file already exists: {configPath}. Use --force to overwrite." );
				}

				if ( !force && File.Exists( appPath ) )
				{
					throw new ConfigException( $"App config already exists: {appPath}. Use --force to overwrite." );
				}

				var securityJsonc = JsoncGenerator.Generate( defaultConfig, securityOnly: true );
				var operationalJsonc = JsoncGenerator.Generate( defaultConfig, operationalOnly: true );
				var appJsonc = JsoncGenerator.Generate( defaultConfig, appOnly: true );

				File.WriteAllText( securityPath, securityJsonc );
				File.WriteAllText( configPath, operationalJsonc );
				File.WriteAllText( appPath, appJsonc );

				Console.Error.WriteLine( $"Generated security config: {securityPath}" );
				Console.Error.WriteLine( $"Generated operational config: {configPath}" );
				Console.Error.WriteLine( $"Generated app config: {appPath}" );
			}
			else
			{
				var configPath = Path.Combine( directory, ConfigFileName );

				if ( !force && File.Exists( configPath ) )
				{
					throw new ConfigException( $"Config file already exists: {configPath}. Use --force to overwrite." );
				}

				var jsonc = JsoncGenerator.Generate( defaultConfig );
				File.WriteAllText( configPath, jsonc );

				Console.Error.WriteLine( $"Generated config: {configPath}" );
			}
		}

		/// <summary>
		/// Creates a default config with <c>["SelectStatement"]</c> as the allowed statements.
		/// This intentionally differs from the class default of empty list — a new user should
		/// have a working SELECT-only config out of the box.
		/// </summary>
		/// <returns>Default configuration instance.</returns>
		internal static SqlCliConfig CreateDefaultConfig()
		{
			return new SqlCliConfig
			{
				Security = new SecurityConfig
				{
					AllowedStatements = new List<string> { "SelectStatement" }
				}
			};
		}

		/// <summary>
		/// Deserializes a JSONC config file into <see cref="SqlCliConfig"/>.
		/// </summary>
		private static SqlCliConfig DeserializeFile( string path )
		{
			try
			{
				var json = File.ReadAllText( path );
				var config = JsonSerializer.Deserialize<SqlCliConfig>( json, ReadOptions )
					?? throw new ConfigException( $"Configuration deserialized to null: {path}" );
				return config;
			}
			catch ( JsonException ex )
			{
				throw new ConfigException( $"Invalid configuration in {path}: {ex.Message}", ex );
			}
		}

		/// <summary>
		/// Validates the filter mode value.
		/// </summary>
		private static void ValidateFilterMode( string filterMode )
		{
			if ( !string.Equals( filterMode, "whitelist", StringComparison.OrdinalIgnoreCase ) )
			{
				throw new ConfigException( $"Unsupported filterMode: '{filterMode}'. Only 'whitelist' is currently supported." );
			}
		}

		/// <summary>
		/// Layers explicitly present operational config properties from a JSON element
		/// onto the base config, leaving unspecified properties untouched.
		/// </summary>
		/// <param name="section">The "operational" JSON element from the override file.</param>
		/// <param name="target">Base operational config to modify.</param>
		private static void LayerFromJson( JsonElement section, OperationalConfig target )
		{
			if ( section.TryGetProperty( "timeout", out var t ) )
			{
				target.Timeout = t.GetInt32();
			}

			if ( section.TryGetProperty( "connectTimeout", out var ct ) )
			{
				target.ConnectTimeout = ct.GetInt32();
			}

			if ( section.TryGetProperty( "maxRows", out var mr ) )
			{
				target.MaxRows = mr.GetInt32();
			}

			if ( section.TryGetProperty( "maxFileSize", out var mf ) )
			{
				target.MaxFileSize = mf.GetInt64();
			}

			if ( section.TryGetProperty( "format", out var f ) )
			{
				target.Format = f.GetString();
			}
		}

		/// <summary>
		/// Layers explicitly present app config properties from a JSON element
		/// onto the base config, leaving unspecified properties untouched.
		/// </summary>
		/// <param name="section">The "app" JSON element from the override file.</param>
		/// <param name="target">Base app config to modify.</param>
		private static void LayerFromJson( JsonElement section, AppConfig target )
		{
			if ( section.TryGetProperty( "server", out var s ) && s.ValueKind != JsonValueKind.Null )
			{
				target.Server = s.GetString();
			}

			if ( section.TryGetProperty( "database", out var d ) && d.ValueKind != JsonValueKind.Null )
			{
				target.Database = d.GetString();
			}

			if ( section.TryGetProperty( "trustServerCertificate", out var tc ) )
			{
				target.TrustServerCertificate = tc.GetBoolean();
			}

			if ( section.TryGetProperty( "noEncrypt", out var ne ) )
			{
				target.NoEncrypt = ne.GetBoolean();
			}
		}

		/// <summary>
		/// Reads a JSONC file and returns the parsed <see cref="JsonDocument"/>.
		/// </summary>
		/// <param name="path">Path to the JSONC file.</param>
		/// <returns>Parsed JSON document.</returns>
		private static JsonDocument ParseJsonFile( string path )
		{
			var json = File.ReadAllText( path );
			return JsonDocument.Parse( json, new JsonDocumentOptions
			{
				CommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			} );
		}
	}

	/// <summary>
	/// Represents an error in loading or parsing the configuration file.
	/// </summary>
	public class ConfigException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigException"/> class.
		/// </summary>
		/// <param name="message">Error message.</param>
		public ConfigException( string message ) : base( message ) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigException"/> class with an inner exception.
		/// </summary>
		/// <param name="message">Error message.</param>
		/// <param name="inner">Inner exception.</param>
		public ConfigException( string message, Exception inner ) : base( message, inner ) { }
	}
}

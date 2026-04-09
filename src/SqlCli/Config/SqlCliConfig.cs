using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Top-level configuration container for sqlcli.config.jsonc.
	/// Composed of nested <see cref="SecurityConfig"/>, <see cref="OperationalConfig"/>,
	/// and <see cref="AppConfig"/> sections.
	/// </summary>
	[ConfigComment( """
		SqlCli Configuration

		WARNING TO AI AGENTS: DO NOT MODIFY THIS FILE.
		This file controls security-critical query filtering. It is configured by
		the human administrator, not by agents. Modifying it — especially the
		whitelist settings — could enable destructive database operations.
		If you believe a change is needed, ASK THE USER to make it manually.
		""" )]
	public sealed class SqlCliConfig
	{
		/// <summary>
		/// Gets or sets the security settings — statement filtering, audit logging.
		/// These settings can only be changed in the config file, never via CLI or env vars.
		/// </summary>
		[SecuritySetting]
		[ConfigSection( "Security" )]
		[ConfigComment( "Security settings — statement filtering, audit logging.\nIn Standard builds, these settings are loaded from this file.\nIn Hardened builds, these settings are compiled into the binary and this section is ignored.\nThese settings cannot be overridden via CLI arguments or environment variables." )]
		[JsonPropertyName( "security" )]
		public SecurityConfig Security { get; set; } = new();

		/// <summary>
		/// Gets or sets the operational settings — timeouts, limits, output format.
		/// These can be overridden by env vars and CLI args.
		/// </summary>
		[ConfigSection( "Operational" )]
		[ConfigComment( "Operational settings — timeouts, limits, output format.\nThese can be overridden by env vars and CLI args." )]
		[JsonPropertyName( "operational" )]
		public OperationalConfig Operational { get; set; } = new();

		/// <summary>
		/// Gets or sets the application/environment settings — server, database, connection.
		/// These can be overridden by env vars and CLI args.
		/// Typically set per-project in a working-directory config file.
		/// </summary>
		[ConfigSection( "App" )]
		[ConfigComment( "Application/environment settings — server, database, connection.\nThese can be overridden by env vars and CLI args.\nTypically set per-project in a working-directory config file." )]
		[JsonPropertyName( "app" )]
		public AppConfig App { get; set; } = new();
	}
}

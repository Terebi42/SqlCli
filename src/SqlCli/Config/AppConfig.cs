using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Application/environment configuration for server, database, and connection settings.
	/// These settings can be overridden by environment variables and CLI arguments.
	/// Typically set per-project in a working-directory config file.
	/// </summary>
	public sealed class AppConfig
	{
		/// <summary>
		/// Gets or sets the SQL Server hostname.
		/// </summary>
		[ConfigComment( "SQL Server hostname." )]
		[JsonPropertyName( "server" )]
		public string Server { get; set; }

		/// <summary>
		/// Gets or sets the database name.
		/// </summary>
		[ConfigComment( "Database name." )]
		[JsonPropertyName( "database" )]
		public string Database { get; set; }

		/// <summary>
		/// Gets or sets whether to trust the server certificate without validation.
		/// </summary>
		[ConfigComment( "Trust the server certificate without validation." )]
		[JsonPropertyName( "trustServerCertificate" )]
		public bool TrustServerCertificate { get; set; }

		/// <summary>
		/// Gets or sets whether to disable connection encryption (for legacy servers).
		/// </summary>
		[ConfigComment( "Disable connection encryption (for legacy servers)." )]
		[JsonPropertyName( "noEncrypt" )]
		public bool NoEncrypt { get; set; }
	}
}

using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Mutable operational configuration that can be layered from config file,
	/// environment variables, and CLI arguments.
	/// </summary>
	public sealed class OperationalConfig
	{
		/// <summary>
		/// Gets or sets the query (command) timeout in seconds.
		/// </summary>
		[ConfigComment( "Query (command) timeout in seconds. CLI: --timeout (range: 1-300)" )]
		[JsonPropertyName( "timeout" )]
		public int Timeout { get; set; } = 30;

		/// <summary>
		/// Gets or sets the connection timeout in seconds.
		/// </summary>
		[ConfigComment( "Connection timeout in seconds. CLI: --connect-timeout (range: 1-300)" )]
		[JsonPropertyName( "connectTimeout" )]
		public int ConnectTimeout { get; set; } = 15;

		/// <summary>
		/// Gets or sets the maximum rows returned per result set.
		/// </summary>
		[ConfigComment( "Maximum rows returned per result set. CLI: --max-rows (minimum: 1)" )]
		[JsonPropertyName( "maxRows" )]
		public int MaxRows { get; set; } = 100;

		/// <summary>
		/// Gets or sets the maximum SQL file size in bytes for --file input.
		/// </summary>
		[ConfigComment( "Maximum SQL file size in bytes for --file input. CLI: --max-file-size\nDefault: 51200 (50 KB). --query has a hard limit of 1024 bytes." )]
		[JsonPropertyName( "maxFileSize" )]
		public long MaxFileSize { get; set; } = 51200;

		/// <summary>
		/// Gets or sets the default output format (csv, json, or table).
		/// </summary>
		[ConfigComment( "Default output format: csv, json, or table." )]
		[JsonPropertyName( "format" )]
		public string Format { get; set; } = "csv";
	}
}

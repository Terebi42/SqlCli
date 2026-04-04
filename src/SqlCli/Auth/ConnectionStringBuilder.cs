using Microsoft.Data.SqlClient;
using SqlCli.Config;

namespace SqlCli.Auth
{
	/// <summary>
	/// Builds SQL Server connection strings based on the resolved authentication mode,
	/// app configuration, and operational configuration.
	/// </summary>
	public static class ConnectionStringBuilder
	{
		/// <summary>
		/// Builds a connection string for the given authentication mode, app config, and operational config.
		/// </summary>
		/// <param name="mode">Resolved authentication mode.</param>
		/// <param name="app">App configuration with server and connection settings.</param>
		/// <param name="ops">Operational configuration with timeout settings.</param>
		/// <returns>Formatted connection string.</returns>
		public static string Build( AuthMode mode, AppConfig app, OperationalConfig ops )
		{
			var builder = new SqlConnectionStringBuilder
			{
				DataSource = app.Server,
				InitialCatalog = app.Database,
				TrustServerCertificate = app.TrustServerCertificate,
				ConnectTimeout = ops.ConnectTimeout,
				Encrypt = app.NoEncrypt ? SqlConnectionEncryptOption.Optional : SqlConnectionEncryptOption.Mandatory,
				ApplicationName = "SqlCli"
			};

			switch ( mode )
			{
				case AuthMode.DomainAuth:
				case AuthMode.WindowsAuth:
					builder.IntegratedSecurity = true;
					break;
				case AuthMode.SqlAuth sql:
					builder.UserID = sql.SqlUser;
					builder.Password = sql.SqlPassword;
					builder.IntegratedSecurity = false;
					break;
			}

			return builder.ConnectionString;
		}
	}
}

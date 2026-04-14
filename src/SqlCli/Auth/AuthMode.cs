using System;

namespace SqlCli.Auth
{
	/// <summary>
	/// Abstract base for authentication modes supported by the CLI.
	/// Authentication only — server/database are in <see cref="Config.AppConfig"/>.
	/// </summary>
	public abstract record AuthMode
	{
		/// <summary>
		/// Authenticates via SSPI with domain credentials (NTLM/Negotiate/Kerberos).
		/// </summary>
		/// <param name="Domain">Domain name (e.g. Active Directory domain).</param>
		/// <param name="User">Domain username.</param>
		/// <param name="Password">Domain password.</param>
		/// <param name="SspiPackage">SSPI package to use (NTLM, Negotiate, or Kerberos). Defaults to NTLM.</param>
		public record DomainAuth( string Domain, string User, string Password, string SspiPackage = "NTLM" ) : AuthMode;

		/// <summary>
		/// Authenticates using the current process identity (integrated security).
		/// On Windows, uses the current Windows identity. On Linux, uses the current Kerberos ticket.
		/// </summary>
		public record IntegratedAuth : AuthMode;

		/// <summary>
		/// Authenticates using SQL Server credentials.
		/// </summary>
		/// <param name="SqlUser">SQL Server login name.</param>
		/// <param name="SqlPassword">SQL Server login password.</param>
		public record SqlAuth( string SqlUser, string SqlPassword ) : AuthMode;

		/// <summary>
		/// Resolves the authentication mode from CLI arguments and environment variables.
		/// Domain credentials (domain, user, password) fall back to SQLCLI_DOMAIN, SQLCLI_USER,
		/// SQLCLI_PASSWORD environment variables. All other parameters are CLI-only.
		/// </summary>
		public static AuthMode Resolve(
			string domain, string user, string password,
			bool integratedAuth,
			string sqlUser, string sqlPassword,
			string sspiPackage = null )
		{
			return ResolveCore(
				domain ?? Environment.GetEnvironmentVariable( "SQLCLI_DOMAIN" ),
				user ?? Environment.GetEnvironmentVariable( "SQLCLI_USER" ),
				password ?? Environment.GetEnvironmentVariable( "SQLCLI_PASSWORD" ),
				integratedAuth, sqlUser, sqlPassword, sspiPackage );
		}

		/// <summary>
		/// Core resolution logic without environment variable fallback.
		/// </summary>
		internal static AuthMode ResolveCore(
			string domain, string user, string password,
			bool integratedAuth,
			string sqlUser, string sqlPassword,
			string sspiPackage = null )
		{
			var hasDomainAuth = !string.IsNullOrEmpty( domain ) || !string.IsNullOrEmpty( user ) || !string.IsNullOrEmpty( password );
			var hasSqlAuth = !string.IsNullOrEmpty( sqlUser ) || !string.IsNullOrEmpty( sqlPassword );

			var modeCount = ( hasDomainAuth ? 1 : 0 ) + ( integratedAuth ? 1 : 0 ) + ( hasSqlAuth ? 1 : 0 );

			if ( modeCount == 0 )
			{
				throw new AuthException( "Exactly one auth mode required: --domain/--user/--password-stdin (or SQLCLI_PASSWORD env var), --integrated-auth, or --sql-user/--sql-password." );
			}

			if ( modeCount > 1 )
			{
				throw new AuthException( "Only one auth mode allowed. Do not combine domain auth, --integrated-auth, and --sql-user/--sql-password." );
			}

			if ( hasDomainAuth )
			{
				if ( string.IsNullOrEmpty( domain ) || string.IsNullOrEmpty( user ) || string.IsNullOrEmpty( password ) )
				{
					throw new AuthException( "Domain auth requires all three: --domain, --user, and password (via --password-stdin or SQLCLI_PASSWORD env var)." );
				}

				var effectivePackage = sspiPackage ?? "NTLM";
				if ( effectivePackage is not ( "NTLM" or "Negotiate" or "Kerberos" ) )
				{
					throw new AuthException( $"Invalid --sspi-package: {effectivePackage}. Valid values are NTLM, Negotiate, Kerberos." );
				}

				return new DomainAuth( domain, user, password, effectivePackage );
			}

			if ( integratedAuth )
			{
				return new IntegratedAuth();
			}

			if ( string.IsNullOrEmpty( sqlUser ) || string.IsNullOrEmpty( sqlPassword ) )
			{
				throw new AuthException( "SQL auth requires both --sql-user and --sql-password." );
			}

			return new SqlAuth( sqlUser, sqlPassword );
		}
	}

	/// <summary>
	/// Represents an authentication-related error.
	/// </summary>
	public class AuthException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AuthException"/> class.
		/// </summary>
		/// <param name="message">Error message.</param>
		public AuthException( string message ) : base( message ) { }
	}
}

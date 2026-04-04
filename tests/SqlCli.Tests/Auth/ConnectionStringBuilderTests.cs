using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Auth;
using SqlCli.Config;

namespace SqlCli.Tests.Auth
{
	/// <summary>
	/// Tests for <see cref="ConnectionStringBuilder.Build"/> connection string generation.
	/// </summary>
	[TestClass]
	public class ConnectionStringBuilderTests
	{
		/// <summary>
		/// Creates a default app config for testing.
		/// </summary>
		private static AppConfig CreateApp( string server = "srv", string database = "db" )
		{
			return new AppConfig
			{
				Server = server,
				Database = database
			};
		}

		/// <summary>
		/// Creates a default operational config for testing.
		/// </summary>
		private static OperationalConfig CreateOps()
		{
			return new OperationalConfig
			{
				ConnectTimeout = 15
			};
		}

		/// <summary>
		/// Verifies that DomainAuth mode produces integrated security in the connection string.
		/// </summary>
		[TestMethod]
		public void Build_DomainAuth_UsesIntegratedSecurity()
		{
			var mode = new AuthMode.DomainAuth( "CORP", "user", "pass" );
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Integrated Security=True" );
			StringAssert.Contains( cs, "Data Source=srv" );
			StringAssert.Contains( cs, "Initial Catalog=db" );
		}

		/// <summary>
		/// Verifies that WindowsAuth mode produces integrated security in the connection string.
		/// </summary>
		[TestMethod]
		public void Build_WindowsAuth_UsesIntegratedSecurity()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Integrated Security=True" );
		}

		/// <summary>
		/// Verifies that SqlAuth mode embeds SQL credentials in the connection string.
		/// </summary>
		[TestMethod]
		public void Build_SqlAuth_UsesCredentials()
		{
			var mode = new AuthMode.SqlAuth( "sa", "pass123" );
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "User ID=sa" );
			StringAssert.Contains( cs, "Password=pass123" );
		}

		/// <summary>
		/// Verifies that TrustServerCertificate defaults to false when not specified.
		/// </summary>
		[TestMethod]
		public void Build_DefaultTrustServerCertificate_IsFalse()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			Assert.IsFalse( cs.Contains( "Trust Server Certificate=True", System.StringComparison.OrdinalIgnoreCase ) );
		}

		/// <summary>
		/// Verifies that TrustServerCertificate is enabled when the flag is true.
		/// </summary>
		[TestMethod]
		public void Build_TrustServerCertificateTrue_IncludedInConnectionString()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			app.TrustServerCertificate = true;
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Trust Server Certificate=True" );
		}

		/// <summary>
		/// Verifies that ConnectTimeout is set in the connection string.
		/// </summary>
		[TestMethod]
		public void Build_ConnectTimeout_IncludedInConnectionString()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			ops.ConnectTimeout = 45;
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Connect Timeout=45" );
		}

		/// <summary>
		/// Verifies that default ConnectTimeout is 15 seconds.
		/// </summary>
		[TestMethod]
		public void Build_DefaultConnectTimeout_Is15()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Connect Timeout=15" );
		}

		/// <summary>
		/// Verifies that ApplicationName is set to SqlCli.
		/// </summary>
		[TestMethod]
		public void Build_ApplicationName_IsSqlCli()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Application Name=SqlCli" );
		}

		/// <summary>
		/// Verifies that NoEncrypt sets Encrypt to Optional.
		/// </summary>
		[TestMethod]
		public void Build_NoEncryptTrue_SetsEncryptFalse()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			app.NoEncrypt = true;
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Encrypt=False" );
		}

		/// <summary>
		/// Verifies that Encrypt defaults to True (Mandatory) when NoEncrypt is false.
		/// </summary>
		[TestMethod]
		public void Build_NoEncryptFalse_SetsEncryptTrue()
		{
			var mode = new AuthMode.WindowsAuth();
			var app = CreateApp();
			var ops = CreateOps();
			var cs = ConnectionStringBuilder.Build( mode, app, ops );
			StringAssert.Contains( cs, "Encrypt=True" );
		}
	}
}

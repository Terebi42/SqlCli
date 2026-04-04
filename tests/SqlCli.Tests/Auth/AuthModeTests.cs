using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Auth;

namespace SqlCli.Tests.Auth
{
	/// <summary>
	/// Tests for <see cref="AuthMode.Resolve"/> method and its validation logic.
	/// </summary>
	[TestClass]
	public class AuthModeTests
	{
		/// <summary>
		/// Verifies that providing domain, user, and password resolves to DomainAuth mode.
		/// </summary>
		[TestMethod]
		public void Resolve_DomainAuth_ReturnsDomainAuthMode()
		{
			var mode = AuthMode.ResolveCore( "CORP", "user", "pass", false, null, null );
			Assert.IsInstanceOfType<AuthMode.DomainAuth>( mode );
			var da = (AuthMode.DomainAuth)mode;
			Assert.AreEqual( "CORP", da.Domain );
			Assert.AreEqual( "user", da.User );
			Assert.AreEqual( "pass", da.Password );
			Assert.AreEqual( "NTLM", da.SspiPackage );
		}

		/// <summary>
		/// Verifies that sspiPackage parameter is passed through to DomainAuth.
		/// </summary>
		[TestMethod]
		public void Resolve_DomainAuth_WithSspiPackage_UsesSpecifiedPackage()
		{
			var mode = AuthMode.ResolveCore( "CORP", "user", "pass", false, null, null, "Negotiate" );
			var da = (AuthMode.DomainAuth)mode;
			Assert.AreEqual( "Negotiate", da.SspiPackage );
		}

		/// <summary>
		/// Verifies that an invalid sspiPackage throws.
		/// </summary>
		[TestMethod]
		public void Resolve_DomainAuth_InvalidSspiPackage_Throws()
		{
			Assert.ThrowsExactly<AuthException>( () =>
				AuthMode.ResolveCore( "CORP", "user", "pass", false, null, null, "BadPackage" ) );
		}

		/// <summary>
		/// Verifies that the windows-auth flag resolves to WindowsAuth mode.
		/// </summary>
		[TestMethod]
		public void Resolve_WindowsAuth_ReturnsWindowsAuthMode()
		{
			var mode = AuthMode.ResolveCore( null, null, null, true, null, null );
			Assert.IsInstanceOfType<AuthMode.WindowsAuth>( mode );
		}

		/// <summary>
		/// Verifies that providing sql-user and sql-password resolves to SqlAuth mode.
		/// </summary>
		[TestMethod]
		public void Resolve_SqlAuth_ReturnsSqlAuthMode()
		{
			var mode = AuthMode.ResolveCore( null, null, null, false, "sa", "pass" );
			Assert.IsInstanceOfType<AuthMode.SqlAuth>( mode );
		}

		/// <summary>
		/// Verifies that providing no auth parameters throws.
		/// </summary>
		[TestMethod]
		public void Resolve_NoAuthMode_Throws()
		{
			Assert.ThrowsExactly<AuthException>( () =>
				AuthMode.ResolveCore( null, null, null, false, null, null ) );
		}

		/// <summary>
		/// Verifies that providing two auth modes simultaneously throws.
		/// </summary>
		[TestMethod]
		public void Resolve_TwoAuthModes_Throws()
		{
			Assert.ThrowsExactly<AuthException>( () =>
				AuthMode.ResolveCore( "CORP", "user", "pass", true, null, null ) );
		}

		/// <summary>
		/// Verifies that domain auth with missing domain throws.
		/// </summary>
		[TestMethod]
		public void Resolve_DomainAuth_MissingDomain_Throws()
		{
			Assert.ThrowsExactly<AuthException>( () =>
				AuthMode.ResolveCore( null, "user", "pass", false, null, null ) );
		}

		/// <summary>
		/// Verifies that SQL auth with missing password throws.
		/// </summary>
		[TestMethod]
		public void Resolve_SqlAuth_MissingPassword_Throws()
		{
			Assert.ThrowsExactly<AuthException>( () =>
				AuthMode.ResolveCore( null, null, null, false, "sa", null ) );
		}
	}
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Auth;

namespace SqlCli.Tests.Auth
{
	/// <summary>
	/// Tests for <see cref="MultiSubnetDetector"/> host extraction and multi-IP detection guards.
	/// </summary>
	[TestClass]
	public class MultiSubnetDetectorTests
	{
		/// <summary>
		/// Verifies that a plain hostname is extracted unchanged.
		/// </summary>
		[TestMethod]
		public void ExtractTcpHost_PlainHost_ReturnsHost()
		{
			Assert.AreEqual( "sqllistp5904", MultiSubnetDetector.ExtractTcpHost( "sqllistp5904" ) );
		}

		/// <summary>
		/// Verifies that a tcp: protocol prefix is stripped.
		/// </summary>
		[TestMethod]
		public void ExtractTcpHost_TcpPrefix_StripsPrefix()
		{
			Assert.AreEqual( "myhost", MultiSubnetDetector.ExtractTcpHost( "tcp:myhost" ) );
		}

		/// <summary>
		/// Verifies that a named-instance suffix is dropped, leaving only the host.
		/// </summary>
		[TestMethod]
		public void ExtractTcpHost_NamedInstance_ReturnsHostOnly()
		{
			Assert.AreEqual( "myhost", MultiSubnetDetector.ExtractTcpHost( @"myhost\SQLEXPRESS" ) );
		}

		/// <summary>
		/// Verifies that a port suffix is dropped.
		/// </summary>
		[TestMethod]
		public void ExtractTcpHost_WithPort_ReturnsHostOnly()
		{
			Assert.AreEqual( "myhost", MultiSubnetDetector.ExtractTcpHost( "myhost,1433" ) );
		}

		/// <summary>
		/// Verifies that prefix, instance, and port are all stripped together.
		/// </summary>
		[TestMethod]
		public void ExtractTcpHost_PrefixInstanceAndPort_ReturnsHostOnly()
		{
			Assert.AreEqual( "myhost", MultiSubnetDetector.ExtractTcpHost( @"tcp:myhost\inst,1433" ) );
		}

		/// <summary>
		/// Verifies that local aliases return null (never treated as an AG listener).
		/// </summary>
		[TestMethod]
		[DataRow( "localhost" )]
		[DataRow( "LOCALHOST" )]
		[DataRow( "." )]
		[DataRow( "(local)" )]
		[DataRow( @"(localdb)\MSSQLLocalDB" )]
		public void ExtractTcpHost_LocalAliases_ReturnNull( string server )
		{
			Assert.IsNull( MultiSubnetDetector.ExtractTcpHost( server ) );
		}

		/// <summary>
		/// Verifies that non-TCP protocol prefixes (named pipes / shared memory) return null.
		/// </summary>
		[TestMethod]
		[DataRow( @"np:\\myhost\pipe\sql\query" )]
		[DataRow( "lpc:myhost" )]
		public void ExtractTcpHost_NonTcpProtocols_ReturnNull( string server )
		{
			Assert.IsNull( MultiSubnetDetector.ExtractTcpHost( server ) );
		}

		/// <summary>
		/// Verifies that null/empty/whitespace input returns null.
		/// </summary>
		[TestMethod]
		[DataRow( null )]
		[DataRow( "" )]
		[DataRow( "   " )]
		public void ExtractTcpHost_EmptyInput_ReturnsNull( string server )
		{
			Assert.IsNull( MultiSubnetDetector.ExtractTcpHost( server ) );
		}

		/// <summary>
		/// Verifies that empty/null input never triggers multi-IP detection.
		/// </summary>
		[TestMethod]
		[DataRow( null )]
		[DataRow( "" )]
		public void ResolvesToMultipleIps_EmptyInput_ReturnsFalse( string server )
		{
			Assert.IsFalse( MultiSubnetDetector.ResolvesToMultipleIps( server ) );
		}

		/// <summary>
		/// Verifies that an IP literal is never treated as multi-IP (resolves to itself, no DNS lookup).
		/// </summary>
		[TestMethod]
		[DataRow( "10.0.0.1" )]
		[DataRow( "127.0.0.1" )]
		[DataRow( "::1" )]
		public void ResolvesToMultipleIps_IpLiteral_ReturnsFalse( string server )
		{
			Assert.IsFalse( MultiSubnetDetector.ResolvesToMultipleIps( server ) );
		}

		/// <summary>
		/// Verifies that local aliases never trigger multi-IP detection (no DNS lookup).
		/// </summary>
		[TestMethod]
		[DataRow( "localhost" )]
		[DataRow( "." )]
		public void ResolvesToMultipleIps_LocalAlias_ReturnsFalse( string server )
		{
			Assert.IsFalse( MultiSubnetDetector.ResolvesToMultipleIps( server ) );
		}
	}
}

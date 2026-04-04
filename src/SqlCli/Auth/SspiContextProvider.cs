using System;
using System.Buffers;
using System.Net;
using System.Net.Security;
using Microsoft.Data.SqlClient;

namespace SqlCli.Auth
{
	/// <summary>
	/// Custom SSPI context provider that authenticates using explicit domain credentials
	/// with a configurable SSPI package (NTLM, Negotiate, or Kerberos).
	/// </summary>
	/// <remarks>
	/// This replaces the LogonUser P/Invoke approach which caused SSPI negotiation timeouts
	/// in Microsoft.Data.SqlClient on .NET 10 when Kerberos SPN resolution fails.
	/// </remarks>
	public sealed class DomainSspiContextProvider : SspiContextProvider, IDisposable
	{
		private readonly NetworkCredential _credential;
		private readonly string _package;
		private NegotiateAuthentication _auth;

		/// <summary>
		/// Initializes a new instance of the <see cref="DomainSspiContextProvider"/> class.
		/// </summary>
		/// <param name="domain">Windows domain name.</param>
		/// <param name="user">Domain username.</param>
		/// <param name="password">Domain password.</param>
		/// <param name="package">SSPI package: NTLM, Negotiate, or Kerberos.</param>
		public DomainSspiContextProvider( string domain, string user, string password, string package = "NTLM" )
		{
			_credential = new NetworkCredential( user, password, domain );
			_package = package;
		}

		/// <summary>
		/// Generates the next SSPI authentication token for the SQL Server handshake.
		/// </summary>
		/// <param name="incomingBlob">Server challenge bytes (empty on first call).</param>
		/// <param name="outgoingBlobWriter">Writer for the response token.</param>
		/// <param name="authParams">Authentication parameters including the target SPN.</param>
		/// <returns>True if authentication should continue or completed; false on failure.</returns>
		protected override bool GenerateContext(
			ReadOnlySpan<byte> incomingBlob,
			IBufferWriter<byte> outgoingBlobWriter,
			SspiAuthenticationParameters authParams )
		{
			_auth ??= new NegotiateAuthentication(
				new NegotiateAuthenticationClientOptions
				{
					Package = _package,
					TargetName = authParams.Resource,
					Credential = _credential,
				} );

			var blob = _auth.GetOutgoingBlob( incomingBlob, out var statusCode );

			if ( statusCode is not NegotiateAuthenticationStatusCode.Completed
				and not NegotiateAuthenticationStatusCode.ContinueNeeded )
			{
				return false;
			}

			if ( blob is not null )
			{
				outgoingBlobWriter.Write( blob );
			}

			return true;
		}

		/// <summary>
		/// Disposes the underlying <see cref="NegotiateAuthentication"/> instance.
		/// </summary>
		public void Dispose()
		{
			_auth?.Dispose();
		}
	}
}

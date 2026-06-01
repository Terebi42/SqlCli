using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace SqlCli.Auth
{
	/// <summary>
	/// Detects whether a server name resolves to multiple IP addresses, which indicates
	/// an Availability Group listener (or Failover Cluster Instance) whose DNS entry
	/// registers all replica IPs. In that case <c>MultiSubnetFailover</c> should be
	/// enabled so the driver attempts all IPs in parallel instead of stalling on a dead one.
	/// </summary>
	public static class MultiSubnetDetector
	{
		/// <summary>
		/// Determines whether the given server name resolves to more than one distinct IP address.
		/// Returns false for empty input, loopback/local aliases, IP literals, non-TCP protocol
		/// prefixes, or when resolution fails — so that auto-enabling MultiSubnetFailover never
		/// breaks single-IP, local, or shared-memory/named-pipe connections.
		/// </summary>
		/// <param name="server">The configured server / data source value.</param>
		/// <returns>True if the host resolves to multiple distinct IPs over TCP; otherwise false.</returns>
		public static bool ResolvesToMultipleIps( string server )
		{
			var host = ExtractTcpHost( server );
			if ( host is null )
			{
				return false;
			}

			// IP literals resolve to themselves — never multi-IP.
			if ( IPAddress.TryParse( host, out _ ) )
			{
				return false;
			}

			try
			{
				var addresses = Dns.GetHostAddresses( host );
				var distinct = addresses
					.Where( a => !IPAddress.IsLoopback( a ) )
					.Select( a => a.ToString() )
					.Distinct()
					.Count();
				return distinct > 1;
			}
			catch ( Exception ex ) when ( ex is SocketException or ArgumentException )
			{
				// Resolution failed — leave MultiSubnetFailover off rather than guess.
				return false;
			}
		}

		/// <summary>
		/// Extracts the bare TCP host from a data source value, stripping an optional protocol
		/// prefix, named-instance suffix, and port. Returns null when the host is empty, a
		/// loopback/local alias, or uses a non-TCP protocol (named pipes / shared memory),
		/// since MultiSubnetFailover requires TCP and is meaningless for local connections.
		/// </summary>
		/// <param name="server">The configured server / data source value.</param>
		/// <returns>The bare host name, or null if not a resolvable TCP host.</returns>
		internal static string ExtractTcpHost( string server )
		{
			if ( string.IsNullOrWhiteSpace( server ) )
			{
				return null;
			}

			var value = server.Trim();

			// Strip a leading protocol prefix (e.g. "tcp:host", "np:\\...", "lpc:host", "admin:host").
			var colon = value.IndexOf( ':' );
			if ( colon > 0 && !IPAddress.TryParse( value, out _ ) )
			{
				var scheme = value[..colon].Trim().ToLowerInvariant();
				if ( scheme is "tcp" or "admin" )
				{
					value = value[(colon + 1)..].Trim();
				}
				else if ( scheme is "np" or "lpc" )
				{
					// Non-TCP protocols — MultiSubnetFailover does not apply.
					return null;
				}
			}

			// Drop a named-instance suffix ("host\\instance") — resolve only the host.
			var backslash = value.IndexOf( '\\' );
			if ( backslash >= 0 )
			{
				value = value[..backslash];
			}

			// Drop a port suffix ("host,1433").
			var comma = value.IndexOf( ',' );
			if ( comma >= 0 )
			{
				value = value[..comma];
			}

			value = value.Trim();
			if ( value.Length == 0 )
			{
				return null;
			}

			// Local aliases never represent an AG listener.
			var lower = value.ToLowerInvariant();
			if ( lower is "." or "localhost" or "(local)" or "(localdb)" || lower.StartsWith( "(localdb)" ) )
			{
				return null;
			}

			return value;
		}
	}
}

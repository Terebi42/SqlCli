using System;

namespace SqlCli
{
	/// <summary>
	/// Bitwise flag exit codes for the CLI process.
	/// </summary>
	[Flags]
	public enum ExitCode
	{
		/// <summary>Successful execution with no errors.</summary>
		Success = 0,

		/// <summary>SQL error such as connection failure or query failure.</summary>
		SqlError = 1,

		/// <summary>One or more statements were rejected by the filter.</summary>
		FilterBlock = 2,

		/// <summary>Invalid or missing command-line arguments.</summary>
		InvalidArgs = 4,

		/// <summary>Configuration file is missing or contains invalid JSON.</summary>
		ConfigError = 8,

		/// <summary>Authentication failure such as SSPI negotiation failure.</summary>
		AuthError = 16,

		/// <summary>Query exceeded the configured timeout.</summary>
		TimeoutError = 32
	}

	/// <summary>
	/// Extension methods for the <see cref="ExitCode"/> enum.
	/// </summary>
	public static class ExitCodeExtensions
	{
		/// <summary>
		/// Returns a human-readable reference of all exit codes and their meanings.
		/// </summary>
		/// <returns>Formatted exit code help text.</returns>
		public static string GetHelpText()
		{
			return """
				Exit Code Reference (flags - combined via bitwise OR):

				  0   Success
				  1   SQL error (query failed, connection failed, max rows exceeded)
				  2   Filter rejection (blocked statement)
				  4   Invalid arguments (missing required flags, conflicting auth modes, invalid format, input size exceeded)
				  8   Config error (invalid JSON)
				  16  Auth error (SSPI/credentials failed)
				  32  Timeout (query timed out or connection timed out)

				Combined examples:
				  10  Filter rejection (2) + Config error (8)
				  3   SQL error (1) + Filter rejection (2)
				""";
		}
	}
}

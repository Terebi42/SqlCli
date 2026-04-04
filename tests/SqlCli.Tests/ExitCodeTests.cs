using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SqlCli.Tests
{
	/// <summary>
	/// Tests for the <see cref="ExitCode"/> enum and <see cref="ExitCodeExtensions"/> helper.
	/// </summary>
	[TestClass]
	public class ExitCodeTests
	{
		/// <summary>
		/// Verifies that Success maps to exit code 0.
		/// </summary>
		[TestMethod]
		public void Success_IsZero()
		{
			Assert.AreEqual( 0, (int)ExitCode.Success );
		}

		/// <summary>
		/// Verifies that exit codes can be combined via bitwise OR.
		/// </summary>
		[TestMethod]
		public void CombinedCodes_BitwiseOr()
		{
			var combined = ExitCode.FilterBlock | ExitCode.ConfigError;
			Assert.AreEqual( 10, (int)combined );
		}

		/// <summary>
		/// Verifies that all non-zero exit codes are powers of two.
		/// </summary>
		[TestMethod]
		public void AllCodes_ArePowersOfTwo()
		{
			var values = Enum.GetValues<ExitCode>().Where( v => v != ExitCode.Success );
			foreach ( var value in values )
			{
				var intVal = (int)value;
				Assert.IsTrue( ( intVal & ( intVal - 1 ) ) == 0,
					$"{value} ({intVal}) is not a power of 2" );
			}
		}

		/// <summary>
		/// Verifies that the help text contains descriptions for all exit codes.
		/// </summary>
		[TestMethod]
		public void GetHelpText_ContainsAllCodes()
		{
			var help = ExitCodeExtensions.GetHelpText();
			StringAssert.Contains( help, "Success" );
			StringAssert.Contains( help, "SQL error" );
			StringAssert.Contains( help, "Filter rejection" );
			StringAssert.Contains( help, "Invalid arguments" );
			StringAssert.Contains( help, "Config error" );
			StringAssert.Contains( help, "Auth error" );
			StringAssert.Contains( help, "Timeout" );
			StringAssert.Contains( help, "query timed out" );
			StringAssert.Contains( help, "connection timed out" );
		}
	}
}

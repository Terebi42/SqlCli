#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Execution;
using SqlCli.Output;
using OutputFormatException = SqlCli.Output.OutputFormatException;

namespace SqlCli.Tests.Output
{
	/// <summary>
	/// Tests for <see cref="CsvFormatter"/> CSV output formatting.
	/// </summary>
	[TestClass]
	public class CsvFormatterTests
	{
		/// <summary>
		/// Verifies that a single result set produces correct CSV output.
		/// </summary>
		[TestMethod]
		public void Format_SingleResultSet_OutputsCsv()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Id", "Name"],
					[
						new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Acme" },
						new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Globex" }
					] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var csv = new CsvFormatter().Format( result );
			Assert.AreEqual( "Id,Name\r\n1,Acme\r\n2,Globex", csv );
		}

		/// <summary>
		/// Verifies that multiple result sets throw a FormatException.
		/// </summary>
		[TestMethod]
		public void Format_MultipleResultSets_ThrowsFormatException()
		{
			var result = new QueryResult(
			[
				new ResultSet( ["Id"], [new Dictionary<string, object?> { ["Id"] = 1 }] ),
				new ResultSet( ["Name"], [new Dictionary<string, object?> { ["Name"] = "x" }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			Assert.ThrowsExactly<OutputFormatException>( () =>
				new CsvFormatter().Format( result ) );
		}

		/// <summary>
		/// Verifies that values containing commas are properly quoted.
		/// </summary>
		[TestMethod]
		public void Format_ValueContainingComma_QuotedProperly()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Name"],
					[new Dictionary<string, object?> { ["Name"] = "Smith, John" }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var csv = new CsvFormatter().Format( result );
			Assert.AreEqual( "Name\r\n\"Smith, John\"", csv );
		}

		/// <summary>
		/// Verifies that values containing quotes are properly escaped.
		/// </summary>
		[TestMethod]
		public void Format_ValueContainingQuotes_EscapedProperly()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Name"],
					[new Dictionary<string, object?> { ["Name"] = "Say \"hello\"" }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var csv = new CsvFormatter().Format( result );
			Assert.AreEqual( "Name\r\n\"Say \"\"hello\"\"\"", csv );
		}

		/// <summary>
		/// Verifies that null values produce empty CSV fields.
		/// </summary>
		[TestMethod]
		public void Format_NullValue_OutputsEmpty()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Name"],
					[new Dictionary<string, object?> { ["Name"] = null }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var csv = new CsvFormatter().Format( result );
			// CsvHelper writes null as empty string; the row is a blank line after the header
			Assert.AreEqual( "Name\r\n", csv );
		}

		/// <summary>
		/// Verifies that an empty result set outputs only the header row.
		/// </summary>
		[TestMethod]
		public void Format_EmptyResultSet_OutputsHeaderOnly()
		{
			var result = new QueryResult(
			[
				new ResultSet( ["Id", "Name"], new List<Dictionary<string, object?>>() )
			], TimeSpan.FromMilliseconds( 100 ) );

			var csv = new CsvFormatter().Format( result );
			Assert.AreEqual( "Id,Name", csv );
		}
	}
}

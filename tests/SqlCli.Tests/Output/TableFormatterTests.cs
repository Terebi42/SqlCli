#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Execution;
using SqlCli.Output;

namespace SqlCli.Tests.Output
{
	/// <summary>
	/// Tests for <see cref="TableFormatter"/> text table output formatting.
	/// </summary>
	[TestClass]
	public class TableFormatterTests
	{
		/// <summary>
		/// Verifies that a single result set is formatted with headers, data, and row count.
		/// </summary>
		[TestMethod]
		public void Format_SingleResultSet_FormatsTable()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Id", "Name"],
					[
						new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Acme" },
						new Dictionary<string, object?> { ["Id"] = 2, ["Name"] = "Globex" }
					] )
			], TimeSpan.FromMilliseconds( 123 ) );

			var table = new TableFormatter().Format( result );

			StringAssert.Contains( table, "Id" );
			StringAssert.Contains( table, "Name" );
			StringAssert.Contains( table, "Acme" );
			StringAssert.Contains( table, "Globex" );
			StringAssert.Contains( table, "(2 rows)" );
		}

		/// <summary>
		/// Verifies that multiple result sets are both shown in the output.
		/// </summary>
		[TestMethod]
		public void Format_MultipleResultSets_AllShown()
		{
			var result = new QueryResult(
			[
				new ResultSet( ["Id"], [new Dictionary<string, object?> { ["Id"] = 1 }] ),
				new ResultSet( ["Name"], [new Dictionary<string, object?> { ["Name"] = "x" }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var table = new TableFormatter().Format( result );

			StringAssert.Contains( table, "Id" );
			StringAssert.Contains( table, "Name" );
			StringAssert.Contains( table, "(1 row)" );
		}

		/// <summary>
		/// Verifies that an empty result set shows headers and zero rows.
		/// </summary>
		[TestMethod]
		public void Format_EmptyResultSet_ShowsHeaderAndZeroRows()
		{
			var result = new QueryResult(
			[
				new ResultSet( ["Id", "Name"], new List<Dictionary<string, object?>>() )
			], TimeSpan.FromMilliseconds( 50 ) );

			var table = new TableFormatter().Format( result );

			StringAssert.Contains( table, "Id" );
			StringAssert.Contains( table, "(0 rows)" );
		}

		/// <summary>
		/// Verifies that null values are displayed as NULL.
		/// </summary>
		[TestMethod]
		public void Format_NullValue_ShowsNULL()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Name"],
					[new Dictionary<string, object?> { ["Name"] = null }] )
			], TimeSpan.FromMilliseconds( 50 ) );

			var table = new TableFormatter().Format( result );

			StringAssert.Contains( table, "NULL" );
		}
	}
}

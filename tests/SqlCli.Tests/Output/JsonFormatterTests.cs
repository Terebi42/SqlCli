#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Execution;
using SqlCli.Output;

namespace SqlCli.Tests.Output
{
	/// <summary>
	/// Tests for <see cref="JsonFormatter"/> JSON output formatting.
	/// </summary>
	[TestClass]
	public class JsonFormatterTests
	{
		/// <summary>
		/// Verifies that a single result set produces valid JSON with correct structure.
		/// </summary>
		[TestMethod]
		public void Format_SingleResultSet_ValidJson()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Id", "Name"],
					[new Dictionary<string, object?> { ["Id"] = 1, ["Name"] = "Acme" }] )
			], TimeSpan.FromMilliseconds( 123 ) );

			var json = new JsonFormatter().Format( result );
			var doc = JsonDocument.Parse( json );

			Assert.AreEqual( 1, doc.RootElement.GetProperty( "resultSets" ).GetArrayLength() );
			Assert.AreEqual( "00:00:00.1230000", doc.RootElement.GetProperty( "elapsed" ).GetString() );
		}

		/// <summary>
		/// Verifies that multiple result sets are all included in the JSON output.
		/// </summary>
		[TestMethod]
		public void Format_MultipleResultSets_AllIncluded()
		{
			var result = new QueryResult(
			[
				new ResultSet( ["Id"], [new Dictionary<string, object?> { ["Id"] = 1 }] ),
				new ResultSet( ["Name"], [new Dictionary<string, object?> { ["Name"] = "x" }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var json = new JsonFormatter().Format( result );
			var doc = JsonDocument.Parse( json );

			Assert.AreEqual( 2, doc.RootElement.GetProperty( "resultSets" ).GetArrayLength() );
		}

		/// <summary>
		/// Verifies that null values are serialized as JSON null.
		/// </summary>
		[TestMethod]
		public void Format_NullValue_SerializedAsNull()
		{
			var result = new QueryResult(
			[
				new ResultSet(
					["Name"],
					[new Dictionary<string, object?> { ["Name"] = null }] )
			], TimeSpan.FromMilliseconds( 100 ) );

			var json = new JsonFormatter().Format( result );
			var doc = JsonDocument.Parse( json );

			var row = doc.RootElement.GetProperty( "resultSets" )[0]
				.GetProperty( "rows" )[0];
			Assert.AreEqual( JsonValueKind.Null, row.GetProperty( "Name" ).ValueKind );
		}
	}
}

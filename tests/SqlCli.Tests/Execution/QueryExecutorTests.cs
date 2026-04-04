using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Execution;

namespace SqlCli.Tests.Execution
{
	/// <summary>
	/// Tests for <see cref="QueryExecutor"/> column deduplication and exception types.
	/// </summary>
	[TestClass]
	public class QueryExecutorTests
	{
		/// <summary>
		/// Verifies that duplicate column names get suffixed with _1, _2, etc.
		/// </summary>
		[TestMethod]
		public void DeduplicateColumns_DuplicateNames_GetSuffixed()
		{
			var raw = new List<string> { "Id", "Id", "Name" };
			var result = QueryExecutor.DeduplicateColumns( raw );
			CollectionAssert.AreEqual( new[] { "Id", "Id_1", "Name" }, result.ToArray() );
		}

		/// <summary>
		/// Verifies that unique column names are unchanged.
		/// </summary>
		[TestMethod]
		public void DeduplicateColumns_UniqueNames_Unchanged()
		{
			var raw = new List<string> { "Id", "Name", "Date" };
			var result = QueryExecutor.DeduplicateColumns( raw );
			CollectionAssert.AreEqual( new[] { "Id", "Name", "Date" }, result.ToArray() );
		}

		/// <summary>
		/// Verifies that three duplicate columns get suffixed incrementally.
		/// </summary>
		[TestMethod]
		public void DeduplicateColumns_ThreeDuplicates_IncrementalSuffix()
		{
			var raw = new List<string> { "Col", "Col", "Col" };
			var result = QueryExecutor.DeduplicateColumns( raw );
			CollectionAssert.AreEqual( new[] { "Col", "Col_1", "Col_2" }, result.ToArray() );
		}

		/// <summary>
		/// Verifies that MaxRowsExceededException can be constructed with a message.
		/// </summary>
		[TestMethod]
		public void MaxRowsExceededException_HasMessage()
		{
			var ex = new MaxRowsExceededException( "Result set exceeded maximum row limit of 100." );
			Assert.AreEqual( "Result set exceeded maximum row limit of 100.", ex.Message );
		}
	}
}

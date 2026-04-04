#nullable enable
using System;
using System.Collections.Generic;

namespace SqlCli.Execution
{
	/// <summary>
	/// Contains the result sets and elapsed time from executing SQL statements.
	/// </summary>
	/// <param name="ResultSets">Ordered list of result sets returned by the query.</param>
	/// <param name="Elapsed">Total execution time.</param>
	public record QueryResult(
		List<ResultSet> ResultSets,
		TimeSpan Elapsed
	);

	/// <summary>
	/// Represents a single result set with column names and row data.
	/// </summary>
	/// <param name="Columns">Ordered list of column names.</param>
	/// <param name="Rows">List of row dictionaries mapping column names to values.</param>
	public record ResultSet(
		List<string> Columns,
		List<Dictionary<string, object?>> Rows
	);
}

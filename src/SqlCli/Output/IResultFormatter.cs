using System;
using SqlCli.Execution;

namespace SqlCli.Output
{
	/// <summary>
	/// Formats query results into a specific output format (CSV, JSON, table).
	/// </summary>
	public interface IResultFormatter
	{
		/// <summary>
		/// Formats the query result into a string representation.
		/// </summary>
		/// <param name="result">Query result to format.</param>
		/// <returns>Formatted output string.</returns>
		string Format( QueryResult result );
	}

	/// <summary>
	/// Represents an error in output formatting such as unsupported multi-result-set output.
	/// </summary>
	public class OutputFormatException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="OutputFormatException"/> class.
		/// </summary>
		/// <param name="message">Error message.</param>
		public OutputFormatException( string message ) : base( message ) { }
	}
}

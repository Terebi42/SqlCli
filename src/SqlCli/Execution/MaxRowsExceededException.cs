using System;

namespace SqlCli.Execution
{
	/// <summary>
	/// Thrown when a query result exceeds the configured maximum row count.
	/// </summary>
	public class MaxRowsExceededException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MaxRowsExceededException"/> class.
		/// </summary>
		/// <param name="message">Error message describing the row limit violation.</param>
		public MaxRowsExceededException( string message ) : base( message ) { }
	}
}

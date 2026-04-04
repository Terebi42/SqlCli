namespace SqlCli.Filtering
{
	/// <summary>
	/// Validates SQL input against a configured set of allowed statement types.
	/// </summary>
	public interface IStatementFilter
	{
		/// <summary>
		/// Validates the given SQL text and returns the filter result.
		/// </summary>
		/// <param name="sql">Raw SQL text to validate.</param>
		/// <returns>Filter result indicating whether the SQL is allowed.</returns>
		FilterResult Validate( string sql );
	}
}

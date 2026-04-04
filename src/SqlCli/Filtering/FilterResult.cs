using System.Collections.Generic;

namespace SqlCli.Filtering
{
	/// <summary>
	/// Result of validating SQL statements against the configured filter.
	/// </summary>
	/// <param name="Allowed">Whether all statements passed validation.</param>
	/// <param name="Violations">List of filter violations, empty if allowed.</param>
	/// <param name="Statements">Individual statement texts extracted from the AST parse.</param>
	public record FilterResult( bool Allowed, List<FilterViolation> Violations, List<string> Statements )
	{
		/// <summary>
		/// Creates a successful filter result with no violations.
		/// </summary>
		/// <param name="statements">Individual statement texts extracted from the AST.</param>
		/// <returns>A passing filter result.</returns>
		public static FilterResult Success( List<string> statements = null )
		{
			return new( true, [], statements ?? [] );
		}

		/// <summary>
		/// Creates a blocked filter result with the specified violations.
		/// </summary>
		/// <param name="violations">List of violations that caused the block.</param>
		/// <returns>A failing filter result.</returns>
		public static FilterResult Blocked( List<FilterViolation> violations )
		{
			return new( false, violations, [] );
		}
	}

	/// <summary>
	/// Describes a single filter violation for a blocked SQL statement.
	/// </summary>
	/// <param name="Statement">The SQL text that was blocked.</param>
	/// <param name="Keyword">The statement type or keyword that triggered the block.</param>
	/// <param name="Reason">Human-readable reason for the block.</param>
	public record FilterViolation( string Statement, string Keyword, string Reason );
}

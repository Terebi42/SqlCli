using System;
using System.Linq;
using System.Text;
using SqlCli.Execution;

namespace SqlCli.Output
{
	/// <summary>
	/// Formats query results as a human-readable padded text table.
	/// </summary>
	public class TableFormatter : IResultFormatter
	{
		/// <summary>
		/// Formats the query result as a text table with column-aligned values.
		/// </summary>
		/// <param name="result">Query result to format.</param>
		/// <returns>Formatted table string.</returns>
		public string Format( QueryResult result )
		{
			if ( result.ResultSets.Count == 0 )
			{
				return string.Empty;
			}

			var sb = new StringBuilder();

			for ( var i = 0; i < result.ResultSets.Count; i++ )
			{
				if ( i > 0 )
				{
					sb.AppendLine().AppendLine();
				}

				FormatResultSet( sb, result.ResultSets[i] );
			}

			sb.AppendLine();
			sb.Append( $"Elapsed: {result.Elapsed.TotalSeconds:F3}s" );

			return sb.ToString();
		}

		/// <summary>
		/// Formats a single result set into the string builder with headers, separators, and rows.
		/// </summary>
		/// <param name="sb">String builder to append to.</param>
		/// <param name="rs">Result set to format.</param>
		private static void FormatResultSet( StringBuilder sb, ResultSet rs )
		{
			var widths = rs.Columns.Select( c => c.Length ).ToArray();

			foreach ( var row in rs.Rows )
			{
				for ( var i = 0; i < rs.Columns.Count; i++ )
				{
					var val = FormatValue( row[rs.Columns[i]] );
					widths[i] = Math.Max( widths[i], val.Length );
				}
			}

			sb.AppendLine( string.Join( " | ", rs.Columns.Select( ( c, i ) => c.PadRight( widths[i] ) ) ) );
			sb.AppendLine( string.Join( "-|-", widths.Select( w => new string( '-', w ) ) ) );

			foreach ( var row in rs.Rows )
			{
				var values = rs.Columns.Select( ( c, i ) => FormatValue( row[c] ).PadRight( widths[i] ) );
				sb.AppendLine( string.Join( " | ", values ) );
			}

			sb.Append( $"({rs.Rows.Count} row{( rs.Rows.Count == 1 ? "" : "s" )})" );
		}

		/// <summary>
		/// Converts a value to its string representation, showing NULL for null or DBNull.
		/// </summary>
		/// <param name="value">Value to format.</param>
		/// <returns>String representation.</returns>
		private static string FormatValue( object value )
		{
			return value is null or DBNull ? "NULL" : value.ToString() ?? string.Empty;
		}
	}
}

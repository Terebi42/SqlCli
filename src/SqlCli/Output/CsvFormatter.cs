using System;
using System.Globalization;
using System.IO;
using CsvHelper;
using SqlCli.Execution;

namespace SqlCli.Output
{
	/// <summary>
	/// Formats a single query result set as RFC 4180 compliant CSV.
	/// </summary>
	public class CsvFormatter : IResultFormatter
	{
		/// <summary>
		/// Formats the query result as CSV text. Throws if multiple result sets are present.
		/// </summary>
		/// <param name="result">Query result to format.</param>
		/// <returns>CSV string representation.</returns>
		public string Format( QueryResult result )
		{
			if ( result.ResultSets.Count > 1 )
			{
				throw new OutputFormatException(
					"CSV format does not support multiple result sets. Run queries separately or use --format json." );
			}

			if ( result.ResultSets.Count == 0 )
			{
				return string.Empty;
			}

			var rs = result.ResultSets[0];

			using var writer = new StringWriter();
			using var csv = new CsvWriter( writer, CultureInfo.InvariantCulture );

			// Write header
			foreach ( var col in rs.Columns )
			{
				csv.WriteField( col );
			}

			csv.NextRecord();

			// Write rows
			foreach ( var row in rs.Rows )
			{
				foreach ( var col in rs.Columns )
				{
					var value = row[col];
					csv.WriteField( value is DBNull ? null : value );
				}

				csv.NextRecord();
			}

			csv.Flush();
			var output = writer.ToString();

			// Remove the single trailing \r\n added by the last NextRecord call
			if ( output.EndsWith( "\r\n" ) )
			{
				output = output[..^2];
			}
			else if ( output.EndsWith( "\n" ) )
			{
				output = output[..^1];
			}

			return output;
		}
	}
}

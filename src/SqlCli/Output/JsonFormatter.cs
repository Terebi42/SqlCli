using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlCli.Execution;

namespace SqlCli.Output
{
	/// <summary>
	/// Formats query results as a JSON object with result sets and elapsed time.
	/// </summary>
	public class JsonFormatter : IResultFormatter
	{
		private static readonly JsonSerializerOptions Options = new()
		{
			WriteIndented = false,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			DefaultIgnoreCondition = JsonIgnoreCondition.Never
		};

		/// <summary>
		/// Formats the query result as a JSON string.
		/// </summary>
		/// <param name="result">Query result to format.</param>
		/// <returns>JSON string representation.</returns>
		public string Format( QueryResult result )
		{
			var output = new
			{
				resultSets = result.ResultSets.Select( rs => new
				{
					columns = rs.Columns,
					rows = rs.Rows
				} ),
				elapsed = result.Elapsed.ToString()
			};

			return JsonSerializer.Serialize( output, Options );
		}
	}
}

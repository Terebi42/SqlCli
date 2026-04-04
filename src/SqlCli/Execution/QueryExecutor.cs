#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlCli.Auth;

namespace SqlCli.Execution
{
	/// <summary>
	/// Executes SQL statements against a SQL Server connection.
	/// </summary>
	public class QueryExecutor
	{
		/// <summary>
		/// Executes the given SQL statements and returns all result sets.
		/// </summary>
		/// <param name="connectionString">SQL Server connection string.</param>
		/// <param name="statements">List of SQL statements to execute.</param>
		/// <param name="timeoutSeconds">Command timeout in seconds.</param>
		/// <param name="authMode">Authentication mode (used to configure SSPI provider for domain auth).</param>
		/// <param name="maxRows">Maximum number of rows per result set. Throws if exceeded.</param>
		/// <returns>Query result containing all result sets and elapsed time.</returns>
		public static QueryResult Execute( string connectionString, List<string> statements, int timeoutSeconds, AuthMode authMode, int maxRows = 0 )
		{
			if ( maxRows <= 0 )
			{
				throw new ArgumentException( "maxRows must be at least 1.", nameof( maxRows ) );
			}

			var sw = Stopwatch.StartNew();
			var resultSets = new List<ResultSet>();

			using var connection = new SqlConnection( connectionString );
			DomainSspiContextProvider? sspiProvider = null;

			if ( authMode is AuthMode.DomainAuth da )
			{
				sspiProvider = new DomainSspiContextProvider( da.Domain, da.User, da.Password, da.SspiPackage );
				connection.SspiContextProvider = sspiProvider;
			}

			try
			{
				connection.Open();

				foreach ( var sql in statements )
				{
					using var command = new SqlCommand( sql, connection )
					{
						CommandTimeout = timeoutSeconds
					};

					using var reader = command.ExecuteReader();
					do
					{
						var rawColumns = new List<string>();
						for ( var i = 0; i < reader.FieldCount; i++ )
						{
							rawColumns.Add( reader.GetName( i ) );
						}

						var columns = DeduplicateColumns( rawColumns );

						var rows = new List<Dictionary<string, object?>>();
						while ( reader.Read() )
						{
							if ( maxRows > 0 && rows.Count >= maxRows )
							{
								throw new MaxRowsExceededException(
									$"Result set exceeded maximum row limit of {maxRows}." );
							}

							var row = new Dictionary<string, object?>();
							for ( var i = 0; i < reader.FieldCount; i++ )
							{
								var value = reader.GetValue( i );
								row[columns[i]] = value is DBNull ? null : value;
							}

							rows.Add( row );
						}

						resultSets.Add( new ResultSet( columns, rows ) );
					} while ( reader.NextResult() );
				}

				sw.Stop();
				return new QueryResult( resultSets, sw.Elapsed );
			}
			finally
			{
				sspiProvider?.Dispose();
			}
		}

		/// <summary>
		/// Deduplicates column names by appending _1, _2 suffixes for duplicates.
		/// </summary>
		/// <param name="rawColumns">Raw column names from the reader.</param>
		/// <returns>Deduplicated column names.</returns>
		public static List<string> DeduplicateColumns( List<string> rawColumns )
		{
			var result = new List<string>( rawColumns.Count );
			var seen = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );

			foreach ( var col in rawColumns )
			{
				if ( seen.TryGetValue( col, out var count ) )
				{
					seen[col] = count + 1;
					result.Add( $"{col}_{count}" );
				}
				else
				{
					seen[col] = 1;
					result.Add( col );
				}
			}

			return result;
		}
	}
}

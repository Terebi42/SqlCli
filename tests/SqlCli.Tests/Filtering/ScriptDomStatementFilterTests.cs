using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Filtering;

namespace SqlCli.Tests.Filtering
{
	/// <summary>
	/// Tests for <see cref="ScriptDomStatementFilter"/> parser-based SQL filtering.
	/// </summary>
	[TestClass]
	public class ScriptDomStatementFilterTests
	{
		/// <summary>
		/// Creates a filter with the specified allowed type names and no audit log.
		/// </summary>
		/// <param name="allowed">Allowed ScriptDom statement type names.</param>
		/// <returns>Configured filter instance.</returns>
		private ScriptDomStatementFilter CreateFilter( params string[] allowed )
		{
			return new( allowed.ToList(), null );
		}

		/// <summary>
		/// Creates a filter with allowed types and allowed SELECT features.
		/// </summary>
		/// <param name="allowedTypes">Allowed ScriptDom statement type names.</param>
		/// <param name="allowedSelectFeatures">Allowed SELECT sub-features.</param>
		/// <returns>Configured filter instance.</returns>
		private ScriptDomStatementFilter CreateFilterWithFeatures( string[] allowedTypes, string[] allowedSelectFeatures )
		{
			return new( allowedTypes.ToList(), null, allowedSelectFeatures.ToList() );
		}

		/// <summary>
		/// Verifies that an allowed SELECT passes validation.
		/// </summary>
		[TestMethod]
		public void Validate_AllowedSelect_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM Orders" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that a CTE with SELECT passes as SelectStatement.
		/// </summary>
		[TestMethod]
		public void Validate_CteSelect_PassesAsSelectStatement()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "WITH cte AS (SELECT 1) SELECT * FROM cte" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that a blocked DELETE is rejected with correct type name.
		/// </summary>
		[TestMethod]
		public void Validate_BlockedDelete_Rejected()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "DELETE FROM Orders" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "DeleteStatement", result.Violations[0].Keyword );
		}

		/// <summary>
		/// Verifies that case-insensitive SQL still parses correctly.
		/// </summary>
		[TestMethod]
		public void Validate_CaseInsensitiveSql_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "select * from Orders" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that the type name matching is case-insensitive.
		/// </summary>
		[TestMethod]
		public void Validate_TypeNameCaseInsensitive_Passes()
		{
			var filter = CreateFilter( "selectstatement" );
			var result = filter.Validate( "SELECT 1" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that a multi-batch with one blocked statement rejects all.
		/// </summary>
		[TestMethod]
		public void Validate_MultiBatch_OneBlocked_RejectsAll()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT 1; DROP TABLE Orders" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "DropTableStatement", result.Violations[0].Keyword );
		}

		/// <summary>
		/// Verifies that all blocked statements in a multi-batch are reported.
		/// </summary>
		[TestMethod]
		public void Validate_MultiBatch_AllBlocked_ReportsAll()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "DELETE FROM Orders; TRUNCATE TABLE Logs" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 2, result.Violations.Count );
		}

		/// <summary>
		/// Verifies that blocked keywords inside line comments do not trigger rejection.
		/// </summary>
		[TestMethod]
		public void Validate_CommentContainingBlockedKeyword_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "-- DELETE everything\nSELECT 1" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that blocked keywords inside block comments do not trigger rejection.
		/// </summary>
		[TestMethod]
		public void Validate_BlockCommentContainingBlockedKeyword_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "/* DROP TABLE */ SELECT 1" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that empty input passes validation.
		/// </summary>
		[TestMethod]
		public void Validate_EmptyInput_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that whitespace-only input passes validation.
		/// </summary>
		[TestMethod]
		public void Validate_WhitespaceInput_Passes()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "   " );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that an empty whitelist blocks all statements.
		/// </summary>
		[TestMethod]
		public void Validate_EmptyWhitelist_BlocksEverything()
		{
			var filter = CreateFilter();
			var result = filter.Validate( "SELECT 1" );
			Assert.IsFalse( result.Allowed );
		}

		/// <summary>
		/// Verifies that WITH...DELETE is correctly identified as DeleteStatement.
		/// </summary>
		[TestMethod]
		public void Validate_WithDeleteAfterCte_Blocked()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "WITH cte AS (SELECT * FROM Orders) DELETE FROM cte" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( "DeleteStatement", result.Violations[0].Keyword );
		}

		/// <summary>
		/// Verifies that dynamic SQL inside EXEC is blocked.
		/// </summary>
		[TestMethod]
		public void Validate_DynamicSqlInExec_Blocked()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "EXEC('DELETE FROM Orders')" );
			Assert.IsFalse( result.Allowed );
		}

		/// <summary>
		/// Verifies that nested block comments are handled correctly by ScriptDom.
		/// </summary>
		[TestMethod]
		public void Validate_NestedBlockComments_HandledCorrectly()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "/* outer /* inner */ still comment */ SELECT 1" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that the audit log is written as valid JSON lines when a statement is rejected.
		/// </summary>
		[TestMethod]
		public void Validate_AuditLogWritten_AsJsonLines_OnRejection()
		{
			var tempDir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() );
			Directory.CreateDirectory( tempDir );
			var auditPath = Path.Combine( tempDir, "audit.log" );

			try
			{
				var filter = new ScriptDomStatementFilter( new List<string> { "SelectStatement" }, auditPath );
				filter.Validate( "DELETE FROM Orders" );

				Assert.IsTrue( File.Exists( auditPath ) );
				var lines = File.ReadAllLines( auditPath );
				Assert.AreEqual( 1, lines.Length );

				var doc = JsonDocument.Parse( lines[0] );
				var root = doc.RootElement;
				Assert.AreEqual( "BLOCKED", root.GetProperty( "event" ).GetString() );
				Assert.AreEqual( "DeleteStatement", root.GetProperty( "statementType" ).GetString() );
				Assert.IsTrue( root.TryGetProperty( "timestamp", out _ ) );
				Assert.IsTrue( root.TryGetProperty( "pid", out var pidProp ) );
				Assert.AreEqual( JsonValueKind.Number, pidProp.ValueKind );
				Assert.IsTrue( root.TryGetProperty( "sql", out _ ) );
				Assert.IsTrue( root.TryGetProperty( "reason", out _ ) );
			}
			finally
			{
				Directory.Delete( tempDir, true );
			}
		}

		/// <summary>
		/// Verifies that the audit log is not written when statements pass.
		/// </summary>
		[TestMethod]
		public void Validate_AuditLogNotWritten_OnSuccess()
		{
			var tempDir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() );
			Directory.CreateDirectory( tempDir );
			var auditPath = Path.Combine( tempDir, "audit.log" );

			try
			{
				var filter = new ScriptDomStatementFilter( new List<string> { "SelectStatement" }, auditPath );
				filter.Validate( "SELECT 1" );

				Assert.IsFalse( File.Exists( auditPath ) );
			}
			finally
			{
				Directory.Delete( tempDir, true );
			}
		}

		/// <summary>
		/// Verifies that string literals containing blocked keywords are not confused with actual statements.
		/// </summary>
		[TestMethod]
		public void Validate_StringLiteralWithKeyword_NotConfused()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT 'DROP TABLE Orders'" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that FilterResult.Statements contains the individual parsed statements.
		/// </summary>
		[TestMethod]
		public void Validate_Success_PopulatesStatements()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT 1; SELECT 2" );
			Assert.IsTrue( result.Allowed );
			Assert.AreEqual( 2, result.Statements.Count );
			Assert.AreEqual( "SELECT 1", result.Statements[0] );
			Assert.AreEqual( "SELECT 2", result.Statements[1] );
		}

		/// <summary>
		/// Verifies that a single statement populates correctly.
		/// </summary>
		[TestMethod]
		public void Validate_SingleStatement_PopulatesStatements()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM Orders" );
			Assert.IsTrue( result.Allowed );
			Assert.AreEqual( 1, result.Statements.Count );
			Assert.AreEqual( "SELECT * FROM Orders", result.Statements[0] );
		}

		/// <summary>
		/// Verifies that long SQL text is truncated in violation messages.
		/// </summary>
		[TestMethod]
		public void Validate_LongSql_TruncatedInViolations()
		{
			var filter = CreateFilter( "SelectStatement" );
			var longSql = "DELETE FROM " + new string( 'X', 300 );
			var result = filter.Validate( longSql );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.IsTrue( result.Violations[0].Statement.EndsWith( "(truncated)" ) );
			Assert.IsTrue( result.Violations[0].Statement.Length < longSql.Length );
		}

		/// <summary>
		/// Verifies that short SQL text is not truncated in violation messages.
		/// </summary>
		[TestMethod]
		public void Validate_ShortSql_NotTruncatedInViolations()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "DELETE FROM Orders" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( "DELETE FROM Orders", result.Violations[0].Statement );
		}

		/// <summary>
		/// Verifies that SELECT INTO is blocked by default.
		/// </summary>
		[TestMethod]
		public void Validate_SelectInto_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * INTO NewTable FROM Orders" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "SelectInto", result.Violations[0].Keyword );
			StringAssert.Contains( result.Violations[0].Reason, "SELECT INTO" );
		}

		/// <summary>
		/// Verifies that SELECT INTO passes when IntoClause is in allowedSelectFeatures.
		/// </summary>
		[TestMethod]
		public void Validate_SelectInto_PassesWhenFeatureAllowed()
		{
			var filter = CreateFilterWithFeatures( ["SelectStatement"], ["IntoClause"] );
			var result = filter.Validate( "SELECT * INTO NewTable FROM Orders" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that OPENROWSET BULK is blocked by default.
		/// </summary>
		[TestMethod]
		public void Validate_OpenRowsetBulk_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM OPENROWSET(BULK 'C:\\data.csv', SINGLE_CLOB) AS x" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "OpenRowset", result.Violations[0].Keyword );
			StringAssert.Contains( result.Violations[0].Reason, "OPENROWSET" );
		}

		/// <summary>
		/// Verifies that OPENROWSET passes when OpenRowset is in allowedSelectFeatures.
		/// </summary>
		[TestMethod]
		public void Validate_OpenRowset_PassesWhenFeatureAllowed()
		{
			var filter = CreateFilterWithFeatures( ["SelectStatement"], ["OpenRowset"] );
			var result = filter.Validate( "SELECT * FROM OPENROWSET(BULK 'C:\\data.csv', SINGLE_CLOB) AS x" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that OPENDATASOURCE is blocked by default.
		/// </summary>
		[TestMethod]
		public void Validate_OpenDatasource_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM OPENDATASOURCE('SQLOLEDB','Data Source=srv;').db.dbo.t" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "OpenDatasource", result.Violations[0].Keyword );
			StringAssert.Contains( result.Violations[0].Reason, "OPENDATASOURCE" );
		}

		/// <summary>
		/// Verifies that OPENDATASOURCE passes when OpenDatasource is in allowedSelectFeatures.
		/// </summary>
		[TestMethod]
		public void Validate_OpenDatasource_PassesWhenFeatureAllowed()
		{
			var filter = CreateFilterWithFeatures( ["SelectStatement"], ["OpenDatasource"] );
			var result = filter.Validate( "SELECT * FROM OPENDATASOURCE('SQLOLEDB','Data Source=srv;').db.dbo.t" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that OPENROWSET in a subquery is also caught.
		/// </summary>
		[TestMethod]
		public void Validate_OpenRowsetInSubquery_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM (SELECT * FROM OPENROWSET(BULK 'C:\\data.csv', SINGLE_CLOB) AS x) AS sub" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( "OpenRowset", result.Violations[0].Keyword );
		}

		/// <summary>
		/// Verifies that OPENQUERY is blocked by default.
		/// </summary>
		[TestMethod]
		public void Validate_OpenQuery_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "SELECT * FROM OPENQUERY(LinkedServer, 'SELECT 1')" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "OpenQuery", result.Violations[0].Keyword );
			StringAssert.Contains( result.Violations[0].Reason, "OPENQUERY" );
		}

		/// <summary>
		/// Verifies that OPENQUERY passes when OpenQuery is in allowedSelectFeatures.
		/// </summary>
		[TestMethod]
		public void Validate_OpenQuery_PassesWhenFeatureAllowed()
		{
			var filter = CreateFilterWithFeatures( ["SelectStatement"], ["OpenQuery"] );
			var result = filter.Validate( "SELECT * FROM OPENQUERY(LinkedServer, 'SELECT 1')" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that OPTION (MAXRECURSION 0) is blocked by default.
		/// </summary>
		[TestMethod]
		public void Validate_MaxRecursionZero_BlockedByDefault()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "WITH cte AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM cte WHERE n < 1000) SELECT * FROM cte OPTION (MAXRECURSION 0)" );
			Assert.IsFalse( result.Allowed );
			Assert.AreEqual( 1, result.Violations.Count );
			Assert.AreEqual( "MaxRecursion0", result.Violations[0].Keyword );
			StringAssert.Contains( result.Violations[0].Reason, "MAXRECURSION 0" );
		}

		/// <summary>
		/// Verifies that OPTION (MAXRECURSION 0) passes when UnlimitedMaxRecursion is allowed.
		/// </summary>
		[TestMethod]
		public void Validate_MaxRecursionZero_PassesWhenFeatureAllowed()
		{
			var filter = CreateFilterWithFeatures( ["SelectStatement"], ["UnlimitedMaxRecursion"] );
			var result = filter.Validate( "WITH cte AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM cte WHERE n < 1000) SELECT * FROM cte OPTION (MAXRECURSION 0)" );
			Assert.IsTrue( result.Allowed );
		}

		/// <summary>
		/// Verifies that OPTION (MAXRECURSION 100) is NOT blocked (only 0 is dangerous).
		/// </summary>
		[TestMethod]
		public void Validate_MaxRecursionNonZero_NotBlocked()
		{
			var filter = CreateFilter( "SelectStatement" );
			var result = filter.Validate( "WITH cte AS (SELECT 1 AS n UNION ALL SELECT n+1 FROM cte WHERE n < 100) SELECT * FROM cte OPTION (MAXRECURSION 100)" );
			Assert.IsTrue( result.Allowed );
		}
	}
}

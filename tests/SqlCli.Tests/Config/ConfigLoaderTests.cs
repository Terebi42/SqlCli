using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Config;

namespace SqlCli.Tests.Config
{
	/// <summary>
	/// Tests for <see cref="ConfigLoader"/> configuration file loading, splitting, and generation.
	/// </summary>
	[TestClass]
	public class ConfigLoaderTests
	{
		private string _tempDir;

		/// <summary>
		/// Creates a temporary directory for each test.
		/// </summary>
		[TestInitialize]
		public void Setup()
		{
			_tempDir = Path.Combine( Path.GetTempPath(), Guid.NewGuid().ToString() );
			Directory.CreateDirectory( _tempDir );
		}

		/// <summary>
		/// Cleans up the temporary directory after each test.
		/// </summary>
		[TestCleanup]
		public void Cleanup()
		{
			if ( Directory.Exists( _tempDir ) )
			{
				Directory.Delete( _tempDir, true );
			}
		}

		// --- SecurityConfig loading tests ---

		/// <summary>
		/// Verifies that a valid combined config file loads security settings correctly.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_ValidCombinedConfig_DeserializesCorrectly()
		{
			var json = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": ["SelectStatement", "ExecuteStatement"],
			    "audit": { "enabled": false, "path": "custom.log" }
			  },
			  "operational": {
			    "timeout": 60
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var security = ConfigLoader.LoadSecurity( _tempDir );

			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement", "ExecuteStatement" }, security.AllowedStatements.ToArray() );
			Assert.IsFalse( security.Audit.Enabled );
			Assert.AreEqual( "custom.log", security.Audit.Path );
		}

		/// <summary>
		/// Verifies that a dedicated security file takes precedence over the combined config.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_SecurityFileExist_TakesPrecedence()
		{
			var combinedJson = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": ["SelectStatement", "ExecuteStatement"]
			  }
			}
			""";
			var securityJson = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": ["SelectStatement"]
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), combinedJson );
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.security.jsonc" ), securityJson );

			var security = ConfigLoader.LoadSecurity( _tempDir );

			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
		}

		/// <summary>
		/// Verifies that EnsureConfigExists generates a default config and LoadSecurity loads it.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_MissingFile_EnsureConfigExistsGeneratesDefault()
		{
			ConfigLoader.EnsureConfigExists( _tempDir );
			Assert.IsTrue( File.Exists( Path.Combine( _tempDir, "sqlcli.config.jsonc" ) ) );

			var security = ConfigLoader.LoadSecurity( _tempDir );

			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
			Assert.IsTrue( security.Audit.Enabled );
		}

		/// <summary>
		/// Verifies that LoadSecurity returns safe defaults when no config file exists
		/// and EnsureConfigExists was not called.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_MissingFile_ReturnsSafeDefaults()
		{
			var security = ConfigLoader.LoadSecurity( _tempDir );

			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
		}

		/// <summary>
		/// Verifies that empty allowedStatements in file loads as empty list (not class default).
		/// </summary>
		[TestMethod]
		public void LoadSecurity_EmptyAllowedStatements_LoadsEmptyList()
		{
			var json = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": []
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var security = ConfigLoader.LoadSecurity( _tempDir );

			Assert.AreEqual( 0, security.AllowedStatements.Count );
		}

		/// <summary>
		/// Verifies that an unsupported filterMode throws a ConfigException.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_UnsupportedFilterMode_ThrowsConfigException()
		{
			var json = """
			{
			  "security": {
			    "filterMode": "blacklist",
			    "allowedStatements": ["SelectStatement"]
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var ex = Assert.ThrowsExactly<ConfigException>( () => ConfigLoader.LoadSecurity( _tempDir ) );
			StringAssert.Contains( ex.Message, "Unsupported filterMode" );
			StringAssert.Contains( ex.Message, "blacklist" );
		}

		/// <summary>
		/// Verifies that invalid JSON throws a ConfigException with a clear message.
		/// </summary>
		[TestMethod]
		public void LoadSecurity_InvalidJson_ThrowsWithClearMessage()
		{
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), "not json{{{" );

			var ex = Assert.ThrowsExactly<ConfigException>( () => ConfigLoader.LoadSecurity( _tempDir ) );
			StringAssert.Contains( ex.Message, "Invalid configuration" );
		}

		// --- OperationalConfig loading tests ---

		/// <summary>
		/// Verifies that operational config loads from exe dir.
		/// </summary>
		[TestMethod]
		public void LoadOperational_FromExeDir_LoadsCorrectly()
		{
			var json = """
			{
			  "operational": {
			    "timeout": 60,
			    "connectTimeout": 20,
			    "maxRows": 200,
			    "maxFileSize": 100000,
			    "format": "json"
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var ops = ConfigLoader.LoadOperational( _tempDir, _tempDir );

			Assert.AreEqual( 60, ops.Timeout );
			Assert.AreEqual( 20, ops.ConnectTimeout );
			Assert.AreEqual( 200, ops.MaxRows );
			Assert.AreEqual( 100000L, ops.MaxFileSize );
			Assert.AreEqual( "json", ops.Format );
		}

		/// <summary>
		/// Verifies that working-dir config overrides exe-dir values.
		/// </summary>
		[TestMethod]
		public void LoadOperational_WorkingDirOverrides_ExeDirValues()
		{
			var exeDir = Path.Combine( _tempDir, "exe" );
			var workDir = Path.Combine( _tempDir, "work" );
			Directory.CreateDirectory( exeDir );
			Directory.CreateDirectory( workDir );

			var exeJson = """
			{
			  "operational": {
			    "timeout": 30,
			    "maxRows": 100
			  }
			}
			""";
			var workJson = """
			{
			  "operational": {
			    "timeout": 60,
			    "maxRows": 500
			  }
			}
			""";
			File.WriteAllText( Path.Combine( exeDir, "sqlcli.config.jsonc" ), exeJson );
			File.WriteAllText( Path.Combine( workDir, "sqlcli.config.jsonc" ), workJson );

			var ops = ConfigLoader.LoadOperational( exeDir, workDir );

			Assert.AreEqual( 60, ops.Timeout );
			Assert.AreEqual( 500, ops.MaxRows );
		}

		/// <summary>
		/// Verifies that missing config returns defaults.
		/// </summary>
		[TestMethod]
		public void LoadOperational_MissingConfig_ReturnsDefaults()
		{
			var ops = ConfigLoader.LoadOperational( _tempDir, _tempDir );

			Assert.AreEqual( 30, ops.Timeout );
			Assert.AreEqual( 15, ops.ConnectTimeout );
			Assert.AreEqual( 100, ops.MaxRows );
			Assert.AreEqual( 51200L, ops.MaxFileSize );
			Assert.AreEqual( "csv", ops.Format );
		}

		// --- AppConfig loading tests ---

		/// <summary>
		/// Verifies that app config loads from exe dir combined config.
		/// </summary>
		[TestMethod]
		public void LoadApp_FromExeDir_LoadsCorrectly()
		{
			var json = """
			{
			  "app": {
			    "server": "myserver",
			    "database": "mydb",
			    "trustServerCertificate": true,
			    "noEncrypt": true
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var app = ConfigLoader.LoadApp( _tempDir, _tempDir );

			Assert.AreEqual( "myserver", app.Server );
			Assert.AreEqual( "mydb", app.Database );
			Assert.IsTrue( app.TrustServerCertificate );
			Assert.IsTrue( app.NoEncrypt );
		}

		/// <summary>
		/// Verifies that app config from working-dir sqlcli.app.jsonc overrides exe-dir values.
		/// </summary>
		[TestMethod]
		public void LoadApp_WorkingDirAppFile_OverridesExeDirValues()
		{
			var exeDir = Path.Combine( _tempDir, "exe" );
			var workDir = Path.Combine( _tempDir, "work" );
			Directory.CreateDirectory( exeDir );
			Directory.CreateDirectory( workDir );

			var exeJson = """
			{
			  "app": {
			    "server": "exe-server",
			    "database": "exe-db"
			  }
			}
			""";
			var appJson = """
			{
			  "app": {
			    "server": "work-server",
			    "database": "work-db"
			  }
			}
			""";
			File.WriteAllText( Path.Combine( exeDir, "sqlcli.config.jsonc" ), exeJson );
			File.WriteAllText( Path.Combine( workDir, "sqlcli.app.jsonc" ), appJson );

			var app = ConfigLoader.LoadApp( exeDir, workDir );

			Assert.AreEqual( "work-server", app.Server );
			Assert.AreEqual( "work-db", app.Database );
		}

		/// <summary>
		/// Verifies that security properties in an app config file are ignored (type safety).
		/// </summary>
		[TestMethod]
		public void LoadApp_SecurityPropertiesInAppFile_AreIgnored()
		{
			var exeDir = Path.Combine( _tempDir, "exe" );
			var workDir = Path.Combine( _tempDir, "work" );
			Directory.CreateDirectory( exeDir );
			Directory.CreateDirectory( workDir );

			var exeJson = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": ["SelectStatement"]
			  },
			  "app": {
			    "server": "exe-server"
			  }
			}
			""";
			var appJson = """
			{
			  "security": {
			    "allowedStatements": ["SelectStatement", "DeleteStatement"]
			  },
			  "app": {
			    "server": "work-server"
			  }
			}
			""";
			File.WriteAllText( Path.Combine( exeDir, "sqlcli.config.jsonc" ), exeJson );
			File.WriteAllText( Path.Combine( workDir, "sqlcli.app.jsonc" ), appJson );

			// Security should come only from exe dir
			var security = ConfigLoader.LoadSecurity( exeDir );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );

			// App should come from working dir
			var app = ConfigLoader.LoadApp( exeDir, workDir );
			Assert.AreEqual( "work-server", app.Server );
		}

		/// <summary>
		/// Verifies that missing app config returns defaults.
		/// </summary>
		[TestMethod]
		public void LoadApp_MissingConfig_ReturnsDefaults()
		{
			var app = ConfigLoader.LoadApp( _tempDir, _tempDir );

			Assert.IsNull( app.Server );
			Assert.IsNull( app.Database );
			Assert.IsFalse( app.TrustServerCertificate );
			Assert.IsFalse( app.NoEncrypt );
		}

		// --- GenerateConfig tests ---

		/// <summary>
		/// Verifies that GenerateConfig writes a combined config file.
		/// </summary>
		[TestMethod]
		public void GenerateConfig_Combined_WritesFile()
		{
			ConfigLoader.GenerateConfig( _tempDir, split: false, force: false );

			var path = Path.Combine( _tempDir, "sqlcli.config.jsonc" );
			Assert.IsTrue( File.Exists( path ) );

			var content = File.ReadAllText( path );
			StringAssert.Contains( content, "filterMode" );
			StringAssert.Contains( content, "allowedStatements" );
			StringAssert.Contains( content, "SelectStatement" );
		}

		/// <summary>
		/// Verifies that GenerateConfig split writes three example files.
		/// </summary>
		[TestMethod]
		public void GenerateConfig_Split_WritesThreeFiles()
		{
			ConfigLoader.GenerateConfig( _tempDir, split: true, force: false );

			Assert.IsTrue( File.Exists( Path.Combine( _tempDir, "sqlcli.security.example.jsonc" ) ) );
			Assert.IsTrue( File.Exists( Path.Combine( _tempDir, "sqlcli.config.example.jsonc" ) ) );
			Assert.IsTrue( File.Exists( Path.Combine( _tempDir, "sqlcli.app.example.jsonc" ) ) );

			var secContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.security.example.jsonc" ) );
			StringAssert.Contains( secContent, "filterMode" );
			StringAssert.Contains( secContent, "allowedStatements" );

			var opsContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.config.example.jsonc" ) );
			StringAssert.Contains( opsContent, "timeout" );
			StringAssert.Contains( opsContent, "maxRows" );

			var appContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.app.example.jsonc" ) );
			StringAssert.Contains( appContent, "server" );
			StringAssert.Contains( appContent, "database" );
		}

		/// <summary>
		/// Verifies that GenerateConfig errors if file exists without force.
		/// </summary>
		[TestMethod]
		public void GenerateConfig_FileExists_ErrorsWithoutForce()
		{
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), "{}" );

			Assert.ThrowsExactly<ConfigException>( () =>
				ConfigLoader.GenerateConfig( _tempDir, split: false, force: false ) );
		}

		/// <summary>
		/// Verifies that GenerateConfig with force overwrites existing file.
		/// </summary>
		[TestMethod]
		public void GenerateConfig_FileExists_OverwritesWithForce()
		{
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), "{}" );

			ConfigLoader.GenerateConfig( _tempDir, split: false, force: true );

			var content = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ) );
			StringAssert.Contains( content, "filterMode" );
		}

		// --- JSONC generator tests ---

		/// <summary>
		/// Verifies that JSONC generator produces valid JSON when comments are stripped.
		/// </summary>
		[TestMethod]
		public void JsoncGenerator_ProducesValidJsonc_DeserializesCorrectly()
		{
			var config = ConfigLoader.CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( config );

			// System.Text.Json can parse JSONC with comment handling
			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			};

			var deserialized = JsonSerializer.Deserialize<SqlCliConfig>( jsonc, options );
			Assert.IsNotNull( deserialized );
			Assert.AreEqual( "whitelist", deserialized.Security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, deserialized.Security.AllowedStatements.ToArray() );
			Assert.AreEqual( 30, deserialized.Operational.Timeout );
			Assert.AreEqual( 100, deserialized.Operational.MaxRows );
		}

		/// <summary>
		/// Verifies that JSONC generator includes comments from attributes.
		/// </summary>
		[TestMethod]
		public void JsoncGenerator_IncludesComments()
		{
			var config = ConfigLoader.CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( config );

			StringAssert.Contains( jsonc, "WARNING TO AI AGENTS" );
			StringAssert.Contains( jsonc, "Security settings" );
			StringAssert.Contains( jsonc, "Operational settings" );
			StringAssert.Contains( jsonc, "Application/environment settings" );
		}

		/// <summary>
		/// Verifies that securityOnly flag produces only the security section.
		/// </summary>
		[TestMethod]
		public void JsoncGenerator_SecurityOnly_ProducesOnlySecuritySection()
		{
			var config = ConfigLoader.CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( config, securityOnly: true );

			StringAssert.Contains( jsonc, "\"security\"" );
			StringAssert.Contains( jsonc, "filterMode" );
			StringAssert.Contains( jsonc, "allowedStatements" );
			StringAssert.Contains( jsonc, "audit" );
			Assert.IsFalse( jsonc.Contains( "\"operational\"" ) );
			Assert.IsFalse( jsonc.Contains( "\"app\"" ) );
		}

		/// <summary>
		/// Verifies that operationalOnly flag produces only the operational section.
		/// </summary>
		[TestMethod]
		public void JsoncGenerator_OperationalOnly_ProducesOnlyOperationalSection()
		{
			var config = ConfigLoader.CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( config, operationalOnly: true );

			Assert.IsFalse( jsonc.Contains( "\"security\"" ) );
			Assert.IsFalse( jsonc.Contains( "\"app\"" ) );
			StringAssert.Contains( jsonc, "\"operational\"" );
			StringAssert.Contains( jsonc, "\"timeout\"" );
			StringAssert.Contains( jsonc, "\"maxRows\"" );
		}

		/// <summary>
		/// Verifies that appOnly flag produces only the app section.
		/// </summary>
		[TestMethod]
		public void JsoncGenerator_AppOnly_ProducesOnlyAppSection()
		{
			var config = ConfigLoader.CreateDefaultConfig();
			var jsonc = JsoncGenerator.Generate( config, appOnly: true );

			Assert.IsFalse( jsonc.Contains( "\"security\"" ) );
			Assert.IsFalse( jsonc.Contains( "\"operational\"" ) );
			StringAssert.Contains( jsonc, "\"app\"" );
			StringAssert.Contains( jsonc, "\"server\"" );
			StringAssert.Contains( jsonc, "\"database\"" );
		}

		/// <summary>
		/// Verifies that the generated config round-trips through deserialization.
		/// </summary>
		[TestMethod]
		public void GeneratedConfig_RoundTrips_ThroughLoadSecurity()
		{
			ConfigLoader.GenerateConfig( _tempDir, split: false, force: false );

			var security = ConfigLoader.LoadSecurity( _tempDir );

			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
			Assert.IsTrue( security.Audit.Enabled );
		}

		/// <summary>
		/// Verifies that the combined config round-trips through all three load methods.
		/// </summary>
		[TestMethod]
		public void GeneratedConfig_RoundTrips_ThroughAllLoads()
		{
			ConfigLoader.GenerateConfig( _tempDir, split: false, force: false );

			var security = ConfigLoader.LoadSecurity( _tempDir );
			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );

			var ops = ConfigLoader.LoadOperational( _tempDir, _tempDir );
			Assert.AreEqual( 30, ops.Timeout );
			Assert.AreEqual( 100, ops.MaxRows );

			var app = ConfigLoader.LoadApp( _tempDir, _tempDir );
			Assert.IsNull( app.Server );
			Assert.IsFalse( app.TrustServerCertificate );
		}

		/// <summary>
		/// Verifies that each generated split file round-trips through deserialization.
		/// </summary>
		[TestMethod]
		public void GeneratedSplitConfig_EachFile_RoundTrips()
		{
			ConfigLoader.GenerateConfig( _tempDir, split: true, force: false );

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true,
				ReadCommentHandling = JsonCommentHandling.Skip,
				AllowTrailingCommas = true
			};

			// Security example file round-trips
			var secContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.security.example.jsonc" ) );
			var secDeserialized = JsonSerializer.Deserialize<SqlCliConfig>( secContent, options );
			Assert.IsNotNull( secDeserialized );
			Assert.AreEqual( "whitelist", secDeserialized.Security.FilterMode );

			// Operational example file round-trips
			var opsContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.config.example.jsonc" ) );
			var opsDeserialized = JsonSerializer.Deserialize<SqlCliConfig>( opsContent, options );
			Assert.IsNotNull( opsDeserialized );
			Assert.AreEqual( 30, opsDeserialized.Operational.Timeout );

			// App example file round-trips
			var appContent = File.ReadAllText( Path.Combine( _tempDir, "sqlcli.app.example.jsonc" ) );
			var appDeserialized = JsonSerializer.Deserialize<SqlCliConfig>( appContent, options );
			Assert.IsNotNull( appDeserialized );
			Assert.IsNull( appDeserialized.App.Server );
		}

		/// <summary>
		/// Verifies that working-dir config with partial operational settings
		/// doesn't reset other exe-dir values to defaults.
		/// </summary>
		[TestMethod]
		public void LoadOperational_PartialWorkingDir_DoesNotResetOtherExeDirValues()
		{
			var exeDir = Path.Combine( _tempDir, "exe" );
			var workDir = Path.Combine( _tempDir, "work" );
			Directory.CreateDirectory( exeDir );
			Directory.CreateDirectory( workDir );

			var exeJson = """
			{
			  "operational": {
			    "timeout": 60,
			    "connectTimeout": 20,
			    "maxRows": 500,
			    "maxFileSize": 100000,
			    "format": "json"
			  }
			}
			""";
			// Working-dir only sets format — other values should remain from exe-dir
			var workJson = """
			{
			  "operational": {
			    "format": "table"
			  }
			}
			""";
			File.WriteAllText( Path.Combine( exeDir, "sqlcli.config.jsonc" ), exeJson );
			File.WriteAllText( Path.Combine( workDir, "sqlcli.config.jsonc" ), workJson );

			var ops = ConfigLoader.LoadOperational( exeDir, workDir );

			Assert.AreEqual( "table", ops.Format, "Format should be overridden by working-dir config." );
			Assert.AreEqual( 60, ops.Timeout, "Timeout should remain from exe-dir (not reset to default 30)." );
			Assert.AreEqual( 20, ops.ConnectTimeout, "ConnectTimeout should remain from exe-dir (not reset to default 15)." );
			Assert.AreEqual( 500, ops.MaxRows, "MaxRows should remain from exe-dir (not reset to default 100)." );
			Assert.AreEqual( 100000L, ops.MaxFileSize, "MaxFileSize should remain from exe-dir (not reset to default 51200)." );
		}

		/// <summary>
		/// Verifies that working-dir app config with partial settings
		/// doesn't reset other exe-dir values (especially booleans).
		/// </summary>
		[TestMethod]
		public void LoadApp_PartialWorkingDir_DoesNotResetExeDirBooleans()
		{
			var exeDir = Path.Combine( _tempDir, "exe" );
			var workDir = Path.Combine( _tempDir, "work" );
			Directory.CreateDirectory( exeDir );
			Directory.CreateDirectory( workDir );

			var exeJson = """
			{
			  "app": {
			    "server": "exe-server",
			    "database": "exe-db",
			    "trustServerCertificate": true,
			    "noEncrypt": true
			  }
			}
			""";
			// Working-dir only sets server — booleans should remain from exe-dir
			var workJson = """
			{
			  "app": {
			    "server": "work-server"
			  }
			}
			""";
			File.WriteAllText( Path.Combine( exeDir, "sqlcli.config.jsonc" ), exeJson );
			File.WriteAllText( Path.Combine( workDir, "sqlcli.config.jsonc" ), workJson );

			var app = ConfigLoader.LoadApp( exeDir, workDir );

			Assert.AreEqual( "work-server", app.Server, "Server should be overridden by working-dir config." );
			Assert.AreEqual( "exe-db", app.Database, "Database should remain from exe-dir." );
			Assert.IsTrue( app.TrustServerCertificate, "TrustServerCertificate should remain true from exe-dir (not reset to default false)." );
			Assert.IsTrue( app.NoEncrypt, "NoEncrypt should remain true from exe-dir (not reset to default false)." );
		}

		/// <summary>
		/// Verifies that the combined config file with nested structure loads all three configs correctly.
		/// </summary>
		[TestMethod]
		public void LoadAll_CombinedNestedConfig_LoadsAllSectionsCorrectly()
		{
			var json = """
			{
			  "security": {
			    "filterMode": "whitelist",
			    "allowedStatements": ["SelectStatement"],
			    "allowedSelectFeatures": [],
			    "audit": { "enabled": true, "path": "sqlcli-audit.log" }
			  },
			  "operational": {
			    "timeout": 45,
			    "connectTimeout": 10,
			    "maxRows": 250,
			    "maxFileSize": 102400,
			    "format": "json"
			  },
			  "app": {
			    "server": "testserver",
			    "database": "testdb",
			    "trustServerCertificate": true,
			    "noEncrypt": false
			  }
			}
			""";
			File.WriteAllText( Path.Combine( _tempDir, "sqlcli.config.jsonc" ), json );

			var security = ConfigLoader.LoadSecurity( _tempDir );
			Assert.AreEqual( "whitelist", security.FilterMode );
			CollectionAssert.AreEqual( new[] { "SelectStatement" }, security.AllowedStatements.ToArray() );
			Assert.IsTrue( security.Audit.Enabled );

			var ops = ConfigLoader.LoadOperational( _tempDir, _tempDir );
			Assert.AreEqual( 45, ops.Timeout );
			Assert.AreEqual( 10, ops.ConnectTimeout );
			Assert.AreEqual( 250, ops.MaxRows );
			Assert.AreEqual( 102400L, ops.MaxFileSize );
			Assert.AreEqual( "json", ops.Format );

			var app = ConfigLoader.LoadApp( _tempDir, _tempDir );
			Assert.AreEqual( "testserver", app.Server );
			Assert.AreEqual( "testdb", app.Database );
			Assert.IsTrue( app.TrustServerCertificate );
			Assert.IsFalse( app.NoEncrypt );
		}
	}
}

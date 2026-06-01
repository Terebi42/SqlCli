using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlCli.Config;

namespace SqlCli.Tests.Config
{
	/// <summary>
	/// Tests for <see cref="MultiSubnetFailoverModeConverter"/> JSON read/write behavior,
	/// including the boolean canonical form, the "auto" string, and alias/error handling.
	/// </summary>
	[TestClass]
	public class MultiSubnetFailoverModeConverterTests
	{
		/// <summary>
		/// Verifies that On serializes to the boolean true.
		/// </summary>
		[TestMethod]
		public void Serialize_On_WritesBooleanTrue()
		{
			Assert.AreEqual( "true", JsonSerializer.Serialize( MultiSubnetFailoverMode.On ) );
		}

		/// <summary>
		/// Verifies that Off serializes to the boolean false.
		/// </summary>
		[TestMethod]
		public void Serialize_Off_WritesBooleanFalse()
		{
			Assert.AreEqual( "false", JsonSerializer.Serialize( MultiSubnetFailoverMode.Off ) );
		}

		/// <summary>
		/// Verifies that Auto serializes to the string "auto".
		/// </summary>
		[TestMethod]
		public void Serialize_Auto_WritesAutoString()
		{
			Assert.AreEqual( "\"auto\"", JsonSerializer.Serialize( MultiSubnetFailoverMode.Auto ) );
		}

		/// <summary>
		/// Verifies that boolean tokens deserialize to On/Off.
		/// </summary>
		[TestMethod]
		public void Deserialize_Booleans_ReturnOnOff()
		{
			Assert.AreEqual( MultiSubnetFailoverMode.On, JsonSerializer.Deserialize<MultiSubnetFailoverMode>( "true" ) );
			Assert.AreEqual( MultiSubnetFailoverMode.Off, JsonSerializer.Deserialize<MultiSubnetFailoverMode>( "false" ) );
		}

		/// <summary>
		/// Verifies that string values (including aliases and mixed case) deserialize correctly.
		/// </summary>
		[TestMethod]
		[DataRow( "\"auto\"", MultiSubnetFailoverMode.Auto )]
		[DataRow( "\"AUTO\"", MultiSubnetFailoverMode.Auto )]
		[DataRow( "\"on\"", MultiSubnetFailoverMode.On )]
		[DataRow( "\"On\"", MultiSubnetFailoverMode.On )]
		[DataRow( "\"true\"", MultiSubnetFailoverMode.On )]
		[DataRow( "\"off\"", MultiSubnetFailoverMode.Off )]
		[DataRow( "\"OFF\"", MultiSubnetFailoverMode.Off )]
		[DataRow( "\"false\"", MultiSubnetFailoverMode.Off )]
		public void Deserialize_Strings_ReturnExpectedMode( string json, MultiSubnetFailoverMode expected )
		{
			Assert.AreEqual( expected, JsonSerializer.Deserialize<MultiSubnetFailoverMode>( json ) );
		}

		/// <summary>
		/// Verifies that an unrecognized string throws a <see cref="JsonException"/>.
		/// </summary>
		[TestMethod]
		public void Deserialize_InvalidString_ThrowsJsonException()
		{
			Assert.ThrowsExactly<JsonException>( () => JsonSerializer.Deserialize<MultiSubnetFailoverMode>( "\"garbage\"" ) );
		}

		/// <summary>
		/// Verifies that a non-boolean, non-string token (e.g. a number) throws a <see cref="JsonException"/>.
		/// </summary>
		[TestMethod]
		public void Deserialize_NumberToken_ThrowsJsonException()
		{
			Assert.ThrowsExactly<JsonException>( () => JsonSerializer.Deserialize<MultiSubnetFailoverMode>( "5" ) );
		}

		/// <summary>
		/// Verifies that each mode round-trips through serialization on an AppConfig.
		/// </summary>
		[TestMethod]
		[DataRow( MultiSubnetFailoverMode.Auto )]
		[DataRow( MultiSubnetFailoverMode.On )]
		[DataRow( MultiSubnetFailoverMode.Off )]
		public void RoundTrip_ThroughAppConfig_PreservesMode( MultiSubnetFailoverMode mode )
		{
			var app = new AppConfig { Server = "s", MultiSubnetFailover = mode };

			var json = JsonSerializer.Serialize( app );
			var restored = JsonSerializer.Deserialize<AppConfig>( json );

			Assert.IsNotNull( restored );
			Assert.AreEqual( mode, restored.MultiSubnetFailover );
		}
	}
}

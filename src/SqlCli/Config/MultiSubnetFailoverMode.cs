using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Controls how <c>MultiSubnetFailover</c> is applied to the connection.
	/// </summary>
	[JsonConverter( typeof( MultiSubnetFailoverModeConverter ) )]
	public enum MultiSubnetFailoverMode
	{
		/// <summary>
		/// Resolve the server name and enable MultiSubnetFailover only when it resolves to
		/// multiple IP addresses (an Availability Group listener / Failover Cluster Instance).
		/// </summary>
		Auto,

		/// <summary>
		/// Always enable MultiSubnetFailover (attempt all listener IPs in parallel).
		/// </summary>
		On,

		/// <summary>
		/// Never enable MultiSubnetFailover (the driver tries IPs sequentially).
		/// </summary>
		Off
	}

	/// <summary>
	/// JSON converter for <see cref="MultiSubnetFailoverMode"/>. The canonical form is the boolean
	/// <c>true</c> (on) / <c>false</c> (off) — matching the other boolean config settings — plus the
	/// string <c>"auto"</c> for the third state. The strings <c>"on"</c>/<c>"off"</c>/<c>"true"</c>/<c>"false"</c>
	/// (case-insensitive) are also accepted on read. On/off are written as booleans; auto as a string.
	/// </summary>
	public sealed class MultiSubnetFailoverModeConverter : JsonConverter<MultiSubnetFailoverMode>
	{
		/// <inheritdoc />
		public override MultiSubnetFailoverMode Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			return reader.TokenType switch
			{
				JsonTokenType.True => MultiSubnetFailoverMode.On,
				JsonTokenType.False => MultiSubnetFailoverMode.Off,
				JsonTokenType.String => Parse( reader.GetString() ),
				_ => throw new JsonException( "Invalid multiSubnetFailover value; expected \"auto\", true, or false." )
			};
		}

		/// <inheritdoc />
		public override void Write( Utf8JsonWriter writer, MultiSubnetFailoverMode value, JsonSerializerOptions options )
		{
			switch ( value )
			{
				case MultiSubnetFailoverMode.On:
					writer.WriteBooleanValue( true );
					break;
				case MultiSubnetFailoverMode.Off:
					writer.WriteBooleanValue( false );
					break;
				default:
					writer.WriteStringValue( "auto" );
					break;
			}
		}

		/// <summary>
		/// Parses a string into a <see cref="MultiSubnetFailoverMode"/>, accepting boolean aliases.
		/// </summary>
		/// <param name="value">The raw string value.</param>
		/// <returns>The parsed mode.</returns>
		internal static MultiSubnetFailoverMode Parse( string value )
		{
			return value?.Trim().ToLowerInvariant() switch
			{
				"auto" => MultiSubnetFailoverMode.Auto,
				"on" or "true" => MultiSubnetFailoverMode.On,
				"off" or "false" => MultiSubnetFailoverMode.Off,
				_ => throw new JsonException( $"Invalid multiSubnetFailover value '{value}'. Expected \"auto\", true, or false." )
			};
		}

		/// <summary>
		/// Renders a mode as its JSON fragment: <c>true</c>/<c>false</c> for on/off, <c>"auto"</c> for auto.
		/// </summary>
		/// <param name="value">The mode.</param>
		/// <returns>"true", "false", or "\"auto\"".</returns>
		internal static string ToJsonFragment( MultiSubnetFailoverMode value )
		{
			return value switch
			{
				MultiSubnetFailoverMode.On => "true",
				MultiSubnetFailoverMode.Off => "false",
				_ => "\"auto\""
			};
		}
	}
}

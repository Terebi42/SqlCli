using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SqlCli.Config
{
	/// <summary>
	/// Generates JSONC configuration files from <see cref="SqlCliConfig"/> using reflection
	/// to read <see cref="ConfigCommentAttribute"/>, <see cref="ConfigSectionAttribute"/>,
	/// and <see cref="SecuritySettingAttribute"/> attributes.
	/// Supports nested objects (<see cref="SecurityConfig"/>, <see cref="OperationalConfig"/>, <see cref="AppConfig"/>).
	/// </summary>
	public static class JsoncGenerator
	{
		private static readonly JsonSerializerOptions SerializeOptions = new()
		{
			WriteIndented = false
		};

		/// <summary>
		/// Generates a JSONC string from a <see cref="SqlCliConfig"/> instance.
		/// </summary>
		/// <param name="config">Configuration instance to serialize.</param>
		/// <param name="securityOnly">If true, only emit the security section.</param>
		/// <param name="operationalOnly">If true, only emit the operational section.</param>
		/// <param name="appOnly">If true, only emit the app section.</param>
		/// <returns>Formatted JSONC string with comments.</returns>
		public static string Generate( SqlCliConfig config, bool securityOnly = false, bool operationalOnly = false, bool appOnly = false )
		{
			var sb = new StringBuilder();

			// Emit class-level comment
			var classComment = typeof( SqlCliConfig ).GetCustomAttribute<ConfigCommentAttribute>();
			if ( classComment is not null )
			{
				EmitBlockComment( sb, classComment.Comment, "" );
				sb.AppendLine();
			}

			sb.AppendLine( "{" );

			var properties = typeof( SqlCliConfig ).GetProperties( BindingFlags.Public | BindingFlags.Instance );
			var filteredProperties = FilterTopLevelProperties( properties, securityOnly, operationalOnly, appOnly );

			for ( var i = 0; i < filteredProperties.Count; i++ )
			{
				var prop = filteredProperties[i];
				var isLast = i == filteredProperties.Count - 1;

				// Section header
				var sectionAttr = prop.GetCustomAttribute<ConfigSectionAttribute>();
				if ( sectionAttr is not null && i > 0 )
				{
					sb.AppendLine();
				}

				// Property comment
				var commentAttr = prop.GetCustomAttribute<ConfigCommentAttribute>();
				if ( commentAttr is not null )
				{
					EmitComment( sb, commentAttr.Comment, "  " );
				}

				// Property value
				var jsonName = GetJsonPropertyName( prop );
				var value = prop.GetValue( config );

				// Check if this is a nested config object
				if ( IsNestedConfigType( prop.PropertyType ) )
				{
					var comma = isLast ? "" : ",";
					sb.AppendLine( $"  \"{jsonName}\": {{" );
					EmitNestedObject( sb, value, prop.PropertyType, "    " );
					sb.AppendLine( $"  }}{comma}" );
				}
				else
				{
					var jsonValue = SerializeValue( value, prop.PropertyType, "  " );
					var comma = isLast ? "" : ",";
					sb.AppendLine( $"  \"{jsonName}\": {jsonValue}{comma}" );
				}
			}

			sb.AppendLine( "}" );
			return sb.ToString();
		}

		/// <summary>
		/// Filters top-level properties based on section flags.
		/// </summary>
		private static List<PropertyInfo> FilterTopLevelProperties( PropertyInfo[] properties, bool securityOnly, bool operationalOnly, bool appOnly )
		{
			var result = new List<PropertyInfo>();

			foreach ( var prop in properties )
			{
				var jsonName = GetJsonPropertyName( prop );

				if ( securityOnly && jsonName != "security" )
				{
					continue;
				}

				if ( operationalOnly && jsonName != "operational" )
				{
					continue;
				}

				if ( appOnly && jsonName != "app" )
				{
					continue;
				}

				result.Add( prop );
			}

			return result;
		}

		/// <summary>
		/// Determines whether a type is a nested config object that should be expanded.
		/// </summary>
		private static bool IsNestedConfigType( Type type )
		{
			return type == typeof( SecurityConfig )
				|| type == typeof( OperationalConfig )
				|| type == typeof( AppConfig );
		}

		/// <summary>
		/// Emits the properties of a nested config object.
		/// </summary>
		private static void EmitNestedObject( StringBuilder sb, object obj, Type type, string indent )
		{
			var properties = type.GetProperties( BindingFlags.Public | BindingFlags.Instance );
			var emittedCount = 0;

			for ( var i = 0; i < properties.Length; i++ )
			{
				var prop = properties[i];
				var isLast = i == properties.Length - 1;

				// Property comment
				var commentAttr = prop.GetCustomAttribute<ConfigCommentAttribute>();
				if ( commentAttr is not null )
				{
					if ( emittedCount > 0 )
					{
						// Blank line before comment blocks for readability (except first)
					}

					EmitComment( sb, commentAttr.Comment, indent );
				}

				var jsonName = GetJsonPropertyName( prop );
				var value = prop.GetValue( obj );
				var jsonValue = SerializeValue( value, prop.PropertyType, indent );
				var comma = isLast ? "" : ",";
				sb.AppendLine( $"{indent}\"{jsonName}\": {jsonValue}{comma}" );

				emittedCount++;
			}
		}

		/// <summary>
		/// Emits a block comment at the top of the file (class-level).
		/// </summary>
		private static void EmitBlockComment( StringBuilder sb, string comment, string indent )
		{
			sb.AppendLine( $"{indent}// ============================================================================" );

			var lines = comment.Split( '\n' );
			foreach ( var line in lines )
			{
				var trimmed = line.TrimEnd( '\r' ).Trim();
				if ( string.IsNullOrEmpty( trimmed ) )
				{
					sb.AppendLine( $"{indent}//" );
				}
				else
				{
					sb.AppendLine( $"{indent}// {trimmed}" );
				}
			}

			sb.AppendLine( $"{indent}// ============================================================================" );
		}

		/// <summary>
		/// Emits a comment with the specified indentation.
		/// </summary>
		private static void EmitComment( StringBuilder sb, string comment, string indent )
		{
			var lines = comment.Split( '\n' );
			foreach ( var line in lines )
			{
				var trimmed = line.TrimEnd( '\r' ).Trim();
				if ( string.IsNullOrEmpty( trimmed ) )
				{
					sb.AppendLine( $"{indent}//" );
				}
				else
				{
					sb.AppendLine( $"{indent}// {trimmed}" );
				}
			}
		}

		/// <summary>
		/// Gets the JSON property name from <see cref="JsonPropertyNameAttribute"/> or falls back to the property name.
		/// </summary>
		private static string GetJsonPropertyName( PropertyInfo prop )
		{
			var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
			return attr?.Name ?? prop.Name;
		}

		/// <summary>
		/// Serializes a property value to a JSON fragment string.
		/// </summary>
		private static string SerializeValue( object value, Type type, string indent )
		{
			if ( value is null )
			{
				return "null";
			}

			if ( type == typeof( string ) )
			{
				return value is string s && s is not null
					? JsonSerializer.Serialize( s, SerializeOptions )
					: "null";
			}

			if ( type == typeof( bool ) )
			{
				return (bool)value ? "true" : "false";
			}

			if ( type == typeof( int ) || type == typeof( long ) )
			{
				return value.ToString();
			}

			if ( type == typeof( AuditConfig ) )
			{
				var audit = (AuditConfig)value;
				return $"{{ \"enabled\": {( audit.Enabled ? "true" : "false" )}, \"path\": {JsonSerializer.Serialize( audit.Path, SerializeOptions )} }}";
			}

			if ( typeof( IList ).IsAssignableFrom( type ) )
			{
				var list = (IList)value;
				if ( list.Count == 0 )
				{
					return "[]";
				}

				var items = new List<string>();
				foreach ( var item in list )
				{
					items.Add( JsonSerializer.Serialize( item, SerializeOptions ) );
				}

				// Format arrays on multiple lines for readability
				var sb = new StringBuilder();
				sb.AppendLine( "[" );
				for ( var i = 0; i < items.Count; i++ )
				{
					var comma = i < items.Count - 1 ? "," : "";
					sb.AppendLine( $"{indent}  {items[i]}{comma}" );
				}

				sb.Append( $"{indent}]" );
				return sb.ToString();
			}

			return JsonSerializer.Serialize( value, SerializeOptions );
		}
	}
}

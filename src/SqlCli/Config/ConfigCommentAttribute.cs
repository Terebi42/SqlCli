using System;

namespace SqlCli.Config
{
	/// <summary>
	/// Specifies a JSONC comment to emit above the annotated class or property
	/// when generating configuration files.
	/// </summary>
	[AttributeUsage( AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false )]
	public class ConfigCommentAttribute : Attribute
	{
		/// <summary>
		/// Gets the comment text.
		/// </summary>
		public string Comment { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigCommentAttribute"/> class.
		/// </summary>
		/// <param name="comment">Comment text to include in the generated JSONC.</param>
		public ConfigCommentAttribute( string comment )
		{
			Comment = comment;
		}
	}

	/// <summary>
	/// Marks a property as the start of a named section in the generated JSONC file.
	/// A section header comment will be emitted above the property.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
	public class ConfigSectionAttribute : Attribute
	{
		/// <summary>
		/// Gets the section name.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConfigSectionAttribute"/> class.
		/// </summary>
		/// <param name="name">Section name for the header comment.</param>
		public ConfigSectionAttribute( string name )
		{
			Name = name;
		}
	}

	/// <summary>
	/// Marks a property as a security-critical setting that can only be loaded from
	/// the config file and cannot be overridden via CLI or environment variables.
	/// </summary>
	[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
	public class SecuritySettingAttribute : Attribute
	{
	}
}

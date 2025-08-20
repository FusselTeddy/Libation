﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

#nullable enable
namespace FileManager.NamingTemplate;

/// <summary>A collection of <see cref="IPropertyTag"/>s registered to a single <see cref="Type"/>.</summary>
public abstract class TagCollection : IEnumerable<ITemplateTag>
{
	/// <summary>The <see cref="ITemplateTag"/>s registered with this <see cref="TagCollection"/> </summary>
	public IEnumerator<ITemplateTag> GetEnumerator() => PropertyTags.Select(p => p.TemplateTag).GetEnumerator();

	/// <summary>The <see cref="ParameterExpression"/> of the <see cref="TagCollection"/>'s TClass type.</summary>
	internal ParameterExpression Parameter { get; }
	protected RegexOptions Options { get; } = RegexOptions.Compiled;
	internal List<IPropertyTag> PropertyTags { get; } = new();

	protected TagCollection(Type classType, bool caseSensative = true)
	{
		Parameter = Expression.Parameter(classType, classType.Name);
		Options |= caseSensative ? RegexOptions.None : RegexOptions.IgnoreCase;
	}

	/// <summary>
	/// Determine if the template string starts with any of the <see cref="TemplateTags"/>s' <see cref="ITemplateTag"/> signatures,
	/// and if it does parse the tag to an <see cref="Expression"/>
	/// </summary>
	/// <param name="templateString">Template string</param>
	/// <param name="exactName">The <paramref name="templateString"/> substring that was matched.</param>
	/// <param name="propertyValue">The <see cref="Expression"/> that returns the <paramref name="propertyTag"/>'s value</param>
	/// <returns>True if the <paramref name="templateString"/> starts with a tag registered in this class.</returns>
	internal bool StartsWith(string templateString, [NotNullWhen(true)] out string? exactName, [NotNullWhen(true)] out IPropertyTag? propertyTag, [NotNullWhen(true)] out Expression? propertyValue)
	{
		foreach (var p in PropertyTags)
		{
			if (p.StartsWith(templateString, out exactName, out propertyValue))
			{
				propertyTag = p;
				return true;
			}
		}

		propertyValue = null;
		propertyTag = null;
		exactName = null;
		return false;
	}

	/// <summary>
	/// Determine if the template string starts with <see cref="IPropertyTag.TemplateTag"/>'s closing tag signature,
	/// and if it does output the matching tag's <see cref="ITemplateTag"/>
	/// </summary>
	/// <param name="templateString">Template string</param>
	/// <param name="exactName">The <paramref name="templateString"/> substring that was matched.</param>
	/// <param name="closingPropertyTag">The registered <see cref="IClosingPropertyTag"/></param>
	/// <returns>True if the <paramref name="templateString"/> starts with this tag.</returns>
	internal bool StartsWithClosing(string templateString, [NotNullWhen(true)] out string? exactName, [NotNullWhen(true)] out IClosingPropertyTag? closingPropertyTag)
	{
		foreach (var  cg in PropertyTags.OfType<IClosingPropertyTag>())
		{
			if (cg.StartsWithClosing(templateString, out exactName, out closingPropertyTag))
				return true;
		}

		closingPropertyTag = null;
		exactName = null;
		return false;
	}

	private protected void AddPropertyTag(IPropertyTag propertyTag)
	{
		if (!PropertyTags.Any(c => c.TemplateTag.TagName == propertyTag.TemplateTag.TagName))
			PropertyTags.Add(propertyTag);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

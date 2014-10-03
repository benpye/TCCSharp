using System;
using System.Collections;
using System.Globalization;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace TCC
{
	internal static class StringInjectExtension
	{
		/// <summary>
		/// Extension method that replaces keys in a string with the values of matching object properties.
		/// <remarks>Uses <see cref="String.Format()"/> internally; custom formats should match those used for that method.</remarks>
		/// </summary>
		/// <param name="formatString">The format string, containing keys like {foo} and {foo:SomeFormat}.</param>
		/// <param name="injectionObject">The object whose properties should be injected in the string</param>
		/// <returns>A version of the formatString string with keys replaced by (formatted) key values.</returns>
		public static string Inject(this string formatString, object injectionObject)
		{
			return formatString.Inject(GetPropertyHash(injectionObject));
		}

		/// <summary>
		/// Extension method that replaces keys in a string with the values of matching dictionary entries.
		/// <remarks>Uses <see cref="String.Format()"/> internally; custom formats should match those used for that method.</remarks>
		/// </summary>
		/// <param name="formatString">The format string, containing keys like {foo} and {foo:SomeFormat}.</param>
		/// <param name="dictionary">An <see cref="IDictionary"/> with keys and values to inject into the string</param>
		/// <returns>A version of the formatString string with dictionary keys replaced by (formatted) key values.</returns>
		public static string Inject(this string formatString, IDictionary dictionary)
		{
			return formatString.Inject(new Hashtable(dictionary));
		}

		/// <summary>
		/// Extension method that replaces keys in a string with the values of matching hashtable entries.
		/// <remarks>Uses <see cref="String.Format()"/> internally; custom formats should match those used for that method.</remarks>
		/// </summary>
		/// <param name="formatString">The format string, containing keys like {foo} and {foo:SomeFormat}.</param>
		/// <param name="attributes">A <see cref="Hashtable"/> with keys and values to inject into the string</param>
		/// <returns>A version of the formatString string with hastable keys replaced by (formatted) key values.</returns>
		public static string Inject(this string formatString, Hashtable attributes)
		{
			string result = formatString;
			if (attributes == null || formatString == null)
				return result;

			foreach (string attributeKey in attributes.Keys)
			{
				result = result.InjectSingleValue(attributeKey, attributes[attributeKey]);
			}
			return result;
		}

		/// <summary>
		/// Replaces all instances of a 'key' (e.g. {foo} or {foo:SomeFormat}) in a string with an optionally formatted value, and returns the result.
		/// </summary>
		/// <param name="formatString">The string containing the key; unformatted ({foo}), or formatted ({foo:SomeFormat})</param>
		/// <param name="key">The key name (foo)</param>
		/// <param name="replacementValue">The replacement value; if null is replaced with an empty string</param>
		/// <returns>The input string with any instances of the key replaced with the replacement value</returns>
		public static string InjectSingleValue(this string formatString, string key, object replacementValue)
		{
			string result = formatString;
			//regex replacement of key with value, where the generic key format is:
			//Regex foo = new Regex("{(foo)(?:}|(?::(.[^}]*)}))");
			Regex attributeRegex = new Regex("{(" + key + ")(?:}|(?::(.[^}]*)}))");  //for key = foo, matches {foo} and {foo:SomeFormat}

			//loop through matches, since each key may be used more than once (and with a different format string)
			foreach (Match m in attributeRegex.Matches(formatString))
			{
				string replacement = m.ToString();
				if (m.Groups[2].Length > 0) //matched {foo:SomeFormat}
				{
					replacement = FormatSymbol((replacementValue ?? String.Empty).ToString(), m.Groups[2].ToString());
				}
				else //matched {foo}
				{
					replacement = (replacementValue ?? String.Empty).ToString();
				}
				//perform replacements, one match at a time
				result = result.Replace(m.ToString(), replacement);  //attributeRegex.Replace(result, replacement, 1);
			}
			return result;

		}

		/// <summary>
		/// Formats the string given using the transforms specified by specifier.
		/// The following are valid for specifier, they can be strung together and are
		/// parsed left to right.
		/// U - Transforms whole string to upper case.
		/// L - Transforms whole string to lower case.
		/// l - Transforms first character to lower case.
		/// u - Transforms first character to upper case.
		/// s - Splits string on every upper case character, adding an _ between.
		/// Any unknown characters will be ignored.
		/// </summary>
		/// <returns>The symbol.</returns>
		/// <param name="symbol">Symbol.</param>
		/// <param name="specifier">Specifier.</param>
		private static string FormatSymbol(string symbol, string specifier)
		{
			foreach (var c in specifier)
			{
				switch (c)
				{
				case 'U':
					symbol = symbol.ToUpper();
					break;
				case 'L':
					symbol = symbol.ToLower();
					break;
				case 'l':
					symbol = Char.ToLower(symbol[0]) + symbol.Substring(1);
					break;
				case 'u':
					symbol = Char.ToUpper(symbol[0]) + symbol.Substring(1);
					break;
				case 's':
					var parts = Regex.Split(symbol, @"(?<!^)(?=[A-Z])");
					symbol = String.Join("_", parts);
					break;
				default:
					break;
				}
			}

			return symbol;
		}


		/// <summary>
		/// Creates a HashTable based on current object state.
		/// <remarks>Copied from the MVCToolkit HtmlExtensionUtility class</remarks>
		/// </summary>
		/// <param name="properties">The object from which to get the properties</param>
		/// <returns>A <see cref="Hashtable"/> containing the object instance's property names and their values</returns>
		private static Hashtable GetPropertyHash(object properties)
		{
			Hashtable values = null;
			if (properties != null)
			{
				values = new Hashtable();
				PropertyDescriptorCollection props = TypeDescriptor.GetProperties(properties);
				foreach (PropertyDescriptor prop in props)
				{
					values.Add(prop.Name, prop.GetValue(properties));
				}
			}
			return values;
		}
	}
}


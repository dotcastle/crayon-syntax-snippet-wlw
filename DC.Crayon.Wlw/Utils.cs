﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace DC.Crayon.Wlw
{
	/// <summary>
	/// Utilities
	/// </summary>
	internal static class Utils
	{
		public static readonly CookieContainer CookieContainer = new CookieContainer();
		public const string UserAgent = "Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/33.0.1750.154 Safari/537.36";
		private delegate bool CheckSplittableDelegate(int i);

		public static string NameToDisplayName(string name, string wordSeparator = null, bool convertToTitleCase = false, IEnumerable<string> nonSplittableTokens = null)
		{
			// Check name
			name = (name ?? string.Empty).Trim();
			if (name.Length == 0)
			{
				return name;
			}
			name = name.Trim();

			// Adjust word separator
			wordSeparator = wordSeparator ?? " ";

			// Non splittable token indices
			List<Point> nonSplittableTokenIndices = new List<Point>();
			if (nonSplittableTokens != null)
			{
				List<string> distinctNonSplittableTokens = new List<string>();
				foreach (string nonSplittableToken in nonSplittableTokens)
				{
					if (distinctNonSplittableTokens.Find(delegate(string str) { return string.Equals(str, nonSplittableToken, StringComparison.OrdinalIgnoreCase); }) == null)
					{
						distinctNonSplittableTokens.Add(nonSplittableToken);
					}
				}
				foreach (string nonSplittableToken in distinctNonSplittableTokens)
				{
					int index = name.IndexOf(nonSplittableToken, StringComparison.OrdinalIgnoreCase);
					if (index >= 0)
					{
						nonSplittableTokenIndices.Add(new Point(index, index + nonSplittableToken.Length - 1));
					}
				}
			}
			CheckSplittableDelegate checkSplittable = delegate(int i)
			{
				bool prvCharInRange = false;
				foreach (Point item in nonSplittableTokenIndices)
				{
					if ((item.X <= (i - 1)) && ((i - 1) <= item.Y))
					{
						prvCharInRange = true;
						break;
					}
				}

				bool thisCharInRange = false;
				foreach (Point item in nonSplittableTokenIndices)
				{
					if ((item.X <= i) && (i <= item.Y))
					{
						thisCharInRange = true;
						break;
					}
				}

				return !(prvCharInRange && thisCharInRange);
			};

			StringBuilder sbBuffer = new StringBuilder();
			int prvCharClass = 0; // 0 => whitespace, 1 => lowercase, 2 => uppercase, 3 => others
			int curCharClass;
			bool isFirstChar = true;
			char[] charArray = name.ToCharArray();

			for (int iChar = 0, nChars = charArray.Length; iChar < nChars; ++iChar)
			{
				char ch = charArray[iChar];

				// Determine character class
				if (char.IsWhiteSpace(ch))
				{
					curCharClass = 0;
				}
				else if (char.IsLetter(ch))
				{
					curCharClass = char.IsLower(ch) ? 1 : 2;
				}
				else
				{
					curCharClass = 3;
				}

				if (isFirstChar)
				{
					// Convert to upper case if required
					if (convertToTitleCase)
					{
						ch = Char.ToUpperInvariant(ch);
					}

					sbBuffer.Append(ch);
					isFirstChar = false;
				}
				else
				{
					// Whitespace
					if (curCharClass == 0)
					{
						// Append only if prv is not whitespace
						if ((prvCharClass != 0) && checkSplittable(iChar))
						{
							sbBuffer.Append(wordSeparator);
						}
					}
					// Lowercase
					else if (curCharClass == 1)
					{
						// Append separator if previous char is other
						if ((prvCharClass == 3) && checkSplittable(iChar))
						{
							sbBuffer.Append(wordSeparator);

							// Convert to upper case if required
							if (convertToTitleCase)
							{
								ch = Char.ToUpperInvariant(ch);
							}
						}
						sbBuffer.Append(ch);
					}
					// Uppercase
					else if (curCharClass == 2)
					{
						// Append separator if previous char is lowercase or other
						if ((prvCharClass == 1) || (prvCharClass == 3))
						{
							if (checkSplittable(iChar))
							{
								sbBuffer.Append(wordSeparator);
							}
						}
						// Append separator if this character is starting of a word
						else if (prvCharClass == 2)
						{
							// Check the next character
							if ((iChar < (nChars - 1))
								&& char.IsLower(charArray[iChar + 1])
								&& checkSplittable(iChar))
							{
								sbBuffer.Append(wordSeparator);
							}
						}
						sbBuffer.Append(ch);
					}
					// Other
					else if (curCharClass == 3)
					{
						// Append separator if previous char is anything other than other and whitespace
						if ((prvCharClass != 0) && (prvCharClass != 3) && checkSplittable(iChar))
						{
							sbBuffer.Append(wordSeparator);
						}
						sbBuffer.Append(ch);
					}
				}
				prvCharClass = curCharClass;
			}

			if (!convertToTitleCase)
			{
				string text = sbBuffer.ToString();
				return text.Substring(0, 1).ToUpperInvariant() + text.Substring(1).ToLowerInvariant();
			}
			return sbBuffer.ToString();
		}

		/// <summary>
		/// Serializes the specified object using a binary serializer
		/// </summary>
		/// <param name="sourceInstance">Instance to be serialized</param>
		/// <param name="surrogateSelector">Surrogate selector</param>
		/// <returns>Byte array</returns>
		public static byte[] BinarySerialize(object sourceInstance, ISurrogateSelector surrogateSelector = null)
		{
			if (sourceInstance == null)
			{
				return null;
			}

			var binFmtr = new BinaryFormatter();
			if (surrogateSelector != null)
			{
				binFmtr.SurrogateSelector = surrogateSelector;
			}
			using (var memStream = new MemoryStream())
			{
				binFmtr.Serialize(memStream, sourceInstance);
				return memStream.ToArray();
			}
		}

		/// <summary>
		/// De-serialize a byte buffer (serialized by BinarySerialize) to its corresponding instance
		/// </summary>
		/// <param name="sourceInstanceData"></param>
		/// <param name="surrogateSelector">Surrogate selector</param>
		/// <returns>De-serialized object</returns>
		public static object BinaryDeserialize(byte[] sourceInstanceData, ISurrogateSelector surrogateSelector = null)
		{
			if ((sourceInstanceData == null) ||
				(sourceInstanceData.Length == 0))
			{
				return null;
			}

			var binFmtr = new BinaryFormatter();
			if (surrogateSelector != null)
			{
				binFmtr.SurrogateSelector = surrogateSelector;
			}
			using (var memStream = new MemoryStream(sourceInstanceData))
			{
				return binFmtr.Deserialize(memStream);
			}
		}

		private static readonly Regex _invalidXmlRegex = new Regex(@"[^\x09\x0A\x0D\x20-\xD7FF\xE000-\xFFFD\x10000-x10FFFF]", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
		public static string SanitizeXml(this string xml)
		{
			if (xml == null)
			{
				return null;
			}

			// From xml spec valid chars:
			// #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]    
			// any Unicode character, excluding the surrogate blocks, FFFE, and FFFF.
			return _invalidXmlRegex.Replace(xml, string.Empty);
		}

		private static Dictionary<Type, XmlSerializer> _xmlSerializers = new Dictionary<Type, XmlSerializer>();
		/// <summary>
		/// Serializes an object instance to xml
		/// </summary>
		/// <param name="sourceInstance">Object to be serialized</param>
		/// <param name="conformanceLevel">Xml document conformance</param>
		/// <param name="ignoreAssemblyVersion">Ignore assembly version</param>
		/// <returns>Xml document data</returns>
		public static string XmlSerialize(this object sourceInstance, ConformanceLevel conformanceLevel = ConformanceLevel.Document, bool ignoreAssemblyVersion = false)
		{
			if (sourceInstance == null)
			{
				return null;
			}

			Type sourceType = sourceInstance.GetType();
			XmlSerializer xmlSerializer;
			lock (_xmlSerializers)
			{
				if (!_xmlSerializers.TryGetValue(sourceType, out xmlSerializer))
				{
					_xmlSerializers[sourceType] = xmlSerializer = new XmlSerializer(sourceType);
				}
			}

			// Type Name
			var typeName = sourceInstance.GetType().AssemblyQualifiedName;
			if (ignoreAssemblyVersion)
			{
				int vIndex = typeName.IndexOf("Version=", StringComparison.OrdinalIgnoreCase);
				if (vIndex < 0)
				{
					vIndex = typeName.IndexOf("Version =", StringComparison.OrdinalIgnoreCase);
				}
				if (vIndex >= 0)
				{
					typeName = typeName.Substring(0, vIndex).TrimEnd(' ', ',');
				}
			}

			StringBuilder sbBuffer = new StringBuilder(typeName + Environment.NewLine);
			conformanceLevel = (conformanceLevel == ConformanceLevel.Auto) ? ConformanceLevel.Document : conformanceLevel;
			using (StringWriter strWriter = new StringWriter(sbBuffer, CultureInfo.CurrentCulture))
			{
				XmlWriterSettings xmlWriterSettings = new XmlWriterSettings
				{
					CheckCharacters = true,
					ConformanceLevel = conformanceLevel,
					CloseOutput = false,
					Encoding = Encoding.UTF8,
					Indent = true,
					IndentChars = "\t",
					NewLineChars = "\n",
					NewLineHandling = NewLineHandling.Entitize,
					NewLineOnAttributes = true,
					OmitXmlDeclaration = (conformanceLevel == ConformanceLevel.Fragment)
				};

				using (XmlWriter xmlWriter = XmlWriter.Create(strWriter, xmlWriterSettings))
				{
					lock (xmlSerializer)
					{
						xmlSerializer.Serialize(xmlWriter, sourceInstance);
					}
				}
			}

			return sbBuffer.ToString();
		}

		/// <summary>
		/// Deserializes xml data (serialized with XmlSerialize) to its corresponding object instance
		/// </summary>
		/// <param name="sourceXml">Source xml data</param>
		/// <param name="conformanceLevel">Xml data conformance level</param>
		/// <returns>Deserialized object instance</returns>
		public static object XmlDeserialize(this string sourceXml, ConformanceLevel conformanceLevel = ConformanceLevel.Document)
		{
			sourceXml = (sourceXml ?? string.Empty).Trim();
			if (sourceXml.Length == 0)
			{
				return null;
			}

			using (StringReader strReader = new StringReader(sourceXml))
			{
				string typeName = strReader.ReadLine();
				Type instanceType = Type.GetType(typeName.Trim(), true, true);

				XmlSerializer xmlSerializer;
				lock (_xmlSerializers)
				{
					if (!_xmlSerializers.TryGetValue(instanceType, out xmlSerializer))
					{
						_xmlSerializers[instanceType] = xmlSerializer = new XmlSerializer(instanceType);
					}
				}

				conformanceLevel = (conformanceLevel == ConformanceLevel.Auto) ? ConformanceLevel.Document : conformanceLevel;
				XmlReaderSettings xmlReaderSettings = new XmlReaderSettings
				{
					CheckCharacters = true,
					ConformanceLevel = conformanceLevel,
					CloseInput = false,
					IgnoreComments = false,
					IgnoreProcessingInstructions = false,
					IgnoreWhitespace = true,
					ValidationFlags = XmlSchemaValidationFlags.None,
					ValidationType = ValidationType.None
				};

				using (XmlReader xmlReader = XmlReader.Create(strReader, xmlReaderSettings))
				{
					lock (xmlSerializer)
					{
						return xmlSerializer.Deserialize(xmlReader);
					}
				}
			}
		}

		/// <summary>
		/// Copies properties with same name from the source to the target
		/// (Quick fix)
		/// </summary>
		/// <param name="source">Source instance</param>
		/// <param name="target">Target instance</param>
		public static void CopyProperties(object source, object target)
		{
			PropertyDescriptor[] srcProps = TypeDescriptor.GetProperties(source).Cast<PropertyDescriptor>().ToArray();
			PropertyDescriptor[] targetProps = TypeDescriptor.GetProperties(target).Cast<PropertyDescriptor>().ToArray();
			foreach (PropertyDescriptor targetProp in targetProps)
			{
				PropertyDescriptor srcProp = srcProps.FirstOrDefault(p => string.Equals(p.Name, targetProp.Name, StringComparison.Ordinal) && (targetProp.PropertyType == p.PropertyType));
				if (srcProp != null)
				{
					targetProp.SetValue(target, srcProp.GetValue(source));
				}
			}
		}

		private static readonly Regex _intralineRegex = new Regex(@"(?<a>\S)\s+(?<b>\S)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
		/// <summary>
		/// Removes white spaces in each line
		/// </summary>
		/// <param name="text">Text</param>
		/// <param name="removeLeadingWhiteSpaces">Whether to remove leading white spaces</param>
		/// <param name="removeIntralineWhiteSpaces">Whether to remove white spaces inside each line</param>
		/// <param name="removeTrailingWhiteSpaces">Whether to remove trailing white spaces</param>
		/// <returns>Trimmed text</returns>
		public static string RemoveLineWhiteSpaces(string text, bool removeLeadingWhiteSpaces, bool removeIntralineWhiteSpaces, bool removeTrailingWhiteSpaces)
		{
			if (text == null)
			{
				return null;
			}

			string[] lines = text.Split(new string[] {"\r\n", "\n", "\r"}, StringSplitOptions.None);
			return string.Join(Environment.NewLine, lines.Select(l =>
			{
				string trimmedLine = l;
				if (removeLeadingWhiteSpaces)
				{
					trimmedLine = trimmedLine.TrimStart();
				}
				if (removeTrailingWhiteSpaces)
				{
					trimmedLine = trimmedLine.TrimEnd();
				}
				if (removeIntralineWhiteSpaces)
				{
					trimmedLine = _intralineRegex.Replace(trimmedLine, m => m.Groups["a"].Value + m.Groups["b"].Value);
				}
				return trimmedLine;
			}).ToArray());
		}

		/// <summary>
		/// Unindents all lines to start from the first column, while keeping the inter-line indentation intact
		/// </summary>
		/// <param name="text">Text to unindent</param>
		/// <param name="tabSpaces">Number of spaces to replace a tab with</param>
		/// <returns>Unindented text</returns>
		public static string UnindentLines(string text, int tabSpaces = 4)
		{
			if (text == null)
			{
				return null;
			}

			// Replace tabs with spaces
			var tabSpacesBuffer = new StringBuilder();
			for (var i = 0; i < Math.Max(tabSpaces, 0); ++i)
			{
				tabSpacesBuffer.Append(" ");
			}
			text = text.Replace("\t", tabSpacesBuffer.ToString());

			// Divide into lines
			string[] lines = text.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
			string[] nonEmptyLines = lines.Where(l => l.Trim().Length > 0).ToArray();
			int minLeadingSpaces = lines.Min(l => (l.Trim().Length == 0) ? int.MaxValue : (l.Length - l.TrimStart().Length));
			if (minLeadingSpaces == 0)
			{
				return text;
			}
			return string.Join(Environment.NewLine, lines.Select(l => l.Substring(Math.Min(minLeadingSpaces, l.Length)).TrimEnd()).ToArray());
		}
	}
}

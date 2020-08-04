﻿namespace ResXManager.Infrastructure
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    using HtmlAgilityPack;

    public static class ExtensionMethods
    {
        /// <summary>
        /// Converts the culture key name to the corresponding culture. The key name is the ieft language tag with an optional '.' prefix.
        /// </summary>
        /// <param name="cultureKeyName">Key name of the culture, optionally prefixed with a '.'.</param>
        /// <returns>
        /// The culture, or <c>null</c> if the key name is empty.
        /// </returns>
        /// <exception cref="InvalidOperationException">Error parsing language:  + cultureKeyName</exception>
        public static CultureInfo? ToCulture(this string? cultureKeyName)
        {
            try
            {
                cultureKeyName = cultureKeyName?.TrimStart('.');

                return cultureKeyName.IsNullOrEmpty() ? null : CultureInfo.GetCultureInfo(cultureKeyName);
            }
            catch (ArgumentException)
            {
            }

            throw new InvalidOperationException("Error parsing language: " + cultureKeyName);
        }

        /// <summary>
        /// Converts the culture key name to the corresponding culture. The key name is the ieft language tag with an optional '.' prefix.
        /// </summary>
        /// <param name="cultureKeyName">Key name of the culture, optionally prefixed with a '.'.</param>
        /// <returns>
        /// The cultureKey, or <c>null</c> if the culture is invalid.
        /// </returns>
        public static CultureKey? ToCultureKey(this string? cultureKeyName)
        {
            try
            {
                cultureKeyName = cultureKeyName?.TrimStart('.');

                return new CultureKey(cultureKeyName.IsNullOrEmpty() ? null : CultureInfo.GetCultureInfo(cultureKeyName));
            }
            catch (ArgumentException)
            {
            }

            return null;
        }

        public static Regex? TryCreateRegex(this string? expression)
        {
            try
            {
                if (!expression.IsNullOrEmpty())
                    return new Regex(expression, RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
            }
            catch
            {
                // invalid expression, ignore...
            }

            return null;
        }

        /// <summary>
        /// Tests whether the string contains HTML
        /// </summary>
        /// <returns>
        /// True if the contains HTML; otherwise false
        /// </returns>
        public static bool ContainsHtml(this string text)
        {
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(text);
            return doc.DocumentNode.Descendants().Any(n => n.NodeType != HtmlNodeType.Text);
        }

    }
}

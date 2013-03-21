﻿using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.XPath;

// source: mvpxml.codeplex.com

namespace Umbraco.Core.Xml
{
	internal static class XmlNodeExtensions
	{
        static XPathNodeIterator Select(string expression, XPathNavigator source, params XPathVariable[] variables)
		{
			var expr = source.Compile(expression);
			var context = new DynamicContext();
			foreach (var variable in variables)
				context.AddVariable(variable.Name, variable.Value);
			expr.SetContext(context);
			return source.Select(expr);
		}

        /// <summary>
        /// Selects a list of XmlNode matching an XPath expression.
        /// </summary>
        /// <param name="source">A source XmlNode.</param>
        /// <param name="expression">An XPath expression.</param>
        /// <param name="variables">A set of XPathVariables.</param>
        /// <returns>The list of XmlNode matching the XPath expression.</returns>
        /// <remarks>
        /// <para>If <param name="variables" /> is <c>null</c>, or is empty, or contains only one single
        /// value which itself is <c>null</c>, then variables are ignored.</para>
        /// <para>The XPath expression should reference variables as <c>$var</c>.</para>
        /// </remarks>
        public static XmlNodeList SelectNodes(this XmlNode source, string expression, IEnumerable<XPathVariable> variables)
        {
            var av = variables == null ? null : variables.ToArray();
            return SelectNodes(source, expression, av);
        }

        /// <summary>
        /// Selects a list of XmlNode matching an XPath expression.
        /// </summary>
        /// <param name="source">A source XmlNode.</param>
        /// <param name="expression">An XPath expression.</param>
        /// <param name="variables">A set of XPathVariables.</param>
        /// <returns>The list of XmlNode matching the XPath expression.</returns>
        /// <remarks>
        /// <para>If <param name="variables" /> is <c>null</c>, or is empty, or contains only one single
        /// value which itself is <c>null</c>, then variables are ignored.</para>
        /// <para>The XPath expression should reference variables as <c>$var</c>.</para>
        /// </remarks>
        public static XmlNodeList SelectNodes(this XmlNode source, string expression, params XPathVariable[] variables)
        {
            if (variables == null || variables.Length == 0 || variables[0] == null)
                return source.SelectNodes(expression);

			var iterator = Select(expression, source.CreateNavigator(), variables);
			return XmlNodeListFactory.CreateNodeList(iterator);
		}

        /// <summary>
        /// Selects the first XmlNode that matches an XPath expression.
        /// </summary>
        /// <param name="source">A source XmlNode.</param>
        /// <param name="expression">An XPath expression.</param>
        /// <param name="variables">A set of XPathVariables.</param>
        /// <returns>The first XmlNode that matches the XPath expression.</returns>
        /// <remarks>
        /// <para>If <param name="variables" /> is <c>null</c>, or is empty, or contains only one single
        /// value which itself is <c>null</c>, then variables are ignored.</para>
        /// <para>The XPath expression should reference variables as <c>$var</c>.</para>
        /// </remarks>
        public static XmlNode SelectSingleNode(this XmlNode source, string expression, IEnumerable<XPathVariable> variables)
        {
            var av = variables == null ? null : variables.ToArray();
            return SelectSingleNode(source, expression, av);
        }
        
        /// <summary>
        /// Selects the first XmlNode that matches an XPath expression.
        /// </summary>
        /// <param name="source">A source XmlNode.</param>
        /// <param name="expression">An XPath expression.</param>
        /// <param name="variables">A set of XPathVariables.</param>
        /// <returns>The first XmlNode that matches the XPath expression.</returns>
        /// <remarks>
        /// <para>If <param name="variables" /> is <c>null</c>, or is empty, or contains only one single
        /// value which itself is <c>null</c>, then variables are ignored.</para>
        /// <para>The XPath expression should reference variables as <c>$var</c>.</para>
        /// </remarks>
        public static XmlNode SelectSingleNode(this XmlNode source, string expression, params XPathVariable[] variables)
		{
            if (variables == null || variables.Length == 0 || variables[0] == null)
                return source.SelectSingleNode(expression);
            
            return SelectNodes(source, expression, variables).Cast<XmlNode>().FirstOrDefault();
		}
	}
}

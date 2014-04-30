﻿using System;
using Umbraco.Core.Models;
using System.Web;

namespace Umbraco.Core.Dynamics
{
	internal class PropertyResult : IPublishedContentProperty, IHtmlString
	{
	    private readonly IPublishedContentProperty _source;
	    private readonly string _alias;
	    private readonly object _value;
        private readonly PropertyResultType _type;

		internal PropertyResult(IPublishedContentProperty source, PropertyResultType type)
        {
    		if (source == null) throw new ArgumentNullException("source");
            
            _type = type;
            _source = source;
        }

        internal PropertyResult(string alias, object value, Guid version, PropertyResultType type)
        {
        	if (alias == null) throw new ArgumentNullException("alias");
        	if (value == null) throw new ArgumentNullException("value");

            _type = type;
            _alias = alias;
			_value = value;
        }

        internal PropertyResultType PropertyType { get { return _type; } }

        public string PropertyTypeAlias { get { return _source == null ? _alias : _source.PropertyTypeAlias; } }
        public object DataValue { get { return _source == null ? _value : _source.DataValue; } }
        public bool HasValue { get { return _source == null || _source.HasValue; } }
        public object Value { get { return _source == null ? _value : _source.Value; } }
        public object XPathValue { get { return Value == null ? null : Value.ToString(); } }

        // implements IHtmlString.ToHtmlString
        public string ToHtmlString()
        {
            var value = Value;
			return value == null ? string.Empty : value.ToString();
        }
    }
}

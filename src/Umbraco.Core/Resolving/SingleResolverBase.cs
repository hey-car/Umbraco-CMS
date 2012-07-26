﻿using System;

namespace Umbraco.Core.Resolving
{
	public abstract class SingleResolverBase<TResolver, TResolved> : ResolverBase<TResolver>
	{
		TResolved _resolved;
		bool _canBeNull;

		protected SingleResolverBase()
			: this(false)
		{ }

		protected SingleResolverBase(TResolved value)
			: this(false)
		{
			_resolved = value;
		}

		protected SingleResolverBase(bool canBeNull)
		{
			_canBeNull = canBeNull;
		}

		protected SingleResolverBase(TResolved value, bool canBeNull)
		{
			_resolved = value;
			_canBeNull = canBeNull;
		}

		protected TResolved Value
		{
			get
			{
				return _resolved;
			}

			set
			{
				Resolution.EnsureNotFrozen();

				if (!_canBeNull && value == null)
					throw new ArgumentNullException("value");
				_resolved = value;
			}
		}
	}
}

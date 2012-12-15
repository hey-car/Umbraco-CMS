using System.Web;
using Umbraco.Core.Configuration;

namespace Umbraco.Core.Persistence
{
	/// <summary>
	/// The default implementation for the IDatabaseFactory
	/// </summary>
	/// <remarks>
	/// If we are running in an http context
	/// it will create on per context, otherwise it will a global singleton object which is NOT thread safe
	/// since we need (at least) a new instance of the database object per thread.
	/// </remarks>
	internal class DefaultDatabaseFactory : DisposableObject, IDatabaseFactory
	{
		private readonly string _connectionString;
		private readonly string _providerName;
		private static volatile UmbracoDatabase _globalInstance = null;
		private static readonly object Locker = new object();

		/// <summary>
		/// Default constructor initialized with the GlobalSettings.UmbracoConnectionName
		/// </summary>
		public DefaultDatabaseFactory() : this(GlobalSettings.UmbracoConnectionName)
		{
			
		}

		/// <summary>
		/// Constructor accepting custom connection string
		/// </summary>
		/// <param name="connectionString"></param>
		public DefaultDatabaseFactory(string connectionString)
		{
			Mandate.ParameterNotNullOrEmpty(connectionString, "connectionString");
			_connectionString = connectionString;
		}

		/// <summary>
		/// Constructor accepting custom connectino string and provider name
		/// </summary>
		/// <param name="connectionString"></param>
		/// <param name="providerName"></param>
		public DefaultDatabaseFactory(string connectionString, string providerName)
		{
			Mandate.ParameterNotNullOrEmpty(connectionString, "connectionString");
			Mandate.ParameterNotNullOrEmpty(providerName, "providerName");
			_connectionString = connectionString;
			_providerName = providerName;
		}

		public UmbracoDatabase CreateDatabase()
		{
			//no http context, create the singleton global object
			if (HttpContext.Current == null)
			{
				if (_globalInstance == null)
				{
					lock (Locker)
					{
						//double check
						if (_globalInstance == null)
						{
							_globalInstance = string.IsNullOrEmpty(_providerName)
								                  ? new UmbracoDatabase(_connectionString)
								                  : new UmbracoDatabase(_connectionString, _providerName);
						}
					}
				}
				return _globalInstance;
			}

			//we have an http context, so only create one per request
			if (!HttpContext.Current.Items.Contains(typeof(DefaultDatabaseFactory)))
			{
				HttpContext.Current.Items.Add(typeof (DefaultDatabaseFactory),
				                              string.IsNullOrEmpty(_providerName)
					                              ? new UmbracoDatabase(_connectionString)
					                              : new UmbracoDatabase(_connectionString, _providerName));
			}
			return (UmbracoDatabase)HttpContext.Current.Items[typeof(DefaultDatabaseFactory)];
		}

		protected override void DisposeResources()
		{
			if (HttpContext.Current == null)
			{
				_globalInstance.Dispose();
			}
			else
			{
				if (HttpContext.Current.Items.Contains(typeof(DefaultDatabaseFactory)))
				{
					((UmbracoDatabase)HttpContext.Current.Items[typeof(DefaultDatabaseFactory)]).Dispose();
				}
			}
		}
	}
}
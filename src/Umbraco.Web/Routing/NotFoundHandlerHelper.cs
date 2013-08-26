﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Xml;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Logging;
using umbraco.interfaces;

namespace Umbraco.Web.Routing
{
	// provides internal access to legacy url -- should get rid of it eventually
	internal class NotFoundHandlerHelper
	{
		const string ContextKey = "Umbraco.Web.Routing.NotFoundHandlerHelper.Url";

        static NotFoundHandlerHelper()
        {
            InitializeNotFoundHandlers();
        }

		public static string GetLegacyUrlForNotFoundHandlers()
		{
			// that's not backward-compatible because when requesting "/foo.aspx"
			// 4.9  : url = "foo.aspx"
			// 4.10 : url = "/foo"
			//return pcr.Uri.AbsolutePath;

			// so we have to run the legacy code for url preparation :-(

			var httpContext = HttpContext.Current;

			if (httpContext == null)
				return "";

			var url = httpContext.Items[ContextKey] as string;
			if (url != null)
				return url;

			// code from requestModule.UmbracoRewrite
			var tmp = httpContext.Request.Path.ToLower();

			// note: requestModule.UmbracoRewrite also did some stripping of &umbPage
			// from the querystring... that was in v3.x to fix some issues with pre-forms
			// auth. Paul Sterling confirmed in jan. 2013 that we can get rid of it.

			// code from requestHandler.cleanUrl
			var root = Core.IO.SystemDirectories.Root.ToLower();
			if (!string.IsNullOrEmpty(root) && tmp.StartsWith(root))
				tmp = tmp.Substring(root.Length);
			tmp = tmp.TrimEnd('/');
			if (tmp == "/default.aspx")
				tmp = string.Empty;
			else if (tmp == root)
				tmp = string.Empty;

			// code from UmbracoDefault.Page_PreInit
			if (tmp != "" && httpContext.Request["umbPageID"] == null)
			{
				var tryIntParse = tmp.Replace("/", "").Replace(".aspx", string.Empty);
				int result;
				if (int.TryParse(tryIntParse, out result))
					tmp = tmp.Replace(".aspx", string.Empty);
			}
			else if (!string.IsNullOrEmpty(httpContext.Request["umbPageID"]))
			{
				int result;
				if (int.TryParse(httpContext.Request["umbPageID"], out result))
				{
					tmp = httpContext.Request["umbPageID"];
				}
			}

			// code from requestHandler.ctor
			if (tmp != "")
				tmp = tmp.Substring(1);

			httpContext.Items[ContextKey] = tmp;
			return tmp;
		}

        private static IEnumerable<Type> _customHandlerTypes;
	    private static Type _customLastChanceHandlerType;

        static void InitializeNotFoundHandlers()
        {
            // initialize handlers
            // create the definition cache

            LogHelper.Debug<NotFoundHandlerHelper>("Registering custom handlers.");

            var customHandlerTypes = new List<Type>();
            Type customLastChanceHandlerType = null;
            var hasLast = false;

            var customHandlers = new XmlDocument();
            customHandlers.Load(Core.IO.IOHelper.MapPath(Core.IO.SystemFiles.NotFoundhandlersConfig));

            foreach (XmlNode n in customHandlers.DocumentElement.SelectNodes("notFound"))
            {
                var assemblyName = n.Attributes.GetNamedItem("assembly").Value;
                var typeName = n.Attributes.GetNamedItem("type").Value;

                var ns = assemblyName;
                var nsAttr = n.Attributes.GetNamedItem("namespace");
                if (nsAttr != null && string.IsNullOrWhiteSpace(nsAttr.Value) == false)
                    ns = nsAttr.Value;

                var lcAttr = n.Attributes.GetNamedItem("last");
                var last = lcAttr != null && lcAttr.Value != null && lcAttr.Value.InvariantEquals("true");

                if (last) // there can only be one last chance handler in the config file
                {
                    if (hasLast)
                        throw new Exception();
                    hasLast = true;
                }

                LogHelper.Debug<NotFoundHandlerHelper>("Registering '{0}.{1},{2}'{3}", () => ns, () => typeName, () => assemblyName,
                    () => last ? " (last)." : ".");

                Type type = null;
                try
                {
                    var assembly = Assembly.Load(new AssemblyName(assemblyName));
                    type = assembly.GetType(ns + "." + typeName);
                }
                catch (Exception e)
                {
                    LogHelper.Error<NotFoundHandlerHelper>("Error registering handler, ignoring.", e);
                }

                if (type == null) continue;

                if (last)
                    _customLastChanceHandlerType = type;
                else
                    customHandlerTypes.Add(type);
            }

            _customHandlerTypes = customHandlerTypes.ToArray();
        }

        public static IEnumerable<INotFoundHandler> GetNotFoundHandlers()
        {
            // instanciate new handlers
            // using definition cache

            var handlers = new List<INotFoundHandler>();

            foreach (var type in _customHandlerTypes)
            {
                try
                {
                    var handler = Activator.CreateInstance(type) as INotFoundHandler;
                    if (handler != null)
                        handlers.Add(handler);
                }
                catch (Exception e)
                {
                    LogHelper.Error<ContentFinderByNotFoundHandlers>(string.Format("Error instanciating handler {0}, ignoring.", type.FullName), e);
                }
            }

            return handlers;
        }

	    public static bool IsNotFoundHandlerEnabled<T>()
	    {
	        return _customHandlerTypes.Contains(typeof (T));
	    }

	    public static INotFoundHandler GetNotFoundLastChanceHandler()
	    {
	        if (_customLastChanceHandlerType == null) return null;

            try
            {
                var handler = Activator.CreateInstance(_customLastChanceHandlerType) as INotFoundHandler;
                if (handler != null)
                    return handler;
            }
            catch (Exception e)
            {
                LogHelper.Error<ContentFinderByNotFoundHandlers>(string.Format("Error instanciating handler {0}, ignoring.", _customLastChanceHandlerType.FullName), e);
            }

            return null;
        }

        public static IContentFinder SubsituteFinder(INotFoundHandler handler)
        {
            IContentFinder finder = null;

            if (handler is global::umbraco.SearchForAlias)
                finder = new ContentFinderByUrlAlias();
            else if (handler is global::umbraco.SearchForProfile)
                finder = new ContentFinderByProfile();
            else if (handler is global::umbraco.SearchForTemplate)
                finder = new ContentFinderByNiceUrlAndTemplate();
            else if (handler is global::umbraco.handle404)
                finder = new ContentFinderByLegacy404();

            return finder;
        }

    }
}

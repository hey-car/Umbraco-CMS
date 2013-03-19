﻿using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Routing;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Routing;
using umbraco;
using umbraco.IO;
using GlobalSettings = Umbraco.Core.Configuration.GlobalSettings;
using UmbracoSettings = Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Web.Configuration;

namespace Umbraco.Web
{
	// also look at IOHelper.ResolveUrlsFromTextString - nightmarish?!

	// context.RewritePath supports ~/ or else must begin with /vdir
	//  Request.RawUrl is still there
	// response.Redirect does?! always remap to /vdir?!

	public class UmbracoModule : IHttpModule
	{
		#region HttpModule event handlers

		/// <summary>
		/// Begins to process a request.
		/// </summary>
		/// <param name="httpContext"></param>
		void BeginRequest(HttpContextBase httpContext)
		{
			// do not process if client-side request
			if (IsClientSideRequest(httpContext.Request.Url))
				return;

			//write the trace output for diagnostics at the end of the request
			httpContext.Trace.Write("UmbracoModule", "Umbraco request begins");

			// ok, process

			// create the LegacyRequestInitializer
			// and initialize legacy stuff
			var legacyRequestInitializer = new LegacyRequestInitializer(httpContext.Request.Url, httpContext);
			legacyRequestInitializer.InitializeRequest();

			// create the UmbracoContext singleton, one per request, and assign
            // NOTE: we assign 'true' to ensure the context is replaced if it is already set (i.e. during app startup)
            UmbracoContext.EnsureContext(httpContext, ApplicationContext.Current, true);
		}

		/// <summary>
		/// Processses the Umbraco Request
		/// </summary>
		/// <param name="httpContext"></param>
		void ProcessRequest(HttpContextBase httpContext)
		{
			// do not process if client-side request
			if (IsClientSideRequest(httpContext.Request.Url))
				return;

			if (UmbracoContext.Current == null)
				throw new InvalidOperationException("The UmbracoContext.Current is null, ProcessRequest cannot proceed unless there is a current UmbracoContext");
			if (UmbracoContext.Current.RoutingContext == null)
				throw new InvalidOperationException("The UmbracoContext.RoutingContext has not been assigned, ProcessRequest cannot proceed unless there is a RoutingContext assigned to the UmbracoContext");

			var umbracoContext = UmbracoContext.Current;		

			// do not process but remap to handler if it is a base rest request
			if (BaseRest.BaseRestHandler.IsBaseRestRequest(umbracoContext.OriginalRequestUrl))
			{
				httpContext.RemapHandler(new BaseRest.BaseRestHandler());
				return;
			}

			// do not process if this request is not a front-end routable page
		    var isRoutableAttempt = EnsureUmbracoRoutablePage(umbracoContext, httpContext);
            //raise event here
            OnRouteAttempt(new RoutableAttemptEventArgs(isRoutableAttempt.Result, umbracoContext, httpContext));
            if (!isRoutableAttempt.Success)
			{
                return;
			}
				

			httpContext.Trace.Write("UmbracoModule", "Umbraco request confirmed");

			// ok, process

			// note: requestModule.UmbracoRewrite also did some stripping of &umbPage
			// from the querystring... that was in v3.x to fix some issues with pre-forms
			// auth. Paul Sterling confirmed in jan. 2013 that we can get rid of it.

			// instanciate, prepare and process the published content request
			// important to use CleanedUmbracoUrl - lowercase path-only version of the current url
			var pcr = new PublishedContentRequest(umbracoContext.CleanedUmbracoUrl, umbracoContext.RoutingContext);
			umbracoContext.PublishedContentRequest = pcr;
			pcr.Prepare();

            // HandleHttpResponseStatus returns a value indicating that the request should
            // not be processed any further, eg because it has been redirect. then, exit.
            if (HandleHttpResponseStatus(httpContext, pcr))
		        return;

            if (!pcr.HasPublishedContent)
				httpContext.RemapHandler(new PublishedContentNotFoundHandler());
			else
				RewriteToUmbracoHandler(httpContext, pcr);
		}

        // returns a value indicating whether redirection took place and the request has
        // been completed - because we don't want to Response.End() here to terminate
        // everything properly.
        internal static bool HandleHttpResponseStatus(HttpContextBase context, PublishedContentRequest pcr)
        {
            var end = false;
            var response = context.Response;

            LogHelper.Debug<UmbracoModule>("Response status: Redirect={0}, Is404={1}, StatusCode={2}",
                () => pcr.IsRedirect ? (pcr.IsRedirectPermanent ? "permanent" : "redirect") : "none",
                () => pcr.Is404 ? "true" : "false", () => pcr.ResponseStatusCode);

            if (pcr.IsRedirect)
            {
                if (pcr.IsRedirectPermanent)
                    response.Redirect(pcr.RedirectUrl, false); // do not end response
                else
                    response.RedirectPermanent(pcr.RedirectUrl, false); // do not end response
                end = true;
            }
            else if (pcr.Is404)
            {
                response.StatusCode = 404;
                response.TrySkipIisCustomErrors = UmbracoSettings.For<WebRouting>().TrySkipIisCustomErrors;
            }

            if (pcr.ResponseStatusCode > 0)
            {
                // set status code -- even for redirects
                response.StatusCode = pcr.ResponseStatusCode;
                response.StatusDescription = pcr.ResponseStatusDescription;
            }
            //if (pcr.IsRedirect)
            //    response.End(); // end response -- kills the thread and does not return!

            if (pcr.IsRedirect)
            {
                response.Flush();
                // bypass everything and directly execute EndRequest event -- but returns
                context.ApplicationInstance.CompleteRequest();
                // though some say that .CompleteRequest() does not properly shutdown the response
                // and the request will hang until the whole code has run... would need to test?
                LogHelper.Debug<UmbracoModule>("Response status: redirecting, complete request now.");
            }

            return end;
        }

		/// <summary>
		/// Checks if the xml cache file needs to be updated/persisted
		/// </summary>
		/// <param name="httpContext"></param>
		/// <remarks>
		/// TODO: This needs an overhaul, see the error report created here:
		///   https://docs.google.com/document/d/1neGE3q3grB4lVJfgID1keWY2v9JYqf-pw75sxUUJiyo/edit		
		/// </remarks>
		void PersistXmlCache(HttpContextBase httpContext)
		{
			if (content.Instance.IsXmlQueuedForPersistenceToFile)
			{
				content.Instance.RemoveXmlFilePersistenceQueue();
				content.Instance.PersistXmlToFile();
			}
		}

		#endregion

		#region Route helper methods

		/// <summary>
		/// This is a performance tweak to check if this is a .css, .js or .ico, .jpg, .jpeg, .png, .gif file request since
		/// .Net will pass these requests through to the module when in integrated mode.
		/// We want to ignore all of these requests immediately.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		internal bool IsClientSideRequest(Uri url)
		{
			var toIgnore = new[] { ".js", ".css", ".ico", ".png", ".jpg", ".jpeg", ".gif" };
			return toIgnore.Any(x => Path.GetExtension(url.LocalPath).InvariantEquals(x));
		}

		/// <summary>
		/// Checks the current request and ensures that it is routable based on the structure of the request and URI
		/// </summary>		
		/// <param name="context"></param>
		/// <param name="httpContext"></param>
		/// <returns></returns>
        internal Attempt<EnsureRoutableOutcome> EnsureUmbracoRoutablePage(UmbracoContext context, HttpContextBase httpContext)
		{
			var uri = context.OriginalRequestUrl;

		    var reason = EnsureRoutableOutcome.IsRoutable;

			// ensure this is a document request
			if (!EnsureDocumentRequest(httpContext, uri))
			{
			    reason = EnsureRoutableOutcome.NotDocumentRequest;
			}
			// ensure Umbraco is ready to serve documents
			else if (!EnsureIsReady(httpContext, uri))
			{
			    reason = EnsureRoutableOutcome.NotReady;
			}                
			// ensure Umbraco is properly configured to serve documents
			else if (!EnsureIsConfigured(httpContext, uri))
            {
                reason = EnsureRoutableOutcome.NotConfigured;
            }                
            // ensure Umbraco has documents to serve
            else if (!EnsureHasContent(context, httpContext))
            {
                reason = EnsureRoutableOutcome.NoContent;
            }

            return new Attempt<EnsureRoutableOutcome>(reason == EnsureRoutableOutcome.IsRoutable, reason);
		}

		/// <summary>
		/// Ensures that the request is a document request (i.e. one that the module should handle)
		/// </summary>
		/// <param name="httpContext"></param>
		/// <param name="uri"></param>
		/// <returns></returns>
		bool EnsureDocumentRequest(HttpContextBase httpContext, Uri uri)
		{
			var maybeDoc = true;
			var lpath = uri.AbsolutePath.ToLowerInvariant();

			// handle directory-urls used for asmx
			// legacy - what's the point really?
			if (/*maybeDoc &&*/ GlobalSettings.UseDirectoryUrls)
			{
				int asmxPos = lpath.IndexOf(".asmx/", StringComparison.OrdinalIgnoreCase);
				if (asmxPos >= 0)
				{
					// use uri.AbsolutePath, not path, 'cos path has been lowercased
					httpContext.RewritePath(uri.AbsolutePath.Substring(0, asmxPos + 5), // filePath
						uri.AbsolutePath.Substring(asmxPos + 5), // pathInfo
						uri.Query.TrimStart('?'));
					maybeDoc = false;
				}
			}

			// a document request should be
			// /foo/bar/nil
			// /foo/bar/nil/
			// /foo/bar/nil.aspx
			// where /foo is not a reserved path

			// if the path contains an extension that is not .aspx
			// then it cannot be a document request
			if (maybeDoc && lpath.Contains('.') && !lpath.EndsWith(".aspx"))
				maybeDoc = false;

			// at that point, either we have no extension, or it is .aspx

			// if the path is reserved then it cannot be a document request
			if (maybeDoc && GlobalSettings.IsReservedPathOrUrl(lpath, httpContext, RouteTable.Routes))
				maybeDoc = false;

			//NOTE: No need to warn, plus if we do we should log the document, as this message doesn't really tell us anything :)
			//if (!maybeDoc)
			//{
			//	LogHelper.Warn<UmbracoModule>("Not a document");
			//}

			return maybeDoc;
		}

		// ensures Umbraco is ready to handle requests
		// if not, set status to 503 and transfer request, and return false
		// if yes, return true
		bool EnsureIsReady(HttpContextBase httpContext, Uri uri)
		{
			var ready = ApplicationContext.Current.IsReady;

			// ensure we are ready
			if (!ready)
			{
				LogHelper.Warn<UmbracoModule>("Umbraco is not ready");

				if (!UmbracoSettings.EnableSplashWhileLoading)
				{
					// let requests pile up and wait for 10s then show the splash anyway
					ready = ApplicationContext.Current.WaitForReady(10 * 1000);
				}

				if (!ready)
				{
					httpContext.Response.StatusCode = 503;

					var bootUrl = UmbracoSettings.BootSplashPage;
					if (string.IsNullOrWhiteSpace(bootUrl))
						bootUrl = "~/config/splashes/booting.aspx";
					httpContext.RewritePath(UriUtility.ToAbsolute(bootUrl) + "?url=" + HttpUtility.UrlEncode(uri.ToString()));

					return false;
				}
			}

			return true;
		}

		// ensures Umbraco has at least one published node
		// if not, rewrites to splash and return false
		// if yes, return true
	    private static bool EnsureHasContent(UmbracoContext context, HttpContextBase httpContext)
		{
            var store = context.ContentCache;
		    if (store.HasContent(context))
		        return true;

            LogHelper.Warn<UmbracoModule>("Umbraco has no content");

			httpContext.Response.StatusCode = 503;

			const string noContentUrl = "~/config/splashes/noNodes.aspx";
			httpContext.RewritePath(UriUtility.ToAbsolute(noContentUrl));

			return false;
		}

		// ensures Umbraco is configured
		// if not, redirect to install and return false
		// if yes, return true
	    private static bool EnsureIsConfigured(HttpContextBase httpContext, Uri uri)
	    {
	        if (ApplicationContext.Current.IsConfigured)
	            return true;

            LogHelper.Warn<UmbracoModule>("Umbraco is not configured");

			var installPath = UriUtility.ToAbsolute(Core.IO.SystemDirectories.Install);
			var installUrl = string.Format("{0}/default.aspx?redir=true&url={1}", installPath, HttpUtility.UrlEncode(uri.ToString()));
			httpContext.Response.Redirect(installUrl, true);
			return false;
		}

		#endregion

		/// <summary>
		/// Rewrites to the correct Umbraco handler, either WebForms or Mvc
		/// </summary>		
		/// <param name="context"></param>
        /// <param name="pcr"> </param>
		private void RewriteToUmbracoHandler(HttpContextBase context, PublishedContentRequest pcr)
		{
			// NOTE: we do not want to use TransferRequest even though many docs say it is better with IIS7, turns out this is
			// not what we need. The purpose of TransferRequest is to ensure that .net processes all of the rules for the newly
			// rewritten url, but this is not what we want!
			// read: http://forums.iis.net/t/1146511.aspx

			string query = pcr.Uri.Query.TrimStart(new[] { '?' });

			string rewritePath;

            if (pcr.RenderingEngine == RenderingEngine.Unknown)
            {
                // Unkwnown means that no template was found. Default to Mvc because Mvc supports hijacking
                // routes which sometimes doesn't require a template since the developer may want full control
                // over the rendering. Can't do it in WebForms, so Mvc it is. And Mvc will also handle what to
                // do if no template or hijacked route is exist.
                pcr.RenderingEngine = RenderingEngine.Mvc;
            }

			switch (pcr.RenderingEngine)
			{
				case RenderingEngine.Mvc:
					// GlobalSettings.Path has already been through IOHelper.ResolveUrl() so it begins with / and vdir (if any)
					rewritePath = GlobalSettings.Path.TrimEnd(new[] { '/' }) + "/RenderMvc";
					// rewrite the path to the path of the handler (i.e. /umbraco/RenderMvc)
					context.RewritePath(rewritePath, "", query, false);

					//if it is MVC we need to do something special, we are not using TransferRequest as this will 
					//require us to rewrite the path with query strings and then reparse the query strings, this would 
					//also mean that we need to handle IIS 7 vs pre-IIS 7 differently. Instead we are just going to create
					//an instance of the UrlRoutingModule and call it's PostResolveRequestCache method. This does:
					// * Looks up the route based on the new rewritten URL
					// * Creates the RequestContext with all route parameters and then executes the correct handler that matches the route
					//we also cannot re-create this functionality because the setter for the HttpContext.Request.RequestContext is internal
					//so really, this is pretty much the only way without using Server.TransferRequest and if we did that, we'd have to rethink
					//a bunch of things!
					var urlRouting = new UrlRoutingModule();
					urlRouting.PostResolveRequestCache(context);
					break;

				case RenderingEngine.WebForms:
					rewritePath = "~/default.aspx";
					// rewrite the path to the path of the handler (i.e. default.aspx)
					context.RewritePath(rewritePath, "", query, false);
					break;

                default:
                    throw new Exception("Invalid RenderingEngine.");
            }
		}

		#region IHttpModule

		/// <summary>
		/// Initialize the module,  this will trigger for each new application 
		/// and there may be more than 1 application per application domain
		/// </summary>
		/// <param name="app"></param>
		public void Init(HttpApplication app)
		{
			app.BeginRequest += (sender, e) =>
				{
					var httpContext = ((HttpApplication)sender).Context;
                    httpContext.Trace.Write("UmbracoModule", "Umbraco request begins");
				    LogHelper.Debug<UmbracoModule>("Begin request: {0}.", () => httpContext.Request.Url);
                    BeginRequest(new HttpContextWrapper(httpContext));
				};

            app.PostResolveRequestCache += (sender, e) =>
				{
					var httpContext = ((HttpApplication)sender).Context;
					ProcessRequest(new HttpContextWrapper(httpContext));
				};

			// used to check if the xml cache file needs to be updated/persisted
			app.PostRequestHandlerExecute += (sender, e) =>
				{
					var httpContext = ((HttpApplication)sender).Context;
					PersistXmlCache(new HttpContextWrapper(httpContext));
				};

			app.EndRequest += (sender, args) =>
				{
					var httpContext = ((HttpApplication)sender).Context;					
					if (UmbracoContext.Current != null && UmbracoContext.Current.IsFrontEndUmbracoRequest)
					{
						//write the trace output for diagnostics at the end of the request
						httpContext.Trace.Write("UmbracoModule", "Umbraco request completed");	
						LogHelper.Debug<UmbracoModule>("Total milliseconds for umbraco request to process: " + DateTime.Now.Subtract(UmbracoContext.Current.ObjectCreated).TotalMilliseconds);
					}

					DisposeHttpContextItems(httpContext);
				};
		}

		public void Dispose()
		{

		}

		#endregion

		/// <summary>
		/// Any object that is in the HttpContext.Items collection that is IDisposable will get disposed on the end of the request
		/// </summary>
		/// <param name="http"></param>
		private static void DisposeHttpContextItems(HttpContext http)
		{
			foreach(var i in http.Items)
			{
				i.DisposeIfDisposable();
			}
		}

        #region Events
        internal static event EventHandler<RoutableAttemptEventArgs> RouteAttempt;
        private void OnRouteAttempt(RoutableAttemptEventArgs args)
        {
            if (RouteAttempt != null)
                RouteAttempt(this, args);
        } 
        #endregion
	}
}

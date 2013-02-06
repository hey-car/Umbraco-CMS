using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Web.Services.Protocols;
using System.Xml;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using umbraco.BusinessLogic;
using umbraco.interfaces;

namespace Umbraco.Web.Cache
{
    /// <summary>
    /// DistrubutedCacheDispatcher is used to handle Umbraco's load balancing.    
    /// </summary>
    /// <remarks>
    /// Distributing calls to all registered load balanced servers, ensuring that content are synced and cached on all servers.
    /// Dispatcher is exendable, so 3rd party services can easily be integrated into the workflow, using the interfaces.ICacheRefresher interface.
    /// 
    /// Dispatcher can refresh/remove content, templates and macros.
    /// Load balanced servers are registered in umbracoSettings.config.
    /// 
    /// UPDATE 2010 02 - Alex Norcliffe - Refactored Dispatcher to support parallel dispatch threads, and preventing failure of whole dispatch
    /// if one node fails. Still needs more work to get it to Enterprise level though but this is for 4.1
    /// </remarks>
    public class DistributedCache
    {

        #region Public constants/Ids

        public const string TemplateRefresherId = "DD12B6A0-14B9-46e8-8800-C154F74047C8";
        public const string PageCacheRefresherId = "27AB3022-3DFA-47b6-9119-5945BC88FD66";
        public const string MemberCacheRefresherId = "E285DF34-ACDC-4226-AE32-C0CB5CF388DA";
        public const string MediaCacheRefresherId = "B29286DD-2D40-4DDB-B325-681226589FEC";
        public const string MacroCacheRefresherId = "7B1E683C-5F34-43dd-803D-9699EA1E98CA";
        
        #endregion

        private readonly string _login;
        private readonly string _password;
        private readonly string _webServicesUrl;
        private static readonly DistributedCache InstanceObject = new DistributedCache();

        /// <summary>
        /// Constructor
        /// </summary>
        private DistributedCache()
        {
            _login = User.GetUser(UmbracoSettings.DistributedCallUser).LoginName;
            _password = User.GetUser(UmbracoSettings.DistributedCallUser).GetPassword();
            _webServicesUrl = IOHelper.ResolveUrl(SystemDirectories.WebServices);
        }
        
        /// <summary>
        /// Singleton
        /// </summary>
        /// <returns></returns>
        public static DistributedCache Instance
        {
            get
            {
                return InstanceObject;    
            }
        }

        /// <summary>
        /// Sends a request to all registered load-balanced servers to refresh node with the specified Id
        /// using the specified ICacheRefresher with the guid factoryGuid.
        /// </summary>
        /// <param name="factoryGuid">The unique identifier of the ICacheRefresher used to refresh the node.</param>
        /// <param name="id">The id of the node.</param>
        public void Refresh(Guid factoryGuid, int id)
        {
            InvokeDispatchMethod(DispatchType.RefreshByNumericId, factoryGuid, id, Guid.Empty);
        }

        /// <summary>
        /// Sends a request to all registered load-balanced servers to refresh the node with the specified guid
        /// using the specified ICacheRefresher with the guid factoryGuid.
        /// </summary>
        /// <param name="factoryGuid">The unique identifier of the ICacheRefresher used to refresh the node.</param>
        /// <param name="id">The guid of the node.</param>
        public void Refresh(Guid factoryGuid, Guid id)
        {
            InvokeDispatchMethod(DispatchType.RefreshByGuid, factoryGuid, 0, id);
        }

        /// <summary>
        /// Sends a request to all registered load-balanced servers to refresh all nodes
        /// using the specified ICacheRefresher with the guid factoryGuid.
        /// </summary>
        /// <param name="factoryGuid">The unique identifier.</param>
        public void RefreshAll(Guid factoryGuid)
        {
            InvokeDispatchMethod(DispatchType.RefreshAll, factoryGuid, 0, Guid.Empty);
        }

        /// <summary>
        /// Sends a request to all registered load-balanced servers to remove the node with the specified id
        /// using the specified ICacheRefresher with the guid factoryGuid.
        /// </summary>
        /// <param name="factoryGuid">The unique identifier.</param>
        /// <param name="id">The id.</param>
        public void Remove(Guid factoryGuid, int id)
        {
            InvokeDispatchMethod(DispatchType.RemoveById, factoryGuid, id, Guid.Empty);
        }

        /// <summary>
        /// Used to invoke the method on an ICacheRefresher instance if we are not currently using distributed calls.
        /// </summary>
        /// <param name="refresher"></param>
        /// <param name="dispatchType"></param>
        /// <param name="numericId"></param>
        /// <param name="guidId"></param>
        private void InvokeMethodOnRefresherInstance(ICacheRefresher refresher, DispatchType dispatchType, int numericId, Guid guidId)
        {
            //if we are not, then just invoke the call on the cache refresher
            switch (dispatchType)
            {
                case DispatchType.RefreshAll:
                    refresher.RefreshAll();
                    break;
                case DispatchType.RefreshByNumericId:
                    refresher.Refresh(numericId);
                    break;
                case DispatchType.RefreshByGuid:
                    refresher.Refresh(guidId);
                    break;
                case DispatchType.RemoveById:
                    refresher.Remove(numericId);
                    break;
            }
        }

        /// <summary>
        /// Invokes the relevant dispatch method.
        /// </summary>
        /// <param name="dispatchType">Type of the dispatch.</param>
        /// <param name="factoryGuid">The factory GUID.</param>
        /// <param name="numericId">The numeric id.</param>
        /// <param name="guidId">The GUID id.</param>
        private void InvokeDispatchMethod(DispatchType dispatchType, Guid factoryGuid, int numericId, Guid guidId)
        {
            //get the refresher, it must be found or else we cannot continue
            var refresher = GetRefresherById(factoryGuid);
            if (refresher == null)
            {
                var ex = new InvalidOperationException(
                    "Could not find an " + typeof(ICacheRefresher).Name + " with the Id " + guidId);
                LogHelper.Error<DistributedCache>("Could not continue with DistributedCache call", ex);
                return;
            }
                
            //Now, check if we are using Distrubuted calls
            if (!UmbracoSettings.UseDistributedCalls)
            {
                //if we are not, then just invoke the call on the cache refresher
                InvokeMethodOnRefresherInstance(refresher, dispatchType, numericId, guidId);
                return;
            }

            //We are using distributed calls, so lets make them...
            try
            {
                using (var cacheRefresher = new CacheRefresherClient())
                {
                    var asyncResultsList = new List<IAsyncResult>();

                    LogStartDispatch();

                    // Go through each configured node submitting a request asynchronously
                    foreach (XmlNode n in GetDistributedNodes())
                    {
                        SetWebServiceUrlFromNode(cacheRefresher, n);

                        // Add the returned WaitHandle to the list for later checking
                        switch (dispatchType)
                        {
                            case DispatchType.RefreshAll:
                                asyncResultsList.Add(cacheRefresher.BeginRefreshAll(factoryGuid, _login, _password, null,
                                                                                    null));
                                break;
                            case DispatchType.RefreshByGuid:
                                asyncResultsList.Add(cacheRefresher.BeginRefreshByGuid(factoryGuid, guidId, _login,
                                                                                       _password, null, null));
                                break;
                            case DispatchType.RefreshByNumericId:
                                asyncResultsList.Add(cacheRefresher.BeginRefreshById(factoryGuid, numericId, _login,
                                                                                     _password, null, null));
                                break;
                            case DispatchType.RemoveById:
                                asyncResultsList.Add(cacheRefresher.BeginRemoveById(factoryGuid, numericId, _login,
                                                                                    _password, null, null));
                                break;
                        }
                    }


                    List<WaitHandle> waitHandlesList;
                    IAsyncResult[] asyncResults = GetAsyncResults(asyncResultsList, out waitHandlesList);

                    int errorCount = 0;

                    // Once for each WaitHandle that we have, wait for a response and log it
                    // We're previously submitted all these requests effectively in parallel and will now retrieve responses on a FIFO basis
                    for (int waitCalls = 0; waitCalls < asyncResults.Length; waitCalls++)
                    {
                        int handleIndex = WaitHandle.WaitAny(waitHandlesList.ToArray(), TimeSpan.FromSeconds(15));

                        try
                        {
                            // Find out if the call succeeded
                            switch (dispatchType)
                            {
                                case DispatchType.RefreshAll:
                                    cacheRefresher.EndRefreshAll(asyncResults[waitCalls]);
                                    break;
                                case DispatchType.RefreshByGuid:
                                    cacheRefresher.EndRefreshByGuid(asyncResults[waitCalls]);
                                    break;
                                case DispatchType.RefreshByNumericId:
                                    cacheRefresher.EndRefreshById(asyncResults[waitCalls]);
                                    break;
                                case DispatchType.RemoveById:
                                    cacheRefresher.EndRemoveById(asyncResults[waitCalls]);
                                    break;
                            }
                        }
                        catch (WebException ex)
                        {
                            LogDispatchNodeError(ex);

                            errorCount++;
                        }
                        catch (Exception ex)
                        {
                            LogDispatchNodeError(ex);

                            errorCount++;
                        }
                    }

                    LogDispatchBatchResult(errorCount);
                }
            }
            catch (Exception ee)
            {
                LogDispatchBatchError(ee);
            }
        }

        private void LogDispatchBatchError(Exception ee)
        {
            LogHelper.Error<DistributedCache>("Error refreshing distributed list", ee);
        }

        private void LogDispatchBatchResult(int errorCount)
        {
            LogHelper.Debug<DistributedCache>(string.Format("Distributed server push completed with {0} nodes reporting an error", errorCount == 0 ? "no" : errorCount.ToString(CultureInfo.InvariantCulture)));
        }

        private void LogDispatchNodeError(Exception ex)
        {
            LogHelper.Error<DistributedCache>("Error refreshing a node in the distributed list", ex);
        }

        private void LogDispatchNodeError(WebException ex)
        {
            string url = (ex.Response != null) ? ex.Response.ResponseUri.ToString() : "invalid url (responseUri null)";
            LogHelper.Error<DistributedCache>("Error refreshing a node in the distributed list, URI attempted: " + url, ex);
        }

        /// <summary>
        /// Sets the web service URL for a CacheRefresher from an XmlNode.
        /// </summary>
        /// <param name="cr">The CacheRefresher.</param>
        /// <param name="n">The XmlNode.</param>
        private void SetWebServiceUrlFromNode(WebClientProtocol cr, XmlNode n)
        {
            string protocol = GlobalSettings.UseSSL ? "https" : "http";
            if (n.Attributes.GetNamedItem("forceProtocol") != null && !String.IsNullOrEmpty(n.Attributes.GetNamedItem("forceProtocol").Value))
                protocol = n.Attributes.GetNamedItem("forceProtocol").Value;
            string domain = XmlHelper.GetNodeValue(n);
            if (n.Attributes.GetNamedItem("forcePortnumber") != null && !String.IsNullOrEmpty(n.Attributes.GetNamedItem("forcePortnumber").Value))
                domain += string.Format(":{0}", n.Attributes.GetNamedItem("forcePortnumber").Value);

            cr.Url = string.Format("{0}://{1}{2}/cacheRefresher.asmx", protocol, domain, _webServicesUrl);
        }

        private static ICacheRefresher GetRefresherById(Guid uniqueIdentifier)
        {
            return CacheRefreshersResolver.Current.GetById(uniqueIdentifier);
        }

        private void LogStartDispatch()
        {
            LogHelper.Info<DistributedCache>("Submitting calls to distributed servers");
        }

        /// <summary>
        /// Gets the node list of DistributionServers from config.
        /// </summary>
        /// <returns></returns>
        private XmlNodeList GetDistributedNodes()
        {
            return UmbracoSettings.DistributionServers.SelectNodes("./server");
        }

        private IAsyncResult[] GetAsyncResults(List<IAsyncResult> asyncResultsList,
                                                      out List<WaitHandle> waitHandlesList)
        {
            IAsyncResult[] asyncResults = asyncResultsList.ToArray();
            waitHandlesList = new List<WaitHandle>();
            foreach (IAsyncResult asyncResult in asyncResults)
            {
                waitHandlesList.Add(asyncResult.AsyncWaitHandle);
            }
            return asyncResults;
        }

        #region Nested type: DispatchType

        private enum DispatchType
        {
            RefreshAll,
            RefreshByNumericId,
            RefreshByGuid,
            RemoveById
        }

        #endregion
    }
}
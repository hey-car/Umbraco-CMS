﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Dynamics;
using Umbraco.Core.Logging;
using umbraco.interfaces;
using System.Collections;
using System.Reflection;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.propertytype;
using umbraco.cms.businesslogic.property;
using umbraco.BusinessLogic;
using umbraco.DataLayer;
using umbraco.cms.businesslogic;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.media;
using umbraco.MacroEngines.Library;
using umbraco.BusinessLogic.Utils;

using Examine;
using Examine.SearchCriteria;
using Examine.LuceneEngine.SearchCriteria;

namespace umbraco.MacroEngines
{
    public class DynamicNode : DynamicObject, INode
    {
		/// <summary>
		/// This callback is used only so we can set it dynamically for use in unit tests
		/// </summary>
		internal static Func<string, string, Guid> GetDataTypeCallback = (docTypeAlias, propertyAlias) =>
			ContentType.GetDataType(docTypeAlias, propertyAlias);

        #region consts
        // these are private readonlys as const can't be Guids
        private readonly Guid DATATYPE_YESNO_GUID = new Guid("38b352c1-e9f8-4fd8-9324-9a2eab06d97a");
        private readonly Guid DATATYPE_TINYMCE_GUID = new Guid("5e9b75ae-face-41c8-b47e-5f4b0fd82f83");
        private readonly Guid DATATYPE_DATETIMEPICKER_GUID = new Guid("b6fb1622-afa5-4bbf-a3cc-d9672a442222");
        private readonly Guid DATATYPE_DATEPICKER_GUID = new Guid("23e93522-3200-44e2-9f29-e61a6fcbb79a");
        //private readonly Guid DATATYPE_INTEGER_GUID = new Guid("1413afcb-d19a-4173-8e9a-68288d2a73b8");
        #endregion

        internal readonly DynamicBackingItem n;

        public DynamicNodeList ownerList;

        public DynamicNode(DynamicBackingItem n)
        {
            if (n != null)
                this.n = n;
            else
                throw new ArgumentNullException("n", "A node must be provided to make a dynamic instance");
        }
        public DynamicNode(int NodeId)
        {
            this.n = new DynamicBackingItem(NodeId);
        }
        public DynamicNode(int NodeId, DynamicBackingItemType ItemType)
        {
            this.n = new DynamicBackingItem(NodeId, ItemType);
        }
        public DynamicNode(string NodeId)
        {
            int DynamicBackingItemId = 0;
            if (int.TryParse(NodeId, out DynamicBackingItemId))
            {
                this.n = new DynamicBackingItem(DynamicBackingItemId);
                return;
            }
            throw new ArgumentException("Cannot instantiate a DynamicNode without an id");
        }
        public DynamicNode(INode Node)
        {
            this.n = new DynamicBackingItem(Node);
        }
        public DynamicNode(object NodeId)
        {
            int DynamicBackingItemId = 0;
            if (int.TryParse(string.Format("{0}", NodeId), out DynamicBackingItemId))
            {
                this.n = new DynamicBackingItem(DynamicBackingItemId);
                return;
            }
            throw new ArgumentException("Cannot instantiate a DynamicNode without an id");
        }
        public DynamicNode()
        {
            //Empty constructor for a special case with Generic Methods
        }

        public DynamicNode Up()
        {
            return DynamicNodeWalker.Up(this);
        }
        public DynamicNode Up(int number)
        {
            return DynamicNodeWalker.Up(this, number);
        }
        public DynamicNode Up(string nodeTypeAlias)
        {
            return DynamicNodeWalker.Up(this, nodeTypeAlias);
        }
        public DynamicNode Down()
        {
            return DynamicNodeWalker.Down(this);
        }
        public DynamicNode Down(int number)
        {
            return DynamicNodeWalker.Down(this, number);
        }
        public DynamicNode Down(string nodeTypeAlias)
        {
            return DynamicNodeWalker.Down(this, nodeTypeAlias);
        }
        public DynamicNode Next()
        {
            return DynamicNodeWalker.Next(this);
        }
        public DynamicNode Next(int number)
        {
            return DynamicNodeWalker.Next(this, number);
        }
        public DynamicNode Next(string nodeTypeAlias)
        {
            return DynamicNodeWalker.Next(this, nodeTypeAlias);
        }

        public DynamicNode Previous()
        {
            return DynamicNodeWalker.Previous(this);
        }
        public DynamicNode Previous(int number)
        {
            return DynamicNodeWalker.Previous(this, number);
        }
        public DynamicNode Previous(string nodeTypeAlias)
        {
            return DynamicNodeWalker.Previous(this, nodeTypeAlias);
        }
        public DynamicNode Sibling(int number)
        {
            return DynamicNodeWalker.Sibling(this, number);
        }
        public DynamicNode Sibling(string nodeTypeAlias)
        {
            return DynamicNodeWalker.Sibling(this, nodeTypeAlias);
        }
        public DynamicNodeList GetChildrenAsList
        {
            get
            {
                List<DynamicBackingItem> children = n.ChildrenAsList;
                //testing
                if (children.Count == 0 && n.Id == 0)
                {
                    return new DynamicNodeList(new List<DynamicBackingItem> { this.n });
                }
                return new DynamicNodeList(n.ChildrenAsList);
            }
        }
        public DynamicNodeList XPath(string xPath)
        {
            //if this DN was initialized with an underlying NodeFactory.Node
            if (n != null && n.Type == DynamicBackingItemType.Content)
            {
                //get the underlying xml content
                XmlDocument doc = umbraco.content.Instance.XmlContent;
                if (doc != null)
                {
                    //get n as a XmlNode (to be used as the context point for the xpath)
                    //rather than just applying the xPath to the root node, this lets us use .. etc from the DynamicNode point


                    //in test mode, n.Id is 0, let this always succeed
                    if (n.Id == 0)
                    {
                        List<DynamicNode> selfList = new List<DynamicNode>() { this };
                        return new DynamicNodeList(selfList);
                    }
                    XmlNode node = doc.SelectSingleNode(string.Format("//*[@id='{0}']", n.Id));
                    if (node != null)
                    {
                        //got the current node (within the XmlContent instance)
                        XmlNodeList nodes = node.SelectNodes(xPath);
                        if (nodes.Count > 0)
                        {
                            //we got some resulting nodes
                            List<NodeFactory.Node> nodeFactoryNodeList = new List<NodeFactory.Node>();
                            //attempt to convert each node in the set to a NodeFactory.Node
                            foreach (XmlNode nodeXmlNode in nodes)
                            {
                                try
                                {
                                    nodeFactoryNodeList.Add(new NodeFactory.Node(nodeXmlNode));
                                }
                                catch (Exception) { } //swallow the exceptions - the returned nodes might not be full nodes, e.g. property
                            }
                            //Wanted to do this, but because we return DynamicNodeList here, the only
                            //common parent class is DynamicObject
                            //maybe some future refactoring will solve this?
                            //if (nodeFactoryNodeList.Count == 0)
                            //{
                            //    //if the xpath resulted in a node set, but none of them could be converted to NodeFactory.Node
                            //    XElement xElement = XElement.Parse(node.OuterXml);
                            //    //return 
                            //    return new DynamicXml(xElement);
                            //}
                            //convert the NodeFactory nodelist to IEnumerable<DynamicNode> and return it as a DynamicNodeList
                            return new DynamicNodeList(nodeFactoryNodeList.ConvertAll(nfNode => new DynamicNode((INode)nfNode)));
                        }
                        else
                        {
                            // XPath returned no nodes, return an empty DynamicNodeList
                            return new DynamicNodeList();
                        }
                    }
                    else
                    {
                        throw new NullReferenceException("Couldn't locate the DynamicNode within the XmlContent");
                    }
                }
                else
                {
                    throw new NullReferenceException("umbraco.content.Instance.XmlContent is null");
                }
            }
            else
            {
                throw new NullReferenceException("DynamicNode wasn't initialized with an underlying NodeFactory.Node");
            }
        }
        
        
        public DynamicNodeList Search(string term, bool useWildCards = true, string searchProvider = null)
        {
            var searcher = Examine.ExamineManager.Instance.DefaultSearchProvider;
            if(!string.IsNullOrEmpty(searchProvider))
                searcher = Examine.ExamineManager.Instance.SearchProviderCollection[searchProvider];

            var t = term.Escape().Value;
            if (useWildCards)
                t = term.MultipleCharacterWildcard().Value;

            string luceneQuery = "+__Path:(" + this.Path.Replace("-", "\\-") + "*) +" + t;
            var crit = searcher.CreateSearchCriteria().RawQuery(luceneQuery);

            return Search(crit, searcher);
        }

        public DynamicNodeList SearchDescendants(string term, bool useWildCards = true, string searchProvider = null)
        {
            return Search(term, useWildCards, searchProvider);
        }

        public DynamicNodeList SearchChildren(string term, bool useWildCards = true, string searchProvider = null)
        {
            var searcher = Examine.ExamineManager.Instance.DefaultSearchProvider;
            if (!string.IsNullOrEmpty(searchProvider))
                searcher = Examine.ExamineManager.Instance.SearchProviderCollection[searchProvider];

            var t = term.Escape().Value;
            if (useWildCards)
                t = term.MultipleCharacterWildcard().Value;

            string luceneQuery = "+parentID:" + this.Id.ToString() + " +" + t;
            var crit = searcher.CreateSearchCriteria().RawQuery(luceneQuery);

            return Search(crit, searcher);
        }


        public DynamicNodeList Search(Examine.SearchCriteria.ISearchCriteria criteria, Examine.Providers.BaseSearchProvider searchProvider = null)
        {
            var s = Examine.ExamineManager.Instance.DefaultSearchProvider;
            if (searchProvider != null)
                s = searchProvider;
            
            var results = s.Search(criteria);
            return ExamineSearchUtill.ConvertSearchResultToDynamicNode(results);
        }


        

        public bool HasProperty(string name)
        {
            if (n != null)
            {
                try
                {
                    IProperty prop = n.GetProperty(name);
                    if (prop == null)
                    {
                        // check for nicer support of Pascal Casing EVEN if alias is camelCasing:
                        if (prop == null && name.Substring(0, 1).ToUpper() == name.Substring(0, 1))
                        {
                            prop = n.GetProperty(name.Substring(0, 1).ToLower() + name.Substring((1)));
                        }
                    }
                    return (prop != null);
                }
                catch (Exception)
                {
                    return false;
                }
            }
            return false;
        }
        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result)
        {
            try
            {
                //Property?
                result = typeof(DynamicNode).InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.GetProperty,
                                                  null,
                                                  this,
                                                  args);
                return true;
            }
            catch (MissingMethodException)
            {
                try
                {
                    //Static or Instance Method?
                    result = typeof(DynamicNode).InvokeMember(binder.Name,
                                                  System.Reflection.BindingFlags.Instance |
                                                  System.Reflection.BindingFlags.Public |
                                                  System.Reflection.BindingFlags.Static |
                                                  System.Reflection.BindingFlags.InvokeMethod,
                                                  null,
                                                  this,
                                                  args);
                    return true;
                }
                catch (MissingMethodException)
                {
                    try
                    {
                        result = ExecuteExtensionMethod(args, binder.Name, false);
                        return true;
                    }
                    catch (TargetInvocationException)
                    {
                        result = new DynamicNull();
                        return true;
                    }

                    catch
                    {
                        result = null;
                        return false;
                    }

                }


            }
            catch
            {
                result = null;
                return false;
            }

        }

        private object ExecuteExtensionMethod(object[] args, string name, bool argsContainsThis)
        {
            object result = null;

            MethodInfo methodToExecute = ExtensionMethodFinder.FindExtensionMethod(typeof(IEnumerable<DynamicNode>), args, name, false);
            if (methodToExecute == null)
            {
                methodToExecute = ExtensionMethodFinder.FindExtensionMethod(typeof(DynamicNodeList), args, name, false);
            }
            if (methodToExecute != null)
            {
                var genericArgs = (new[] { this }).Concat(args);
                result = methodToExecute.Invoke(null, genericArgs.ToArray());
            }
            else
            {
                throw new MissingMethodException();
            }
            if (result != null)
            {
                if (result is IEnumerable<DynamicBackingItem>)
                {
                    result = new DynamicNodeList((IEnumerable<DynamicBackingItem>)result);
                }
                if (result is IEnumerable<DynamicNode>)
                {
                    result = new DynamicNodeList((IEnumerable<DynamicNode>)result);
                }
                if (result is DynamicBackingItem)
                {
                    result = new DynamicNode((DynamicBackingItem)result);
                }
            }
            return result;
        }
        private List<string> GetAncestorOrSelfNodeTypeAlias(DynamicBackingItem node)
        {
            List<string> list = new List<string>();
            if (node != null)
            {
                if (node.Type == DynamicBackingItemType.Content)
                {
                    //find the doctype node, so we can walk it's parent's tree- not the working.parent content tree
                    CMSNode working = ContentType.GetByAlias(node.NodeTypeAlias);
                    while (working != null)
                    {
						//NOTE: I'm not sure if anyone has ever tested this but if you get working.Parent it will return a CMSNode and
						// it will never be castable to a 'ContentType' object
						// pretty sure the only reason why this method works for the one place that it is used is that it returns
						// the current node's alias which is all that is actually requried, this is just added overhead for no 
						// reason

                        if ((working as ContentType) != null)
                        {
                            list.Add((working as ContentType).Alias);
                        }
                        try
                        {
                            working = working.Parent;
                        }
                        catch (ArgumentException)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
            return list;
        }

    	private static Dictionary<System.Tuple<Guid, int>, Type> _razorDataTypeModelTypes = null;
    	private static readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim();

    	internal static Dictionary<System.Tuple<Guid, int>, Type> RazorDataTypeModelTypes
    	{
    		get
    		{
    			using (var l = new UpgradeableReadLock(_locker))
    			{
    				if (_razorDataTypeModelTypes == null)
    				{
    					l.UpgradeToWriteLock();

    					var foundTypes = new Dictionary<System.Tuple<Guid, int>, Type>();

						try
						{
							PluginManager.Current.ResolveRazorDataTypeModels()
								.ToList()
								.ConvertAll(type =>
								{
									var razorDataTypeModelAttributes = type.GetCustomAttributes<RazorDataTypeModel>(true);
									return razorDataTypeModelAttributes.ToList().ConvertAll(razorDataTypeModelAttribute =>
									{
										var g = razorDataTypeModelAttribute.DataTypeEditorId;
										var priority = razorDataTypeModelAttribute.Priority;
										return new KeyValuePair<System.Tuple<Guid, int>, Type>(new System.Tuple<Guid, int>(g, priority), type);
									});
								})
								.SelectMany(item => item)
								.ToList()
								.ForEach(item =>
								{
									System.Tuple<Guid, int> key = item.Key;
									if (!foundTypes.ContainsKey(key))
									{
										foundTypes.Add(key, item.Value);
									}
								});

							//NOTE: We really dont need to log this?
							//var i = 1;
							//foreach (var item in foundTypes)
							//{
							//    HttpContext.Current.Trace.Write(string.Format("{0}/{1}: {2}@{4} => {3}", i, foundTypes.Count, item.Key.Item1, item.Value.FullName, item.Key.Item2));
							//    i++;
							//}

							//there is no error, so set the collection
							_razorDataTypeModelTypes = foundTypes;

						}
						catch (Exception ex)
						{
							LogHelper.Warn<DynamicNode>("Exception occurred while populating cache, will keep RazorDataTypeModelTypes to null so that this error remains visible and you don't end up with an empty cache with silent failure."
								+ string.Format("The exception was {0} and the message was {1}. {2}", ex.GetType().FullName, ex.Message, ex.StackTrace));							
						}

    				}
					return _razorDataTypeModelTypes;
    			}
    		}
    	}

		private static Guid GetDataType(string docTypeAlias, string propertyAlias)
		{
			return GetDataTypeCallback(docTypeAlias, propertyAlias);
		}

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {

            var name = binder.Name;
            result = null; //this will never be returned

            if (name == "ChildrenAsList" || name == "Children")
            {
                result = GetChildrenAsList;
                return true;
            }
            bool propertyExists = false;
            if (n != null)
            {
                bool recursive = false;
                if (name.StartsWith("_"))
                {
                    name = name.Substring(1, name.Length - 1);
                    recursive = true;
                }
                var data = n.GetProperty(name, recursive, out propertyExists);
                // check for nicer support of Pascal Casing EVEN if alias is camelCasing:
                if (data == null && name.Substring(0, 1).ToUpper() == name.Substring(0, 1) && !propertyExists)
                {
                    data = n.GetProperty(name.Substring(0, 1).ToLower() + name.Substring((1)), recursive, out propertyExists);
                }

                if (data != null)
                {
                    result = data.Value;
                    //special casing for true/false properties
                    //int/decimal are handled by ConvertPropertyValueByDataType
                    //fallback is stringT
                    if (n.NodeTypeAlias == null && data.Alias == null)
                    {
                        throw new ArgumentNullException("No node alias or property alias available. Unable to look up the datatype of the property you are trying to fetch.");
                    }

                    //contextAlias is the node which the property data was returned from
                    //Guid dataType = ContentType.GetDataType(data.ContextAlias, data.Alias);					
                	var dataType = GetDataType(data.ContextAlias, data.Alias);
                    
                    var staticMapping = UmbracoSettings.RazorDataTypeModelStaticMapping.FirstOrDefault(mapping =>
                    {
                        return mapping.Applies(dataType, data.ContextAlias, data.Alias);
                    });
                    if (staticMapping != null)
                    {

                        Type dataTypeType = Type.GetType(staticMapping.TypeName);
                        if (dataTypeType != null)
                        {
                            object instance = null;
                            if (TryCreateInstanceRazorDataTypeModel(dataType, dataTypeType, data.Value, out instance))
                            {
                                result = instance;
                                return true;
                            }
                            else
                            {
								LogHelper.Warn<DynamicNode>(string.Format("Failed to create the instance of the model binder"));                            
                            }
                        }
                        else
                        {
							LogHelper.Warn<DynamicNode>(string.Format("staticMapping type name {0} came back as null from Type.GetType; check the casing, assembly presence, assembly framework version, namespace", staticMapping.TypeName));                            
                        }
                    }
                    
                    if (RazorDataTypeModelTypes != null && RazorDataTypeModelTypes.Any(model => model.Key.Item1 == dataType) && dataType != Guid.Empty)
                    {
                        var razorDataTypeModelDefinition = RazorDataTypeModelTypes.Where(model => model.Key.Item1 == dataType).OrderByDescending(model => model.Key.Item2).FirstOrDefault();
                        if (!(razorDataTypeModelDefinition.Equals(default(KeyValuePair<System.Tuple<Guid, int>, Type>))))
                        {
                            Type dataTypeType = razorDataTypeModelDefinition.Value;
                            object instance = null;
                            if (TryCreateInstanceRazorDataTypeModel(dataType, dataTypeType, data.Value, out instance))
                            {
                                result = instance;
                                return true;
                            }
                            else
                            {
								LogHelper.Warn<DynamicNode>(string.Format("Failed to create the instance of the model binder"));
                            }
                        }
                        else
                        {
							LogHelper.Warn<DynamicNode>(string.Format("Could not get the dataTypeType for the RazorDataTypeModel"));                            
                        }
                    }
                    else
                    {
						//NOTE: Do we really want to log this? I'm not sure.
						//if (RazorDataTypeModelTypes == null)
						//{
						//    HttpContext.Current.Trace.Write(string.Format("RazorDataTypeModelTypes is null, probably an exception while building the cache, falling back to ConvertPropertyValueByDataType", dataType));
						//}
						//else
						//{
						//    HttpContext.Current.Trace.Write(string.Format("GUID {0} does not have a DataTypeModel, falling back to ConvertPropertyValueByDataType", dataType));
						//}

                    }

                    //convert the string value to a known type
                    return ConvertPropertyValueByDataType(ref result, name, dataType);

                }

                //check if the alias is that of a child type

                var typeChildren = n.ChildrenAsList;
                if (typeChildren != null)
                {
                    var filteredTypeChildren = typeChildren.Where(x =>
                    {
                        List<string> ancestorAliases = GetAncestorOrSelfNodeTypeAlias(x);
                        if (ancestorAliases == null)
                        {
                            return false;
                        }
                        return ancestorAliases.Any(alias => alias == name || MakePluralName(alias) == name);
                    });
                    if (filteredTypeChildren.Any())
                    {
                        result = new DynamicNodeList(filteredTypeChildren);
                        return true;
                    }

                }

                try
                {
                    result = n.GetType().InvokeMember(binder.Name,
                                                      System.Reflection.BindingFlags.GetProperty |
                                                      System.Reflection.BindingFlags.Instance |
                                                      System.Reflection.BindingFlags.Public,
                                                      null,
                                                      n,
                                                      null);
                    return true;
                }
                catch
                {
                    //result = null;
                    //return false;
                }
            }

            //if property access, type lookup and member invoke all failed
            //at this point, we're going to return null
            //instead, we return a DynamicNull - see comments in that file
            //this will let things like Model.ChildItem work and return nothing instead of crashing
            if (!propertyExists && result == null)
            {
                //.Where explictly checks for this type
                //and will make it false
                //which means backwards equality (&& property != true) will pass
                //forwwards equality (&& property or && property == true) will fail
                result = new DynamicNull();
                return true;
            }
            return true;
        }
        private bool TryCreateInstanceRazorDataTypeModel(Guid dataType, Type dataTypeType, string value, out object result)
        {
            IRazorDataTypeModel razorDataTypeModel = Activator.CreateInstance(dataTypeType, false) as IRazorDataTypeModel;
            if (razorDataTypeModel != null)
            {
                object instance = null;
                if (razorDataTypeModel.Init(n.Id, value, out instance))
                {
                    if (instance == null)
                    {
						LogHelper.Warn<DynamicNode>("razorDataTypeModel successfully instantiated but returned null for instance");
                    }
                	result = instance;
                    return true;
                }
                else
                {
                    if (instance == null)
                    {
						LogHelper.Warn<DynamicNode>("razorDataTypeModel successfully instantiated but returned null for instance");
                    }
                }
            }
            else
            {
				LogHelper.Warn<DynamicNode>(string.Format("DataTypeModel {0} failed to instantiate, perhaps it is lacking a parameterless constructor or doesn't implement IRazorDataTypeModel?", dataTypeType.FullName));
            }
            result = null;
            return false;
        }
        private bool ConvertPropertyValueByDataType(ref object result, string name, Guid dataType)
        {
            //the resulting property is a string, but to support some of the nice linq stuff in .Where
            //we should really check some more types
            string sResult = string.Format("{0}", result).Trim();

            //boolean
            if (dataType == DATATYPE_YESNO_GUID)
            {
                bool parseResult;
                if (string.IsNullOrEmpty(string.Format("{0}", result)))
                {
                    result = false;
                    return true;
                }
                if (Boolean.TryParse(sResult.Replace("1", "true").Replace("0", "false"), out parseResult))
                {
                    result = parseResult;
                    return true;
                }
            }

            ////integer
            ////this will eat csv strings, so only do it if the decimal also includes a decimal seperator (according to the current culture)
            //if (dataType == DATATYPE_INTEGER_GUID)
            //{
            //    int iResult = 0;
            //    if (int.TryParse(sResult, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out iResult))
            //    {
            //        result = iResult;
            //        return true;
            //    }
            //}

            //this will eat csv strings, so only do it if the decimal also includes a decimal seperator (according to the current culture)
            if (sResult.Contains(System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator))
            {
                //decimal
                decimal dResult = 0;
                if (decimal.TryParse(sResult, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out dResult))
                {
                    result = dResult;
                    return true;
                }
            }
            if (dataType == DATATYPE_DATETIMEPICKER_GUID || dataType == DATATYPE_DATEPICKER_GUID)
            {
                //date
                DateTime dtResult = DateTime.MinValue;
                if (DateTime.TryParse(string.Format("{0}", result), out dtResult))
                {
                    result = dtResult;
                    return true;
                }
                else
                {
                    result = new DynamicNull();
                    return true;
                }
            }

            // Rich text editor (return IHtmlString so devs doesn't need to decode html
            if (dataType == DATATYPE_TINYMCE_GUID)
            {
                result = new HtmlString(result.ToString());
                return true;
            }


            if (string.Equals("true", sResult, StringComparison.CurrentCultureIgnoreCase))
            {
                result = true;
                return true;
            }
            if (string.Equals("false", sResult, StringComparison.CurrentCultureIgnoreCase))
            {
                result = false;
                return true;
            }

            if (result != null)
            {
                //a really rough check to see if this may be valid xml
                if (sResult.StartsWith("<") && sResult.EndsWith(">") && sResult.Contains("/"))
                {
                    try
                    {
                        XElement e = XElement.Parse(DynamicXml.StripDashesInElementOrAttributeNames(sResult), LoadOptions.None);
                        if (e != null)
                        {
                            //check that the document element is not one of the disallowed elements
                            //allows RTE to still return as html if it's valid xhtml
                            string documentElement = e.Name.LocalName;
                            if (!UmbracoSettings.NotDynamicXmlDocumentElements.Any(tag =>
                                string.Equals(tag, documentElement, StringComparison.CurrentCultureIgnoreCase)))
                            {
                                result = new DynamicXml(e);
                                return true;
                            }
                            else
                            {
                                //we will just return this as a string
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //we will just return this as a string
                        return true;
                    }

                }
            }

            return true;
        }

        public DynamicNode Media(string propertyAlias)
        {
            if (n != null)
            {
                IProperty prop = n.GetProperty(propertyAlias);
                if (prop != null)
                {
                    int mediaNodeId;
                    if (int.TryParse(prop.Value, out mediaNodeId))
                    {
                        return razorLibrary.Value.MediaById(mediaNodeId);
                    }
                }
                return null;
            }
            return null;
        }
        public bool IsProtected
        {
            get
            {
                if (n != null)
                {
                    return umbraco.library.IsProtected(n.Id, n.Path);
                }
                return false;
            }
        }
        public bool HasAccess
        {
            get
            {
                if (n != null)
                {
                    return umbraco.library.HasAccess(n.Id, n.Path);
                }
                return true;
            }
        }

        public string Media(string propertyAlias, string mediaPropertyAlias)
        {
            if (n != null)
            {
                IProperty prop = n.GetProperty(propertyAlias);
                if (prop == null && propertyAlias.Substring(0, 1).ToUpper() == propertyAlias.Substring(0, 1))
                {
                    prop = n.GetProperty(propertyAlias.Substring(0, 1).ToLower() + propertyAlias.Substring((1)));
                }
                if (prop != null)
                {
                    int mediaNodeId;
                    if (int.TryParse(prop.Value, out mediaNodeId))
                    {
                        umbraco.cms.businesslogic.media.Media media = new cms.businesslogic.media.Media(mediaNodeId);
                        if (media != null)
                        {
                            Property mprop = media.getProperty(mediaPropertyAlias);
                            // check for nicer support of Pascal Casing EVEN if alias is camelCasing:
                            if (prop == null && mediaPropertyAlias.Substring(0, 1).ToUpper() == mediaPropertyAlias.Substring(0, 1))
                            {
                                mprop = media.getProperty(mediaPropertyAlias.Substring(0, 1).ToLower() + mediaPropertyAlias.Substring((1)));
                            }
                            if (mprop != null)
                            {
                                return string.Format("{0}", mprop.Value);
                            }
                        }
                    }
                }
            }
            return null;
        }

        //this is from SqlMetal and just makes it a bit of fun to allow pluralisation
        private static string MakePluralName(string name)
        {
            if ((name.EndsWith("x", StringComparison.OrdinalIgnoreCase) || name.EndsWith("ch", StringComparison.OrdinalIgnoreCase)) || (name.EndsWith("ss", StringComparison.OrdinalIgnoreCase) || name.EndsWith("sh", StringComparison.OrdinalIgnoreCase)))
            {
                name = name + "es";
                return name;
            }
            if ((name.EndsWith("y", StringComparison.OrdinalIgnoreCase) && (name.Length > 1)) && !IsVowel(name[name.Length - 2]))
            {
                name = name.Remove(name.Length - 1, 1);
                name = name + "ies";
                return name;
            }
            if (!name.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                name = name + "s";
            }
            return name;
        }

        private static bool IsVowel(char c)
        {
            switch (c)
            {
                case 'O':
                case 'U':
                case 'Y':
                case 'A':
                case 'E':
                case 'I':
                case 'o':
                case 'u':
                case 'y':
                case 'a':
                case 'e':
                case 'i':
                    return true;
            }
            return false;
        }
        public DynamicNode AncestorOrSelf()
        {
            return AncestorOrSelf(node => node.Level == 1);
        }
        public DynamicNode AncestorOrSelf(int level)
        {
            return AncestorOrSelf(node => node.Level == level);
        }
        public DynamicNode AncestorOrSelf(string nodeTypeAlias)
        {
            return AncestorOrSelf(node => node.NodeTypeAlias == nodeTypeAlias);
        }
        public DynamicNode AncestorOrSelf(Func<DynamicNode, bool> func)
        {
            var node = this;
            while (node != null)
            {
                if (func(node)) return node;
                DynamicNode parent = node.Parent;
                if (parent != null)
                {
                    if (this != parent)
                    {
                        node = parent;
                    }
                    else
                    {
                        return node;
                    }
                }
                else
                {
                    return null;
                }
            }
            return node;
        }
        public DynamicNodeList AncestorsOrSelf(Func<DynamicNode, bool> func)
        {
            List<DynamicNode> ancestorList = new List<DynamicNode>();
            var node = this;
            ancestorList.Add(node);
            while (node != null)
            {
                if (node.Level == 1) break;
                DynamicNode parent = node.Parent;
                if (parent != null)
                {
                    if (this != parent)
                    {
                        node = parent;
                        if (func(node))
                        {
                            ancestorList.Add(node);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            ancestorList.Reverse();
            return new DynamicNodeList(ancestorList);
        }
        public DynamicNodeList AncestorsOrSelf()
        {
            return AncestorsOrSelf(n => true);
        }
        public DynamicNodeList AncestorsOrSelf(string nodeTypeAlias)
        {
            return AncestorsOrSelf(n => n.NodeTypeAlias == nodeTypeAlias);
        }
        public DynamicNodeList AncestorsOrSelf(int level)
        {
            return AncestorsOrSelf(n => n.Level <= level);
        }
        public DynamicNodeList Descendants(string nodeTypeAlias)
        {
            return Descendants(p => p.NodeTypeAlias == nodeTypeAlias);
        }
        public DynamicNodeList Descendants(int level)
        {
            return Descendants(p => p.Level >= level);
        }
        public DynamicNodeList Descendants()
        {
            return Descendants(n => true);
        }
        public DynamicNodeList Descendants(Func<DynamicBackingItem, bool> func)
        {
            var flattenedNodes = this.n.ChildrenAsList.Map(func, (DynamicBackingItem n) => { return n.ChildrenAsList; });
            return new DynamicNodeList(flattenedNodes.ToList().ConvertAll(DynamicBackingItem => new DynamicNode(DynamicBackingItem)));
        }
        public DynamicNodeList DescendantsOrSelf(int level)
        {
            return DescendantsOrSelf(p => p.Level >= level);
        }
        public DynamicNodeList DescendantsOrSelf(string nodeTypeAlias)
        {
            return DescendantsOrSelf(p => p.NodeTypeAlias == nodeTypeAlias);
        }
        public DynamicNodeList DescendantsOrSelf()
        {
            return DescendantsOrSelf(p => true);
        }
        public DynamicNodeList DescendantsOrSelf(Func<DynamicBackingItem, bool> func)
        {
            if (this.n != null)
            {
                var thisNode = new List<DynamicBackingItem>();
                if (func(this.n))
                {
                    thisNode.Add(this.n);
                }
                var flattenedNodes = this.n.ChildrenAsList.Map(func, (DynamicBackingItem n) => { return n.ChildrenAsList; });
                return new DynamicNodeList(thisNode.Concat(flattenedNodes).ToList().ConvertAll(DynamicBackingItem => new DynamicNode(DynamicBackingItem)));
            }
            return new DynamicNodeList(new List<DynamicBackingItem>());
        }
        public DynamicNodeList Ancestors(int level)
        {
            return Ancestors(n => n.Level <= level);
        }
        public DynamicNodeList Ancestors(string nodeTypeAlias)
        {
            return Ancestors(n => n.NodeTypeAlias == nodeTypeAlias);
        }
        public DynamicNodeList Ancestors()
        {
            return Ancestors(n => true);
        }
        public DynamicNodeList Ancestors(Func<DynamicNode, bool> func)
        {
            List<DynamicNode> ancestorList = new List<DynamicNode>();
            var node = this;
            while (node != null)
            {
                if (node.Level == 1) break;
                DynamicNode parent = node.Parent;
                if (parent != null)
                {
                    if (this != parent)
                    {
                        node = parent;
                        if (func(node))
                        {
                            ancestorList.Add(node);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
            ancestorList.Reverse();
            return new DynamicNodeList(ancestorList);
        }
        public DynamicNode Parent
        {
            get
            {
                if (n == null)
                {
                    return null;
                }
                if (n.Parent != null)
                {
                    return new DynamicNode(n.Parent);
                }
                if (n != null && n.Id == 0)
                {
                    return this;
                }
                return null;
            }
        }

        private Lazy<RazorLibraryCore> razorLibrary = new Lazy<RazorLibraryCore>(() =>
        {
            return new RazorLibraryCore(null);
        });
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNode NodeById(int Id)
        {
            var node = razorLibrary.Value.NodeById(Id);
            if (node is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return node;
        }
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNode NodeById(string Id)
        {
            var node = razorLibrary.Value.NodeById(Id);
            if (node is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return node;
        }
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNode NodeById(object Id)
        {
            var node = razorLibrary.Value.NodeById(Id);
            if (node is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return node;
        }
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNodeList NodesById(List<object> Ids)
        {
            return razorLibrary.Value.NodesById(Ids);
        }
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNodeList NodesById(List<int> Ids)
        {
            return razorLibrary.Value.NodesById(Ids);
        }
        [Obsolete("@Model.NodeById is obsolute, use @Library.NodeById")]
        public DynamicNodeList NodesById(params object[] Ids)
        {
            return razorLibrary.Value.NodesById(Ids);
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNode MediaById(int Id)
        {
            var media = razorLibrary.Value.MediaById(Id);
            if (media is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return media;
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNode MediaById(string Id)
        {
            var media = razorLibrary.Value.MediaById(Id);
            if (media is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return media;
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNode MediaById(object Id)
        {
            var media = razorLibrary.Value.MediaById(Id);
            if (media is DynamicNull)
            {
                return new DynamicNode(0);
            }
            return media;
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNodeList MediaById(List<object> Ids)
        {
            return razorLibrary.Value.MediaById(Ids);
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNodeList MediaById(List<int> Ids)
        {
            return razorLibrary.Value.MediaById(Ids);
        }
        [Obsolete("@Model.MediaById is obsolute, use @Library.MediaById")]
        public DynamicNodeList MediaById(params object[] Ids)
        {
            return razorLibrary.Value.MediaById(Ids);
        }

        public int template
        {
            get { if (n == null) return 0; return n.template; }
        }

        public int SortOrder
        {
            get { if (n == null) return 0; return n.SortOrder; }
        }

        public string Name
        {
            get { if (n == null) return null; return n.Name; }
        }
        public bool Visible
        {
            get
            {
                if (n == null) return true;
                IProperty umbracoNaviHide = n.GetProperty("umbracoNaviHide");
                if (umbracoNaviHide != null)
                {
                    return umbracoNaviHide.Value != "1";
                }
                return true;
            }
        }
        public string Url
        {
            get { if (n == null) return null; return n.Url; }
        }

        public string UrlName
        {
            get { if (n == null) return null; return n.UrlName; }
        }

        public string NodeTypeAlias
        {
            get { if (n == null) return null; return n.NodeTypeAlias; }
        }

        public string WriterName
        {
            get { if (n == null) return null; return n.WriterName; }
        }

        public string CreatorName
        {
            get { if (n == null) return null; return n.CreatorName; }
        }

        public int WriterID
        {
            get { if (n == null) return 0; return n.WriterID; }
        }

        public int CreatorID
        {
            get { if (n == null) return 0; return n.CreatorID; }
        }

        public string Path
        {
            get { return n.Path; }
        }

        public DateTime CreateDate
        {
            get { if (n == null) return DateTime.MinValue; return n.CreateDate; }
        }
        public int Id
        {
            get { if (n == null) return 0; return n.Id; }
        }

        public DateTime UpdateDate
        {
            get { if (n == null) return DateTime.MinValue; return n.UpdateDate; }
        }

        public Guid Version
        {
            get { if (n == null) return Guid.Empty; return n.Version; }
        }

        public string NiceUrl
        {
            get { if (n == null) return null; return n.NiceUrl; }
        }

        public int Level
        {
            get { if (n == null) return 0; return n.Level; }
        }

        public List<IProperty> PropertiesAsList
        {
            get { if (n == null) return null; return n.PropertiesAsList; }
        }

        public DynamicNodeList ChildrenAsList
        {
            get
            {
            	return GetChildrenAsList;
            	//if (n == null) return null; return n.ChildrenAsList;
            }
        }

	    public DynamicNodeList Children
	    {
		    get { return ChildrenAsList; }
	    }

        public IProperty GetProperty(string alias)
        {
            if (n == null) return null;
            return n.GetProperty(alias);
        }
        public IProperty GetProperty(string alias, bool recursive)
        {
            if (!recursive) return GetProperty(alias);
            if (n == null) return null;
            DynamicBackingItem context = this.n;
            IProperty prop = n.GetProperty(alias);
            while (prop == null)
            {
                context = context.Parent;
                if (context == null) break;
                prop = context.GetProperty(alias);
            }
            if (prop != null)
            {
                return prop;
            }
            return null;
        }
        public string GetPropertyValue(string alias)
        {
            return GetPropertyValue(alias, null);
        }
        public string GetPropertyValue(string alias, string fallback)
        {
            var prop = GetProperty(alias);
            if (prop != null) return prop.Value;
            return fallback;
        }
        public string GetPropertyValue(string alias, bool recursive)
        {
            return GetPropertyValue(alias, recursive, null);
        }
        public string GetPropertyValue(string alias, bool recursive, string fallback)
        {
            var prop = GetProperty(alias, recursive);
            if (prop != null) return prop.Value;
            return fallback;
        }
        public System.Data.DataTable ChildrenAsTable()
        {
            if (n == null) return null;
            return n.ChildrenAsTable();
        }

        public System.Data.DataTable ChildrenAsTable(string nodeTypeAliasFilter)
        {
            if (n == null) return null;
            return n.ChildrenAsTable(nodeTypeAliasFilter);
        }
        public bool IsNull(string alias, bool recursive)
        {
            var prop = GetProperty(alias, recursive);
            if (prop == null) return true;
            return (prop as PropertyResult).IsNull();
        }
        public bool IsNull(string alias)
        {
            return IsNull(alias, false);
        }
        public bool HasValue(string alias)
        {
            return HasValue(alias, false);
        }
        public bool HasValue(string alias, bool recursive)
        {
            var prop = GetProperty(alias, recursive);
            if (prop == null) return false;
            return (prop as PropertyResult).HasValue();
        }
        public IHtmlString HasValue(string alias, string valueIfTrue, string valueIfFalse)
        {
            return HasValue(alias, false) ? new HtmlString(valueIfTrue) : new HtmlString(valueIfFalse);
        }
        public IHtmlString HasValue(string alias, bool recursive, string valueIfTrue, string valueIfFalse)
        {
            return HasValue(alias, recursive) ? new HtmlString(valueIfTrue) : new HtmlString(valueIfFalse);
        }
        public IHtmlString HasValue(string alias, string valueIfTrue)
        {
            return HasValue(alias, false) ? new HtmlString(valueIfTrue) : new HtmlString(string.Empty);
        }
        public IHtmlString HasValue(string alias, bool recursive, string valueIfTrue)
        {
            return HasValue(alias, recursive) ? new HtmlString(valueIfTrue) : new HtmlString(string.Empty);
        }
        public int Position()
        {
            return this.Index();
        }
        public int Index()
        {
            if (this.ownerList == null && this.Parent != null)
            {
                var list = this.Parent.ChildrenAsList.Select(n => new DynamicNode(n));
                this.ownerList = new DynamicNodeList(list);
            }
            if (this.ownerList != null)
            {
                List<DynamicNode> container = this.ownerList.Items.ToList();
                int currentIndex = container.FindIndex(n => n.Id == this.Id);
                if (currentIndex != -1)
                {
                    return currentIndex;
                }
                else
                {
                    throw new IndexOutOfRangeException(string.Format("Node {0} belongs to a DynamicNodeList but could not retrieve the index for it's position in the list", this.Id));
                }
            }
            else
            {
                throw new ArgumentNullException(string.Format("Node {0} has been orphaned and doesn't belong to a DynamicNodeList", this.Id));
            }
        }
        public bool IsFirst()
        {
            return IsHelper(n => n.Index() == 0);
        }
        public HtmlString IsFirst(string valueIfTrue)
        {
            return IsHelper(n => n.Index() == 0, valueIfTrue);
        }
        public HtmlString IsFirst(string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Index() == 0, valueIfTrue, valueIfFalse);
        }
        public bool IsNotFirst()
        {
            return !IsHelper(n => n.Index() == 0);
        }
        public HtmlString IsNotFirst(string valueIfTrue)
        {
            return IsHelper(n => n.Index() != 0, valueIfTrue);
        }
        public HtmlString IsNotFirst(string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Index() != 0, valueIfTrue, valueIfFalse);
        }
        public bool IsPosition(int index)
        {
            if (this.ownerList == null)
            {
                return false;
            }
            return IsHelper(n => n.Index() == index);
        }
        public HtmlString IsPosition(int index, string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            return IsHelper(n => n.Index() == index, valueIfTrue);
        }
        public HtmlString IsPosition(int index, string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            return IsHelper(n => n.Index() == index, valueIfTrue, valueIfFalse);
        }
        public bool IsModZero(int modulus)
        {
            if (this.ownerList == null)
            {
                return false;
            }
            return IsHelper(n => n.Index() % modulus == 0);
        }
        public HtmlString IsModZero(int modulus, string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            return IsHelper(n => n.Index() % modulus == 0, valueIfTrue);
        }
        public HtmlString IsModZero(int modulus, string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            return IsHelper(n => n.Index() % modulus == 0, valueIfTrue, valueIfFalse);
        }

        public bool IsNotModZero(int modulus)
        {
            if (this.ownerList == null)
            {
                return false;
            }
            return IsHelper(n => n.Index() % modulus != 0);
        }
        public HtmlString IsNotModZero(int modulus, string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            return IsHelper(n => n.Index() % modulus != 0, valueIfTrue);
        }
        public HtmlString IsNotModZero(int modulus, string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            return IsHelper(n => n.Index() % modulus != 0, valueIfTrue, valueIfFalse);
        }
        public bool IsNotPosition(int index)
        {
            if (this.ownerList == null)
            {
                return false;
            }
            return !IsHelper(n => n.Index() == index);
        }
        public HtmlString IsNotPosition(int index, string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            return IsHelper(n => n.Index() != index, valueIfTrue);
        }
        public HtmlString IsNotPosition(int index, string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            return IsHelper(n => n.Index() != index, valueIfTrue, valueIfFalse);
        }
        public bool IsLast()
        {
            if (this.ownerList == null)
            {
                return false;
            }
            int count = this.ownerList.Items.Count;
            return IsHelper(n => n.Index() == count - 1);
        }
        public HtmlString IsLast(string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            int count = this.ownerList.Items.Count;
            return IsHelper(n => n.Index() == count - 1, valueIfTrue);
        }
        public HtmlString IsLast(string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            int count = this.ownerList.Items.Count;
            return IsHelper(n => n.Index() == count - 1, valueIfTrue, valueIfFalse);
        }
        public bool IsNotLast()
        {
            if (this.ownerList == null)
            {
                return false;
            }
            int count = this.ownerList.Items.Count;
            return !IsHelper(n => n.Index() == count - 1);
        }
        public HtmlString IsNotLast(string valueIfTrue)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(string.Empty);
            }
            int count = this.ownerList.Items.Count;
            return IsHelper(n => n.Index() != count - 1, valueIfTrue);
        }
        public HtmlString IsNotLast(string valueIfTrue, string valueIfFalse)
        {
            if (this.ownerList == null)
            {
                return new HtmlString(valueIfFalse);
            }
            int count = this.ownerList.Items.Count;
            return IsHelper(n => n.Index() != count - 1, valueIfTrue, valueIfFalse);
        }
        public bool IsEven()
        {
            return IsHelper(n => n.Index() % 2 == 0);
        }
        public HtmlString IsEven(string valueIfTrue)
        {
            return IsHelper(n => n.Index() % 2 == 0, valueIfTrue);
        }
        public HtmlString IsEven(string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Index() % 2 == 0, valueIfTrue, valueIfFalse);
        }
        public bool IsOdd()
        {
            return IsHelper(n => n.Index() % 2 == 1);
        }
        public HtmlString IsOdd(string valueIfTrue)
        {
            return IsHelper(n => n.Index() % 2 == 1, valueIfTrue);
        }
        public HtmlString IsOdd(string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Index() % 2 == 1, valueIfTrue, valueIfFalse);
        }
        public bool IsEqual(DynamicNode other)
        {
            return IsHelper(n => n.Id == other.Id);
        }
        public HtmlString IsEqual(DynamicNode other, string valueIfTrue)
        {
            return IsHelper(n => n.Id == other.Id, valueIfTrue);
        }
        public HtmlString IsEqual(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Id == other.Id, valueIfTrue, valueIfFalse);
        }
        public bool IsNotEqual(DynamicNode other)
        {
            return IsHelper(n => n.Id != other.Id);
        }
        public HtmlString IsNotEqual(DynamicNode other, string valueIfTrue)
        {
            return IsHelper(n => n.Id != other.Id, valueIfTrue);
        }
        public HtmlString IsNotEqual(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            return IsHelper(n => n.Id != other.Id, valueIfTrue, valueIfFalse);
        }
        public bool IsDescendant(DynamicNode other)
        {
            var ancestors = this.Ancestors();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null);
        }
        public HtmlString IsDescendant(DynamicNode other, string valueIfTrue)
        {
            var ancestors = this.Ancestors();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null, valueIfTrue);
        }
        public HtmlString IsDescendant(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            var ancestors = this.Ancestors();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null, valueIfTrue, valueIfFalse);
        }
        public bool IsDescendantOrSelf(DynamicNode other)
        {
            var ancestors = this.AncestorsOrSelf();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null);
        }
        public HtmlString IsDescendantOrSelf(DynamicNode other, string valueIfTrue)
        {
            var ancestors = this.AncestorsOrSelf();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null, valueIfTrue);
        }
        public HtmlString IsDescendantOrSelf(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            var ancestors = this.AncestorsOrSelf();
            return IsHelper(n => ancestors.Items.Find(ancestor => ancestor.Id == other.Id) != null, valueIfTrue, valueIfFalse);
        }
        public bool IsAncestor(DynamicNode other)
        {
            var descendants = this.Descendants();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null);
        }
        public HtmlString IsAncestor(DynamicNode other, string valueIfTrue)
        {
            var descendants = this.Descendants();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null, valueIfTrue);
        }
        public HtmlString IsAncestor(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            var descendants = this.Descendants();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null, valueIfTrue, valueIfFalse);
        }
        public bool IsAncestorOrSelf(DynamicNode other)
        {
            var descendants = this.DescendantsOrSelf();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null);
        }
        public HtmlString IsAncestorOrSelf(DynamicNode other, string valueIfTrue)
        {
            var descendants = this.DescendantsOrSelf();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null, valueIfTrue);
        }
        public HtmlString IsAncestorOrSelf(DynamicNode other, string valueIfTrue, string valueIfFalse)
        {
            var descendants = this.DescendantsOrSelf();
            return IsHelper(n => descendants.Items.Find(descendant => descendant.Id == other.Id) != null, valueIfTrue, valueIfFalse);
        }
        public bool IsHelper(Func<DynamicNode, bool> test)
        {
            return test(this);
        }
        public HtmlString IsHelper(Func<DynamicNode, bool> test, string valueIfTrue)
        {
            return IsHelper(test, valueIfTrue, string.Empty);
        }
        public HtmlString IsHelper(Func<DynamicNode, bool> test, string valueIfTrue, string valueIfFalse)
        {
            return test(this) ? new HtmlString(valueIfTrue) : new HtmlString(valueIfFalse);
        }
        public HtmlString Where(string predicate, string valueIfTrue)
        {
            return Where(predicate, valueIfTrue, string.Empty);
        }
        public HtmlString Where(string predicate, string valueIfTrue, string valueIfFalse)
        {
            if (Where(predicate))
            {
                return new HtmlString(valueIfTrue);
            }
            return new HtmlString(valueIfFalse);
        }
        public bool Where(string predicate)
        {
            //Totally gonna cheat here
            var dynamicNodeList = new DynamicNodeList();
            dynamicNodeList.Add(this);
            var filtered = dynamicNodeList.Where<DynamicNode>(predicate);
            if (filtered.Count() == 1)
            {
                //this node matches the predicate
                return true;
            }
            return false;
        }

		#region Explicit INode implementation
		INode INode.Parent
		{
			get { return Parent; }
		}

		int INode.Id
		{
			get { return Id; }
		}

		int INode.template
		{
			get { return template; }
		}

		int INode.SortOrder
		{
			get { return SortOrder; }
		}

		string INode.Name
		{
			get { return Name; }
		}

		string INode.Url
		{
			get { return Url; }
		}

		string INode.UrlName
		{
			get { return UrlName; }
		}

		string INode.NodeTypeAlias
		{
			get { return NodeTypeAlias; }
		}

		string INode.WriterName
		{
			get { return WriterName; }
		}

		string INode.CreatorName
		{
			get { return CreatorName; }
		}

		int INode.WriterID
		{
			get { return WriterID; }
		}

		int INode.CreatorID
		{
			get { return CreatorID; }
		}

		string INode.Path
		{
			get { return Path; }
		}

		DateTime INode.CreateDate
		{
			get { return CreateDate; }
		}

		DateTime INode.UpdateDate
		{
			get { return UpdateDate; }
		}

		Guid INode.Version
		{
			get { return Version; }
		}

		string INode.NiceUrl
		{
			get { return NiceUrl; }
		}

		int INode.Level
		{
			get { return Level; }
		}

		List<IProperty> INode.PropertiesAsList
		{
			get { return PropertiesAsList; }
		}

		List<INode> INode.ChildrenAsList
		{
			get { return new List<INode>(ChildrenAsList.Select(x => x).ToList()); }
		}

		IProperty INode.GetProperty(string Alias)
		{
			return GetProperty(Alias);
		}

		IProperty INode.GetProperty(string Alias, out bool propertyExists)
		{
			var p = GetProperty(Alias, false);
			propertyExists = p != null;
			return p;
		}

		System.Data.DataTable INode.ChildrenAsTable()
		{
			return ChildrenAsTable();
		}

		System.Data.DataTable INode.ChildrenAsTable(string nodeTypeAliasFilter)
		{
			return ChildrenAsTable(nodeTypeAliasFilter);
		} 
		#endregion
	}
}

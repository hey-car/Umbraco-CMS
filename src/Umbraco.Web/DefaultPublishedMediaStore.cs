using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Xml.XPath;
using Examine;
using Umbraco.Core;
using Umbraco.Core.Dynamics;
using Umbraco.Core.Models;
using umbraco;
using umbraco.cms.businesslogic;

namespace Umbraco.Web
{
	/// <summary>
	/// An IPublishedMediaStore that first checks for the media in Examine, and then reverts to the database
	/// </summary>
	/// <remarks>
	/// NOTE: In the future if we want to properly cache all media this class can be extended or replaced when these classes/interfaces are exposed publicly.
	/// </remarks>
	internal class DefaultPublishedMediaStore : IPublishedMediaStore
	{
		public virtual IPublishedContent GetDocumentById(UmbracoContext umbracoContext, int nodeId)
		{
			return GetUmbracoMedia(nodeId);
		}

		public IEnumerable<IPublishedContent> GetRootDocuments(UmbracoContext umbracoContext)
		{
			var rootMedia = global::umbraco.cms.businesslogic.media.Media.GetRootMedias();
			var result = new List<IPublishedContent>();
			//TODO: need to get a ConvertFromMedia method but we'll just use this for now.
			foreach (var media in rootMedia
				.Select(m => global::umbraco.library.GetMedia(m.Id, true))
				.Where(media => media != null && media.Current != null))
			{
				media.MoveNext();
				result.Add(ConvertFromXPathNavigator(media.Current));
			}
			return result;
		}

		private IPublishedContent GetUmbracoMedia(int id)
		{

			try
			{
				//first check in Examine as this is WAY faster
				var criteria = ExamineManager.Instance
					.SearchProviderCollection["InternalSearcher"]
					.CreateSearchCriteria("media");
				var filter = criteria.Id(id);
				var results = ExamineManager
					.Instance.SearchProviderCollection["InternalSearcher"]
					.Search(filter.Compile());
				if (results.Any())
				{
					return ConvertFromSearchResult(results.First());
				}
			}
			catch (FileNotFoundException)
			{
				//Currently examine is throwing FileNotFound exceptions when we have a loadbalanced filestore and a node is published in umbraco
				//See this thread: http://examine.cdodeplex.com/discussions/264341
				//Catch the exception here for the time being, and just fallback to GetMedia
				//TODO: Need to fix examine in LB scenarios!
			}

			var media = global::umbraco.library.GetMedia(id, true);
			if (media != null && media.Current != null)
			{
				media.MoveNext();
				return ConvertFromXPathNavigator(media.Current);
			}

			return null;
		}

		internal IPublishedContent ConvertFromSearchResult(SearchResult searchResult)
		{
			//TODO: Some fields will not be included, that just the way it is unfortunatley until this is fixed:
			// http://examine.codeplex.com/workitem/10350

			var values = new Dictionary<string, string>(searchResult.Fields);
			//we need to ensure some fields exist, because of the above issue
			if (!new []{"template", "templateId"}.Any(values.ContainsKey)) 
				values.Add("template", 0.ToString());
			if (!new[] { "sortOrder" }.Any(values.ContainsKey))
				values.Add("sortOrder", 0.ToString());
			if (!new[] { "urlName" }.Any(values.ContainsKey))
				values.Add("urlName", "");
			if (!new[] { "nodeType" }.Any(values.ContainsKey))
				values.Add("nodeType", 0.ToString());
			if (!new[] { "creatorName" }.Any(values.ContainsKey))
				values.Add("creatorName", "");
			if (!new[] { "writerID" }.Any(values.ContainsKey))
				values.Add("writerID", 0.ToString());
			if (!new[] { "creatorID" }.Any(values.ContainsKey))
				values.Add("creatorID", 0.ToString());
			if (!new[] { "createDate" }.Any(values.ContainsKey))
				values.Add("createDate", default(DateTime).ToString("yyyy-MM-dd HH:mm:ss"));
			if (!new[] { "level" }.Any(values.ContainsKey))
			{				
				values.Add("level", values["__Path"].Split(',').Length.ToString());
			}


			return new DictionaryPublishedContent(values,
			                              d => d.ParentId != -1 //parent should be null if -1
			                                   	? GetUmbracoMedia(d.ParentId)
			                                   	: null,
			                              //callback to return the children of the current node
			                              d => GetChildrenMedia(d.ParentId),
			                              GetProperty)
				{
					LoadedFromExamine = true
				};
		}

		internal IPublishedContent ConvertFromXPathNavigator(XPathNavigator xpath)
		{
			if (xpath == null) throw new ArgumentNullException("xpath");

			var values = new Dictionary<string, string> {{"nodeName", xpath.GetAttribute("nodeName", "")}};
			if (!UmbracoSettings.UseLegacyXmlSchema)
			{
				values.Add("nodeTypeAlias", xpath.Name);
			}
			
			var result = xpath.SelectChildren(XPathNodeType.Element);
			//add the attributes e.g. id, parentId etc
			if (result.Current != null && result.Current.HasAttributes)
			{
				if (result.Current.MoveToFirstAttribute())
				{
					//checking for duplicate keys because of the 'nodeTypeAlias' might already be added above.
					if (!values.ContainsKey(result.Current.Name))
					{
						values.Add(result.Current.Name, result.Current.Value);	
					}
					while (result.Current.MoveToNextAttribute())
					{
						if (!values.ContainsKey(result.Current.Name))
						{
							values.Add(result.Current.Name, result.Current.Value);
						}						
					}
					result.Current.MoveToParent();
				}
			}
			//add the user props
			while (result.MoveNext())
			{
				if (result.Current != null && !result.Current.HasAttributes)
				{
					string value = result.Current.Value;
					if (string.IsNullOrEmpty(value))
					{
						if (result.Current.HasAttributes || result.Current.SelectChildren(XPathNodeType.Element).Count > 0)
						{
							value = result.Current.OuterXml;
						}
					}
					values.Add(result.Current.Name, value);
				}
			}

			return new DictionaryPublishedContent(values, 
				d => d.ParentId != -1 //parent should be null if -1
					? GetUmbracoMedia(d.ParentId) 
					: null,
				//callback to return the children of the current node based on the xml structure already found
				d => GetChildrenMedia(d.ParentId, xpath),
				GetProperty);
		}

		/// <summary>
		/// We will need to first check if the document was loaded by Examine, if so we'll need to check if this property exists 
		/// in the results, if it does not, then we'll have to revert to looking up in the db. 
		/// </summary>
		/// <param name="dd"> </param>
		/// <param name="alias"></param>
		/// <returns></returns>
		private IPublishedContentProperty GetProperty(DictionaryPublishedContent dd, string alias)
		{
			if (dd.LoadedFromExamine)
			{
				//if this is from Examine, lets check if the alias does not exist on the document
				if (dd.Properties.All(x => x.Alias != alias))
				{
					//ok it doesn't exist, we might assume now that Examine didn't index this property because the index is not set up correctly
					//so before we go loading this from the database, we can check if the alias exists on the content type at all, this information
					//is cached so will be quicker to look up.
					if (dd.Properties.Any(x => x.Alias == "__NodeTypeAlias"))
					{
						var aliasesAndNames = ContentType.GetAliasesAndNames(dd.Properties.First(x => x.Alias.InvariantEquals("__NodeTypeAlias")).Value.ToString());
						if (aliasesAndNames != null)
						{
							if (!aliasesAndNames.ContainsKey(alias))
							{
								//Ok, now we know it doesn't exist on this content type anyways
								return null;
							}
						}
					}

					//if we've made it here, that means it does exist on the content type but not in examine, we'll need to query the db :(
					var media = global::umbraco.library.GetMedia(dd.Id, true);
					if (media != null && media.Current != null)
					{
						media.MoveNext();
						var mediaDoc = ConvertFromXPathNavigator(media.Current);
						return mediaDoc.Properties.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
					}					
				}							
			}
			
			return dd.Properties.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
		}

		/// <summary>
		/// A Helper methods to return the children for media whther it is based on examine or xml
		/// </summary>
		/// <param name="parentId"></param>
		/// <param name="xpath"></param>
		/// <returns></returns>
		private IEnumerable<IPublishedContent> GetChildrenMedia(int parentId, XPathNavigator xpath = null)
		{	

			//if there is no navigator, try examine first, then re-look it up
			if (xpath == null)
			{
				try
				{
					//first check in Examine as this is WAY faster
					var criteria = ExamineManager.Instance
						.SearchProviderCollection["InternalSearcher"]
						.CreateSearchCriteria("media");
					var filter = criteria.ParentId(parentId);
					var results = ExamineManager
						.Instance.SearchProviderCollection["InternalSearcher"]
						.Search(filter.Compile());
					if (results.Any())
					{
						return results.Select(ConvertFromSearchResult);
					}
				}
				catch (FileNotFoundException)
				{
					//Currently examine is throwing FileNotFound exceptions when we have a loadbalanced filestore and a node is published in umbraco
					//See this thread: http://examine.cdodeplex.com/discussions/264341
					//Catch the exception here for the time being, and just fallback to GetMedia
				}

				var media = library.GetMedia(parentId, true);
				if (media != null && media.Current != null)
				{
					if (!media.MoveNext())
						return null;
					xpath = media.Current;
				}
			}

			var children = xpath.SelectChildren(XPathNodeType.Element);
			var mediaList = new List<IPublishedContent>();
			while (children.Current != null)
			{
				if (!children.MoveNext())
				{
					break;
				}

				//NOTE: I'm not sure why this is here, it is from legacy code of ExamineBackedMedia, but
				// will leave it here as it must have done something!
				if (children.Current.Name != "contents")
				{
					//make sure it's actually a node, not a property 
					if (!string.IsNullOrEmpty(children.Current.GetAttribute("path", "")) &&
						!string.IsNullOrEmpty(children.Current.GetAttribute("id", "")))
					{
						mediaList.Add(ConvertFromXPathNavigator(children.Current));
					}
				}
			}
			return mediaList;
		}

		/// <summary>
		/// An IPublishedContent that is represented all by a dictionary.
		/// </summary>
		/// <remarks>
		/// This is a helper class and definitely not intended for public use, it expects that all of the values required 
		/// to create an IPublishedContent exist in the dictionary by specific aliases.
		/// </remarks>
		internal class DictionaryPublishedContent : IPublishedContent
		{

			public DictionaryPublishedContent(
				IDictionary<string, string> valueDictionary, 
				Func<DictionaryPublishedContent, IPublishedContent> getParent,
				Func<DictionaryPublishedContent, IEnumerable<IPublishedContent>> getChildren,
				Func<DictionaryPublishedContent, string, IPublishedContentProperty> getProperty)
			{
				if (valueDictionary == null) throw new ArgumentNullException("valueDictionary");
				if (getParent == null) throw new ArgumentNullException("getParent");
				if (getProperty == null) throw new ArgumentNullException("getProperty");

				_getParent = getParent;
				_getChildren = getChildren;
				_getProperty = getProperty;

				LoadedFromExamine = false; //default to false

				ValidateAndSetProperty(valueDictionary, val => Id = int.Parse(val), "id", "nodeId", "__NodeId"); //should validate the int!
				ValidateAndSetProperty(valueDictionary, val => TemplateId = int.Parse(val), "template", "templateId");
				ValidateAndSetProperty(valueDictionary, val => SortOrder = int.Parse(val), "sortOrder");
				ValidateAndSetProperty(valueDictionary, val => Name = val, "nodeName", "__nodeName");
				ValidateAndSetProperty(valueDictionary, val => UrlName = val, "urlName");
				ValidateAndSetProperty(valueDictionary, val => DocumentTypeAlias = val, "nodeTypeAlias", "__NodeTypeAlias");
				ValidateAndSetProperty(valueDictionary, val => DocumentTypeId = int.Parse(val), "nodeType");
				ValidateAndSetProperty(valueDictionary, val => WriterName = val, "writerName");
				ValidateAndSetProperty(valueDictionary, val => CreatorName = val, "creatorName");
				ValidateAndSetProperty(valueDictionary, val => WriterId = int.Parse(val), "writerID");
				ValidateAndSetProperty(valueDictionary, val => CreatorId = int.Parse(val), "creatorID");
				ValidateAndSetProperty(valueDictionary, val => Path = val, "path", "__Path");
				ValidateAndSetProperty(valueDictionary, val => CreateDate = DateTime.Parse(val), "createDate");
				ValidateAndSetProperty(valueDictionary, val => UpdateDate = DateTime.Parse(val), "updateDate");
				ValidateAndSetProperty(valueDictionary, val => Level = int.Parse(val), "level");
				ValidateAndSetProperty(valueDictionary, val =>
					{
						int pId;
						ParentId = -1;
						if (int.TryParse(val, out pId))
						{
							ParentId = pId;
						}						
					}, "parentID");

				Properties = new Collection<IPublishedContentProperty>();

				//loop through remaining values that haven't been applied
				foreach (var i in valueDictionary.Where(x => !_keysAdded.Contains(x.Key)))
				{
					//this is taken from examine
					Properties.Add(i.Key.InvariantStartsWith("__") 
					               	? new PropertyResult(i.Key, i.Value, Guid.Empty, PropertyResultType.CustomProperty) 
					               	: new PropertyResult(i.Key, i.Value, Guid.Empty, PropertyResultType.UserProperty));
				}
			}

			/// <summary>
			/// Flag to get/set if this was laoded from examine cache
			/// </summary>
			internal bool LoadedFromExamine { get; set; }

			private readonly Func<DictionaryPublishedContent, IPublishedContent> _getParent;
			private readonly Func<DictionaryPublishedContent, IEnumerable<IPublishedContent>> _getChildren;
			private readonly Func<DictionaryPublishedContent, string, IPublishedContentProperty> _getProperty;

			public IPublishedContent Parent
			{
				get { return _getParent(this); }
			}

			public int ParentId { get; private set; }
			public int Id { get; private set; }
			public int TemplateId { get; private set; }
			public int SortOrder { get; private set; }
			public string Name { get; private set; }
			public string UrlName { get; private set; }
			public string DocumentTypeAlias { get; private set; }
			public int DocumentTypeId { get; private set; }
			public string WriterName { get; private set; }
			public string CreatorName { get; private set; }
			public int WriterId { get; private set; }
			public int CreatorId { get; private set; }
			public string Path { get; private set; }
			public DateTime CreateDate { get; private set; }
			public DateTime UpdateDate { get; private set; }
			public Guid Version { get; private set; }
			public int Level { get; private set; }
			public Collection<IPublishedContentProperty> Properties { get; private set; }
			public IEnumerable<IPublishedContent> Children
			{
				get { return _getChildren(this); }
			}

			public IPublishedContentProperty GetProperty(string alias)
			{
				return _getProperty(this, alias);
			}

			private readonly List<string> _keysAdded = new List<string>();
			private void ValidateAndSetProperty(IDictionary<string, string> valueDictionary, Action<string> setProperty, params string[] potentialKeys)
			{
				var key = potentialKeys.FirstOrDefault(x => valueDictionary.ContainsKey(x) && valueDictionary[x] != null);
				if (key == null)
				{
					throw new FormatException("The valueDictionary is not formatted correctly and is missing any of the  '" + string.Join(",", potentialKeys) + "' elements");
				}

				setProperty(valueDictionary[key]);
				_keysAdded.Add(key);
			}
		}
	}
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Caching;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Xml;
using System.Xml.Xsl;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.macro;
using umbraco.cms.businesslogic.member;
using umbraco.DataLayer;
using umbraco.interfaces;
using umbraco.IO;
using umbraco.NodeFactory;
using umbraco.presentation.templateControls;
using umbraco.presentation.xslt.Exslt;
using umbraco.scripting;
using Content = umbraco.cms.businesslogic.Content;
using Macro = umbraco.cms.businesslogic.macro.Macro;

namespace umbraco
{
    /// <summary>
    /// Summary description for macro.
    /// </summary>
    public class macro : Page
    {
        #region private properties

        private static Hashtable _macroAlias = new Hashtable();

        /// <summary>Cache for <see cref="GetPredefinedXsltExtensions"/>.</summary>
        private static Dictionary<string, object> m_PredefinedExtensions;

        private readonly string loadUserControlKey = "loadUserControl";

        private readonly StringBuilder mContent = new StringBuilder();
        private readonly Cache macroCache = HttpRuntime.Cache;

        private readonly String macroCacheIdentifier = "umbMacro";
        private readonly int macroType;
        private readonly string macrosAddedKey = "macrosAdded";
        private readonly Hashtable propertyDefinitions = new Hashtable();

        // Macro-elements
        private int macroID;
        private Hashtable properties = new Hashtable();
        private String scriptAssembly;
        private String scriptFile;
        private String scriptType;
        private String xsltFile;

        protected static ISqlHelper SqlHelper
        {
            get { return BusinessLogic.Application.SqlHelper; }
        }

        #endregion

        #region public properties

        public int MacroID
        {
            set { macroID = value; }
            get { return macroID; }
        }

        public bool CacheByPersonalization { set; get; }

        public bool CacheByPage { set; get; }

        public bool DontRenderInEditor { get; set; }

        public int RefreshRate { set; get; }

        public String Alias { set; get; }

        public String Name { set; get; }

        public String XsltFile
        {
            set { xsltFile = value; }
            get { return xsltFile; }
        }

        public String ScriptFile
        {
            set { scriptFile = value; }
            get { return scriptFile; }
        }

        public String ScriptType
        {
            set { scriptType = value; }
            get { return scriptType; }
        }

        public String ScriptAssembly
        {
            set { scriptAssembly = value; }
            get { return scriptAssembly; }
        }

        public Hashtable Properties
        {
            get { return properties; }
            set { properties = value; }
        }

        public int MacroType
        {
            get { return macroType; }
        }

        public String MacroContent
        {
            set { mContent.Append(value); }
            get { return mContent.ToString(); }
        }

        #endregion

        /// <summary>
        /// Creates an empty macro object.
        /// </summary>
        public macro()
        {
        }

        /// <summary>
        /// Creates a macro object
        /// </summary>
        /// <param name="id">Specify the macro-id which should be loaded (from table macro)</param>
        public macro(int id)
        {
            macroID = id;

            if (macroCache[macroCacheIdentifier + id] != null)
            {
                var tempMacro = (macro)macroCache[macroCacheIdentifier + id];
                Name = tempMacro.Name;
                Alias = tempMacro.Alias;
                ScriptType = tempMacro.ScriptType;
                ScriptAssembly = tempMacro.ScriptAssembly;
                XsltFile = tempMacro.XsltFile;
                scriptFile = tempMacro.ScriptFile;
                Properties = tempMacro.Properties;
                propertyDefinitions = tempMacro.propertyDefinitions;
                RefreshRate = tempMacro.RefreshRate;
                CacheByPage = tempMacro.CacheByPage;
                CacheByPersonalization = tempMacro.CacheByPersonalization;
                DontRenderInEditor = tempMacro.DontRenderInEditor;

                HttpContext.Current.Trace.Write("umbracoMacro",
                                                string.Format("Macro loaded from cache (ID: {0}, {1})", id, Name));
            }
            else
            {
                using (
                    IRecordsReader macroDef =
                        SqlHelper.ExecuteReader(
                            "select * from cmsMacro left join cmsMacroProperty property on property.macro = cmsMacro.id left join cmsMacroPropertyType editPropertyType on editPropertyType.id = property.macroPropertyType where cmsMacro.id = @macroID order by property.macroPropertySortOrder",
                            SqlHelper.CreateParameter("@macroID", id)))
                {
                    bool hasRows = macroDef.Read();
                    if (!hasRows)
                        HttpContext.Current.Trace.Warn("Macro", "No definition found for id " + id);

                    while (hasRows)
                    {
                        string tmpStr;
                        bool tmpBool;
                        int tmpInt;

                        if (TryGetColumnBool(macroDef, "macroCacheByPage", out tmpBool))
                            CacheByPage = tmpBool;
                        if (TryGetColumnBool(macroDef, "macroCachePersonalized", out tmpBool))
                            CacheByPersonalization = tmpBool;
                        if (TryGetColumnBool(macroDef, "macroDontRender", out tmpBool))
                            DontRenderInEditor = tmpBool;
                        if (TryGetColumnInt32(macroDef, "macroRefreshRate", out tmpInt))
                            RefreshRate = tmpInt;
                        if (TryGetColumnInt32(macroDef, "macroRefreshRate", out tmpInt))
                            RefreshRate = tmpInt;
                        if (TryGetColumnString(macroDef, "macroName", out tmpStr))
                            Name = tmpStr;
                        if (TryGetColumnString(macroDef, "macroAlias", out tmpStr))
                            Alias = tmpStr;
                        if (TryGetColumnString(macroDef, "macroScriptType", out tmpStr))
                            ScriptType = tmpStr;
                        if (TryGetColumnString(macroDef, "macroScriptAssembly", out tmpStr))
                            ScriptAssembly = tmpStr;
                        if (TryGetColumnString(macroDef, "macroXSLT", out tmpStr))
                            XsltFile = tmpStr;

                        if (TryGetColumnString(macroDef, "macroPython", out tmpStr))
                            ScriptFile = tmpStr;

                        if (TryGetColumnString(macroDef, "macroPropertyAlias", out tmpStr))
                        {
                            string typeAlias;

                            if (TryGetColumnString(macroDef, "macroPropertyTypeAlias", out typeAlias) &&
                                !properties.ContainsKey(tmpStr))
                                properties.Add(tmpStr, typeAlias);

                            string baseType;
                            if (TryGetColumnString(macroDef, "macroPropertyTypeBaseType", out baseType) &&
                                !propertyDefinitions.ContainsKey(tmpStr))
                                propertyDefinitions.Add(tmpStr, baseType);
                        }
                        hasRows = macroDef.Read();
                    }
                }
                // add current macro-object to cache
                macroCache.Insert(macroCacheIdentifier + id, this);
            }

            macroType = (int)Macro.FindMacroType(XsltFile, ScriptFile, ScriptType, ScriptAssembly);
        }

        public override string ToString()
        {
            return Name;
        }

        public static macro ReturnFromAlias(string alias)
        {
            if (_macroAlias.ContainsKey(alias))
                return new macro((int)_macroAlias[alias]);
            else
            {
                try
                {
                    int macroID = Macro.GetByAlias(alias).Id;
                    _macroAlias.Add(alias, macroID);
                    return new macro(macroID);
                }
                catch
                {
                    HttpContext.Current.Trace.Warn("macro", "No macro with alias '" + alias + "' found");
                    return null;
                }
            }
        }

        public static bool TryGetColumnString(IRecordsReader reader, string columnName, out string value)
        {
            if (reader.ContainsField(columnName) && !reader.IsNull(columnName))
            {
                value = reader.GetString(columnName);
                return true;
            }
            else
            {
                value = string.Empty;
                return false;
            }
        }

        public static bool TryGetColumnInt32(IRecordsReader reader, string columnName, out int value)
        {
            if (reader.ContainsField(columnName) && !reader.IsNull(columnName))
            {
                value = reader.GetInt(columnName);
                return true;
            }
            else
            {
                value = -1;
                return false;
            }
        }

        public static bool TryGetColumnBool(IRecordsReader reader, string columnName, out bool value)
        {
            if (reader.ContainsField(columnName) && !reader.IsNull(columnName))
            {
                value = reader.GetBoolean(columnName);
                return true;
            }
            else
            {
                value = false;
                return false;
            }
        }

        public static void ClearAliasCache()
        {
            _macroAlias = new Hashtable();
        }

        /// <summary>
        /// Deletes macro definition from cache.
        /// </summary>
        /// <returns>True if succesfull, false if nothing has been removed</returns>
        public bool removeFromCache()
        {
            ClearAliasCache();
            if (macroID > 0)
            {
                if (macroCache[macroCacheIdentifier + macroID] != null)
                {
                    macroCache.Remove(macroCacheIdentifier + macroID);
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }

        private string getCacheGuid(MacroModel model, Hashtable pageElements, int pageId)
        {
            string tempGuid = !String.IsNullOrEmpty(model.ScriptCode)
                                  ? Macro.GenerateCacheKeyFromCode(model.ScriptCode) + "-"
                                  : model.Alias + "-";

            if (CacheByPage)
            {
                tempGuid = pageId + "-";
            }

            if (CacheByPersonalization)
            {
                if (Member.GetCurrentMember() != null)
                    tempGuid += "m" + Member.GetCurrentMember().Id + "-";
                else
                    tempGuid += "m";
            }
            foreach (MacroPropertyModel prop in model.Properties)
            {
                string attValue = prop.Value;
                if (attValue.Length > 255)
                    tempGuid += attValue.Remove(255, attValue.Length - 255) + "-";
                else
                    tempGuid += attValue + "-";
            }
            return tempGuid;
        }

        public Control renderMacro(Hashtable attributes, Hashtable pageElements, int pageId)
        {
            MacroModel m = ConvertToMacroModel(attributes);
            return renderMacro(m, pageElements, pageId);
        }

        public Control renderMacro(MacroModel model, Hashtable pageElements, int pageId)
        {
            HttpContext.Current.Trace.Write("renderMacro",
                                            string.Format("Rendering started (macro: {0}, type: {1}, cacheRate: {2})",
                                                          Name, MacroType, model.CacheDuration));

            StateHelper.SetContextValue(macrosAddedKey, StateHelper.GetContextValue<int>(macrosAddedKey) + 1);

            String macroHtml = null;
            Control macroControl = null;

            // zb-00037 #29875 : parse attributes here (and before anything else)
            foreach (var prop in model.Properties)
                prop.Value = helper.parseAttribute(pageElements, prop.Value);

            model.CacheIdentifier = getCacheGuid(model, pageElements, pageId);

            if (model.CacheDuration > 0)
            {
                if (cacheMacroAsString(model))
                {
                    macroHtml = macroCache["macroHtml_" + model.CacheIdentifier] as String;

                    if (!String.IsNullOrEmpty(macroHtml))
                    {
                        macroHtml = macroCache["macroHtml_" + model.CacheIdentifier] as String;
                        HttpContext.Current.Trace.Write("renderMacro", "Macro Content loaded from cache ('" + model.CacheIdentifier + "')...");
                    }
                }
                else
                {
                    if (macroCache["macroControl_" + model.CacheIdentifier] != null)
                    {
                        MacroCacheContent cacheContent = (MacroCacheContent)macroCache["macroControl_" + model.CacheIdentifier];
                        macroControl = cacheContent.Content;
                        macroControl.ID = cacheContent.ID;
                        HttpContext.Current.Trace.Write("renderMacro", "Macro Control loaded from cache ('" + model.CacheIdentifier + "')...");
                    }

                }
            }

            if (String.IsNullOrEmpty(macroHtml) && macroControl == null)
            {
                int macroType = model.MacroType != MacroTypes.Unknown ? (int)model.MacroType : MacroType;
                switch (macroType)
                {
                    case (int)MacroTypes.UserControl:
                        try
                        {
                            HttpContext.Current.Trace.Write("umbracoMacro", "Usercontrol added (" + scriptType + ")");
                            macroControl = loadUserControl(ScriptType, model, pageElements);
                            break;
                        }
                        catch (Exception e)
                        {
                            HttpContext.Current.Trace.Warn("umbracoMacro",
                                                           "Error loading userControl (" + scriptType + ")", e);
                            macroControl = new LiteralControl("Error loading userControl '" + scriptType + "'");
                            break;
                        }
                    case (int)MacroTypes.CustomControl:
                        try
                        {
                            HttpContext.Current.Trace.Write("umbracoMacro", "Custom control added (" + scriptType + ")");
                            HttpContext.Current.Trace.Write("umbracoMacro", "ScriptAssembly (" + scriptAssembly + ")");
                            macroControl = loadControl(scriptAssembly, ScriptType, model, pageElements);
                            break;
                        }
                        catch (Exception e)
                        {
                            HttpContext.Current.Trace.Warn("umbracoMacro",
                                                           "Error loading customControl (Assembly: " + scriptAssembly +
                                                           ", Type: '" + scriptType + "'", e);
                            macroControl =
                                new LiteralControl("Error loading customControl (Assembly: " + scriptAssembly +
                                                   ", Type: '" +
                                                   scriptType + "'");
                            break;
                        }
                    case (int)MacroTypes.XSLT:
                        macroControl = loadMacroXSLT(this, model, pageElements);
                        break;
                    case (int)MacroTypes.Script:
                        try
                        {
                            HttpContext.Current.Trace.Write("umbracoMacro",
                                                            "MacroEngine script added (" + ScriptFile + ")");
                            macroControl = loadMacroDLR(model);
                            break;
                        }
                        catch (Exception e)
                        {
                            HttpContext.Current.Trace.Warn("umbracoMacro",
                                                           "Error loading MacroEngine script (file: " + ScriptFile +
                                                           ", Type: '" + scriptType + "'", e);

                            var result = new LiteralControl("Error loading MacroEngine script (file: " + ScriptFile + ")");

                            /*
                            string args = "<ul>";
                            foreach(object key in attributes.Keys)
                                args += "<li><strong>" + key.ToString() + ": </strong> " + attributes[key] + "</li>";

                            foreach (object key in pageElements.Keys)
                                args += "<li><strong>" + key.ToString() + ": </strong> " + pageElements[key] + "</li>";

                            args += "</ul>";

                            result.Text += args;
                            */

                            macroControl = result;

                            break;
                        }
                    default:
                        if (GlobalSettings.DebugMode)
                            macroControl =
                                new LiteralControl("&lt;Macro: " + Name + " (" + ScriptAssembly + "," + ScriptType +
                                                   ")&gt;");
                        break;
                }

                // Add result to cache
                if (model.CacheDuration > 0)
                {
                    // do not add to cache if there's no member and it should cache by personalization
                    if (!model.CacheByMember || (model.CacheByMember && Member.GetCurrentMember() != null))
                    {
                        if (macroControl != null)
                        {
                            // NH: Scripts and XSLT can be generated as strings, but not controls as page events wouldn't be hit (such as Page_Load, etc)
                            if (cacheMacroAsString(model))
                            {
                                using (var sw = new StringWriter())
                                {
                                    var hw = new HtmlTextWriter(sw);
                                    macroControl.RenderControl(hw);

                                    macroCache.Insert("macroHtml_" + model.CacheIdentifier,
                                                      sw.ToString(),
                                                      null,
                                                      DateTime.Now.AddSeconds(model.CacheDuration),
                                                      TimeSpan.Zero,
                                                      CacheItemPriority.Low,
                                                      null);

                                    // zb-00003 #29470 : replace by text if not already text
                                    // otherwise it is rendered twice
                                    if (!(macroControl is LiteralControl))
                                        macroControl = new LiteralControl(sw.ToString());
                                }
                            }
                            else
                            {
                                macroCache.Insert("macroControl_" + model.CacheIdentifier, new MacroCacheContent(macroControl, macroControl.ID), null,
                  DateTime.Now.AddSeconds(model.CacheDuration), TimeSpan.Zero, CacheItemPriority.Low,
                  null);

                            }
                        }
                    }
                }
            }
            else if (macroControl == null)
            {
                macroControl = new LiteralControl(macroHtml);
            }

            return macroControl;
        }

        private bool cacheMacroAsString(MacroModel model)
        {
            return model.MacroType == MacroTypes.XSLT || model.MacroType == MacroTypes.Python;
        }

        public static XslCompiledTransform getXslt(string XsltFile)
        {
            if (HttpRuntime.Cache["macroXslt_" + XsltFile] != null)
            {
                return (XslCompiledTransform)HttpRuntime.Cache["macroXslt_" + XsltFile];
            }
            else
            {
                var xslReader =
                    new XmlTextReader(IOHelper.MapPath(SystemDirectories.Xslt + "/" + XsltFile));

                XslCompiledTransform macroXSLT = CreateXsltTransform(xslReader, GlobalSettings.DebugMode);
                HttpRuntime.Cache.Insert(
                    "macroXslt_" + XsltFile,
                    macroXSLT,
                    new CacheDependency(IOHelper.MapPath(SystemDirectories.Xslt + "/" + XsltFile)));
                return macroXSLT;
            }
        }

        public MacroModel ConvertToMacroModel(Hashtable attributes)
        {
            MacroModel model = new MacroModel(
                this.Name,
                this.Alias,
                this.ScriptAssembly,
                this.ScriptType,
                this.XsltFile,
                this.ScriptFile,
                this.RefreshRate,
                this.CacheByPage,
                this.CacheByPersonalization
                );

            foreach (string key in attributes.Keys)
            {
                model.Properties.Add(new MacroPropertyModel(key, attributes[key].ToString()));
            }

            return model;
        }


        public static XslCompiledTransform CreateXsltTransform(XmlTextReader xslReader, bool debugMode)
        {
            var macroXSLT = new XslCompiledTransform(debugMode);
            var xslResolver = new XmlUrlResolver();
            xslResolver.Credentials = CredentialCache.DefaultCredentials;

            xslReader.EntityHandling = EntityHandling.ExpandEntities;

            try
            {
                if (GlobalSettings.ApplicationTrustLevel > AspNetHostingPermissionLevel.Medium)
                {
                    macroXSLT.Load(xslReader, XsltSettings.TrustedXslt, xslResolver);
                }
                else
                {
                    macroXSLT.Load(xslReader, XsltSettings.Default, xslResolver);
                }
            }
            finally
            {
                xslReader.Close();
            }

            return macroXSLT;
        }

        public static void unloadXslt(string XsltFile)
        {
            if (HttpRuntime.Cache["macroXslt_" + XsltFile] != null)
                HttpRuntime.Cache.Remove("macroXslt_" + XsltFile);
        }

        private Hashtable keysToLowerCase(Hashtable input)
        {
            var retval = new Hashtable();
            foreach (object key in input.Keys)
                retval.Add(key.ToString().ToLower(), input[key]);

            return retval;
        }

        public Control loadMacroXSLT(macro macro, MacroModel model, Hashtable pageElements)
        {
            if (XsltFile.Trim() != string.Empty)
            {
                // Get main XML
                XmlDocument umbracoXML = content.Instance.XmlContent;

                // Create XML document for Macro
                var macroXML = new XmlDocument();
                macroXML.LoadXml("<macro/>");

                foreach (DictionaryEntry macroDef in macro.properties)
                {
                    var prop = model.Properties.Find(m => m.Key == (string)macroDef.Key.ToString().ToLower());
                    // zb-00037 #29875 : values have already been parsed + no need to parse ""
                    string propValue = prop != null ? prop.Value : "";
                    if (!String.IsNullOrEmpty(propValue))
                    {
                        if (propValue != string.Empty)
                            addMacroXmlNode(umbracoXML, macroXML, macroDef.Key.ToString(), macroDef.Value.ToString(),
                                            propValue);
                        else
                            addMacroXmlNode(umbracoXML, macroXML, macroDef.Key.ToString(), macroDef.Value.ToString(),
                                            string.Empty);
                    }
                }

                if (HttpContext.Current.Request.QueryString["umbDebug"] != null && GlobalSettings.DebugMode)
                {
                    return
                        new LiteralControl("<div style=\"border: 2px solid green; padding: 5px;\"><b>Debug from " +
                                           macro.Name +
                                           "</b><br/><p>" + Page.Server.HtmlEncode(macroXML.OuterXml) + "</p></div>");
                }
                else
                {
                    try
                    {
                        XslCompiledTransform xsltFile = getXslt(XsltFile);

                        try
                        {
                            Control result = CreateControlsFromText(GetXsltTransformResult(macroXML, xsltFile));

                            HttpContext.Current.Trace.Write("umbracoMacro", "After performing transformation");

                            return result;
                        }
                        catch (Exception e)
                        {
                            // inner exception code by Daniel Lindstr�m from SBBS.se
                            Exception ie = e;
                            while (ie != null)
                            {
                                HttpContext.Current.Trace.Warn("umbracoMacro InnerException", ie.Message, ie);
                                ie = ie.InnerException;
                            }
                            return new LiteralControl("Error parsing XSLT file: \\xslt\\" + XsltFile);
                        }
                    }
                    catch (Exception e)
                    {
                        HttpContext.Current.Trace.Warn("umbracoMacro", "Error loading XSLT " + xsltFile, e);
                        return new LiteralControl("Error reading XSLT file: \\xslt\\" + XsltFile);
                    }
                }
            }
            else
            {
                Page.Trace.Warn("macro", "Xslt is empty");
                return new LiteralControl(string.Empty);
            }
        }

        /// <summary>
        /// Parses the text for umbraco Item controls that need to be rendered.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <returns>A control containing the parsed text.</returns>
        protected Control CreateControlsFromText(string text)
        {
            // the beginning and end tags
            const string tagStart = "[[[[umbraco:Item";
            const string tagEnd = "]]]]";

            // container that will hold parsed controls
            var container = new PlaceHolder();

            // loop through all text
            int textPos = 0;
            while (textPos < text.Length)
            {
                // try to find an item tag, carefully staying inside the string bounds (- 1)
                int tagStartPos = text.IndexOf(tagStart, textPos);
                int tagEndPos = tagStartPos < 0 ? -1 : text.IndexOf(tagEnd, tagStartPos + tagStart.Length - 1);

                // item tag found?
                if (tagStartPos >= 0 && tagEndPos >= 0)
                {
                    // add the preceding text as a literal control
                    if (tagStartPos > textPos)
                        container.Controls.Add(new LiteralControl(text.Substring(textPos, tagStartPos - textPos)));

                    // extract the tag and parse it
                    string tag = text.Substring(tagStartPos, (tagEndPos + tagEnd.Length) - tagStartPos);
                    Hashtable attributes = helper.ReturnAttributes(tag);

                    // create item with the parameters specified in the tag
                    var item = new Item();
                    item.NodeId = helper.FindAttribute(attributes, "nodeid");
                    item.Field = helper.FindAttribute(attributes, "field");
                    item.Xslt = helper.FindAttribute(attributes, "xslt");
                    item.XsltDisableEscaping = helper.FindAttribute(attributes, "xsltdisableescaping") == "true";
                    container.Controls.Add(item);

                    // advance past the end of the tag
                    textPos = tagEndPos + tagEnd.Length;
                }
                else
                {
                    // no more tags found, just add the remaning text
                    container.Controls.Add(new LiteralControl(text.Substring(textPos)));
                    textPos = text.Length;
                }
            }
            return container;
        }

        public static string GetXsltTransformResult(XmlDocument macroXML, XslCompiledTransform xslt)
        {
            return GetXsltTransformResult(macroXML, xslt, null);
        }

        public static string GetXsltTransformResult(XmlDocument macroXML, XslCompiledTransform xslt,
                                                    Dictionary<string, object> parameters)
        {
            TextWriter tw = new StringWriter();

            HttpContext.Current.Trace.Write("umbracoMacro", "Before adding extensions");
            XsltArgumentList xslArgs;
            xslArgs = AddXsltExtensions();
            var lib = new library();
            xslArgs.AddExtensionObject("urn:umbraco.library", lib);
            HttpContext.Current.Trace.Write("umbracoMacro", "After adding extensions");

            // Add parameters
            if (parameters == null || !parameters.ContainsKey("currentPage"))
            {
                xslArgs.AddParam("currentPage", string.Empty, library.GetXmlNodeCurrent());
            }
            if (parameters != null)
            {
                foreach (var parameter in parameters)
                    xslArgs.AddParam(parameter.Key, string.Empty, parameter.Value);
            }

            // Do transformation
            HttpContext.Current.Trace.Write("umbracoMacro", "Before performing transformation");
            xslt.Transform(macroXML.CreateNavigator(), xslArgs, tw);
            return IOHelper.ResolveUrlsFromTextString(tw.ToString());
        }

        public static XsltArgumentList AddXsltExtensions()
        {
            return AddMacroXsltExtensions();
        }

        /// <summary>
        /// Gets a collection of all XSLT extensions for macros, including predefined extensions.
        /// </summary>
        /// <returns>A dictionary of name/extension instance pairs.</returns>
        public static Dictionary<string, object> GetXsltExtensions()
        {
            // zb-00041 #29966 : cache the extensions

            // We could cache the extensions in a static variable but then the cache
            // would not be refreshed when the .config file is modified. An application
            // restart would be required. Better use the cache and add a dependency.

            return umbraco.cms.businesslogic.cache.Cache.GetCacheItem(
                _xsltExtensionsCacheKey, _xsltExtensionsSyncLock,
                CacheItemPriority.Normal, // normal priority
                null, // no refresh action
                new CacheDependency(_xsltExtensionsConfig), // depends on the .config file
                TimeSpan.FromDays(1), // expires in 1 day (?)
                () => { return GetXsltExtensionsImpl(); });
        }

        // zb-00041 #29966 : cache the extensions
        const string _xsltExtensionsCacheKey = "UmbracoXsltExtensions";
        static string _xsltExtensionsConfig = IOHelper.MapPath(SystemDirectories.Config + "/xsltExtensions.config");
        static object _xsltExtensionsSyncLock = new object();

        static Dictionary<string, object> GetXsltExtensionsImpl()
        {
            // fill a dictionary with the predefined extensions
            var extensions = new Dictionary<string, object>(GetPredefinedXsltExtensions());

            // Load the XSLT extensions configuration
            var xsltExt = new XmlDocument();
            xsltExt.Load(_xsltExtensionsConfig);

            // add all descendants of the XsltExtensions element
            foreach (XmlNode xsltEx in xsltExt.SelectSingleNode("/XsltExtensions"))
            {
                if (xsltEx.NodeType == XmlNodeType.Element)
                {
                    Debug.Assert(xsltEx.Attributes["assembly"] != null, "Extension attribute 'assembly' not specified.");
                    Debug.Assert(xsltEx.Attributes["type"] != null, "Extension attribute 'type' not specified.");
                    Debug.Assert(xsltEx.Attributes["alias"] != null, "Extension attribute 'alias' not specified.");

                    // load the extension assembly
                    string extensionFile =
                        IOHelper.MapPath(string.Format("{0}/{1}.dll", SystemDirectories.Bin,
                                                       xsltEx.Attributes["assembly"].Value));

                    Assembly extensionAssembly;
                    try
                    {
                        extensionAssembly = Assembly.LoadFrom(extensionFile);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception(
                            String.Format(
                                "Could not load assembly {0} for XSLT extension {1}. Please check config/xsltExentions.config.",
                                extensionFile, xsltEx.Attributes["alias"].Value), ex);
                    }

                    // load the extension type
                    Type extensionType = extensionAssembly.GetType(xsltEx.Attributes["type"].Value);
                    if (extensionType == null)
                        throw new Exception(
                            String.Format(
                                "Could not load type {0} ({1}) for XSLT extension {1}. Please check config/xsltExentions.config.",
                                xsltEx.Attributes["type"].Value, extensionFile, xsltEx.Attributes["alias"].Value));

                    // create an instance and add it to the extensions list
                    extensions.Add(xsltEx.Attributes["alias"].Value, Activator.CreateInstance(extensionType));
                }
            }

            //also get types marked with XsltExtension attribute

            // zb-00042 #29949 : do not hide errors, refactor
            foreach (Type xsltType in BusinessLogic.Utils.TypeFinder.FindClassesMarkedWithAttribute(typeof(XsltExtensionAttribute)))
            {
                object[] tpAttributes = xsltType.GetCustomAttributes(typeof(XsltExtensionAttribute), true);
                foreach (XsltExtensionAttribute tpAttribute in tpAttributes)
                {
                    string ns = !string.IsNullOrEmpty(tpAttribute.Namespace) ? tpAttribute.Namespace : xsltType.FullName;
                    extensions.Add(ns, Activator.CreateInstance(xsltType));
                }
            }

            return extensions;
        }

        /// <summary>
        /// Gets the predefined XSLT extensions.
        /// </summary>
        /// <remarks>
        ///     This is a legacy list of EXSLT extensions.
        ///     The Umbraco library is not included, because its instance is page specific.
        /// </remarks>
        /// <returns>A dictionary of name/extension instance pairs.</returns>
        public static Dictionary<string, object> GetPredefinedXsltExtensions()
        {
            if (m_PredefinedExtensions == null)
            {
                m_PredefinedExtensions = new Dictionary<string, object>();

                // add predefined EXSLT extensions
                m_PredefinedExtensions.Add("Exslt.ExsltCommon", new ExsltCommon());
                m_PredefinedExtensions.Add("Exslt.ExsltDatesAndTimes", new ExsltDatesAndTimes());
                m_PredefinedExtensions.Add("Exslt.ExsltMath", new ExsltMath());
                m_PredefinedExtensions.Add("Exslt.ExsltRegularExpressions", new ExsltRegularExpressions());
                m_PredefinedExtensions.Add("Exslt.ExsltStrings", new ExsltStrings());
                m_PredefinedExtensions.Add("Exslt.ExsltSets", new ExsltSets());
            }

            return m_PredefinedExtensions;
        }

        /// <summary>
        /// Returns an XSLT argument list with all XSLT extensions added,
        /// both predefined and configured ones.
        /// </summary>
        /// <returns>A new XSLT argument list.</returns>
        public static XsltArgumentList AddMacroXsltExtensions()
        {
            var xslArgs = new XsltArgumentList();

            foreach (var extension in GetXsltExtensions())
            {
                string extensionNamespace = "urn:" + extension.Key;
                xslArgs.AddExtensionObject(extensionNamespace, extension.Value);
                HttpContext.Current.Trace.Write("umbracoXsltExtension",
                                                String.Format("Extension added: {0}, {1}",
                                                              extensionNamespace, extension.Value.GetType().Name));
            }

            return xslArgs;
        }

        private void addMacroXmlNode(XmlDocument umbracoXML, XmlDocument macroXML, String macroPropertyAlias,
                                     String macroPropertyType, String macroPropertyValue)
        {
            XmlNode macroXmlNode = macroXML.CreateNode(XmlNodeType.Element, macroPropertyAlias, string.Empty);
            var x = new XmlDocument();

            int currentID = -1;
            // If no value is passed, then use the current pageID as value
            if (macroPropertyValue == string.Empty)
            {
                var umbPage = (page)HttpContext.Current.Items["umbPageObject"];
                if (umbPage == null)
                    return;
                currentID = umbPage.PageID;
            }

            HttpContext.Current.Trace.Write("umbracoMacro",
                                            "Xslt node adding search start (" + macroPropertyAlias + ",'" +
                                            macroPropertyValue + "')");
            switch (macroPropertyType)
            {
                case "contentTree":
                    XmlAttribute nodeID = macroXML.CreateAttribute("nodeID");
                    if (macroPropertyValue != string.Empty)
                        nodeID.Value = macroPropertyValue;
                    else
                        nodeID.Value = currentID.ToString();
                    macroXmlNode.Attributes.SetNamedItem(nodeID);

                    // Get subs
                    try
                    {
                        macroXmlNode.AppendChild(macroXML.ImportNode(umbracoXML.GetElementById(nodeID.Value), true));
                    }
                    catch
                    {
                        break;
                    }
                    break;
                case "contentCurrent":
                    x.LoadXml("<nodes/>");
                    XmlNode currentNode;
                    if (macroPropertyValue != string.Empty)
                        currentNode = macroXML.ImportNode(umbracoXML.GetElementById(macroPropertyValue), true);
                    else
                        currentNode = macroXML.ImportNode(umbracoXML.GetElementById(currentID.ToString()), true);

                    // remove all sub content nodes
                    foreach (XmlNode n in currentNode.SelectNodes("./node"))
                        currentNode.RemoveChild(n);

                    macroXmlNode.AppendChild(currentNode);

                    break;
                case "contentSubs":
                    x.LoadXml("<nodes/>");
                    if (macroPropertyValue != string.Empty)
                        x.FirstChild.AppendChild(x.ImportNode(umbracoXML.GetElementById(macroPropertyValue), true));
                    else
                        x.FirstChild.AppendChild(x.ImportNode(umbracoXML.GetElementById(currentID.ToString()), true));
                    macroXmlNode.InnerXml = transformMacroXML(x, "macroGetSubs.xsl");
                    break;
                case "contentAll":
                    x.ImportNode(umbracoXML.DocumentElement.LastChild, true);
                    break;
                case "contentRandom":
                    XmlNode source = umbracoXML.GetElementById(macroPropertyValue);
                    if (source != null)
                    {
                        XmlNodeList sourceList = source.SelectNodes("node");
                        if (sourceList.Count > 0)
                        {
                            int rndNumber;
                            Random r = library.GetRandom();
                            lock (r)
                            {
                                rndNumber = r.Next(sourceList.Count);
                            }
                            XmlNode node = macroXML.ImportNode(sourceList[rndNumber], true);
                            // remove all sub content nodes
                            foreach (XmlNode n in node.SelectNodes("./node"))
                                node.RemoveChild(n);

                            macroXmlNode.AppendChild(node);
                            break;
                        }
                        else
                            HttpContext.Current.Trace.Warn("umbracoMacro",
                                                           "Error adding random node - parent (" + macroPropertyValue +
                                                           ") doesn't have children!");
                    }
                    else
                        HttpContext.Current.Trace.Warn("umbracoMacro",
                                                       "Error adding random node - parent (" + macroPropertyValue +
                                                       ") doesn't exists!");
                    break;
                case "mediaCurrent":
                    var c = new Content(int.Parse(macroPropertyValue));
                    macroXmlNode.AppendChild(macroXML.ImportNode(c.ToXml(content.Instance.XmlContent, false), true));
                    break;
                default:
                    macroXmlNode.InnerText = HttpContext.Current.Server.HtmlDecode(macroPropertyValue);
                    break;
            }
            macroXML.FirstChild.AppendChild(macroXmlNode);
        }

        private string transformMacroXML(XmlDocument xmlSource, string xslt_File)
        {
            var sb = new StringBuilder();
            var sw = new StringWriter(sb);

            XslCompiledTransform result = getXslt(xslt_File);
            //XmlDocument xslDoc = new XmlDocument();

            result.Transform(xmlSource.CreateNavigator(), null, sw);

            if (sw.ToString() != string.Empty)
                return sw.ToString();
            else
                return string.Empty;
        }

        public Control loadMacroDLR(MacroModel macro)
        {
            TraceInfo("umbracoMacro", "Loading IMacroEngine script");
            var ret = new LiteralControl();
            if (!String.IsNullOrEmpty(macro.ScriptCode))
            {
                IMacroEngine engine = MacroEngineFactory.GetByExtension(macro.ScriptLanguage);
                ret.Text = engine.Execute(
                    macro,
                    Node.GetCurrent());

            }
            else
            {
                string path = IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + macro.ScriptName);
                IMacroEngine engine = MacroEngineFactory.GetByFilename(path);
                ret.Text = engine.Execute(macro, Node.GetCurrent());
            }
            TraceInfo("umbracoMacro", "Loading IMacroEngine script [done]");
            return ret;
        }

        /// <summary>
        /// Loads a custom or webcontrol using reflection into the macro object
        /// </summary>
        /// <param name="fileName">The assembly to load from</param>
        /// <param name="controlName">Name of the control</param>
        /// <returns></returns>
        /// <param name="attributes"></param>
        public Control loadControl(string fileName, string controlName, MacroModel model)
        {
            return loadControl(fileName, controlName, model, null);
        }

        /// <summary>
        /// Loads a custom or webcontrol using reflection into the macro object
        /// </summary>
        /// <param name="fileName">The assembly to load from</param>
        /// <param name="controlName">Name of the control</param>
        /// <returns></returns>
        /// <param name="attributes"></param>
        /// <param name="umbPage"></param>
        public Control loadControl(string fileName, string controlName, MacroModel model, Hashtable pageElements)
        {
            Type type;
            Assembly asm;
            try
            {
                string currentAss = IOHelper.MapPath(string.Format("{0}/{1}.dll", SystemDirectories.Bin, fileName));

                if (!File.Exists(currentAss))
                    return new LiteralControl("Unable to load user control because is does not exist: " + fileName);
                asm = Assembly.LoadFrom(currentAss);

                if (HttpContext.Current != null)
                    HttpContext.Current.Trace.Write("umbracoMacro", "Assembly file " + currentAss + " LOADED!!");
            }
            catch
            {
                throw new ArgumentException(string.Format("ASSEMBLY NOT LOADED PATH: {0} NOT FOUND!!",
                                                          IOHelper.MapPath(SystemDirectories.Bin + "/" + fileName +
                                                                           ".dll")));
            }

            if (HttpContext.Current != null)
                HttpContext.Current.Trace.Write("umbracoMacro",
                                                string.Format("Assembly Loaded from ({0}.dll)", fileName));
            type = asm.GetType(controlName);
            if (type == null)
                return new LiteralControl(string.Format("Unable to get type {0} from assembly {1}",
                                                        controlName, asm.FullName));

            var control = Activator.CreateInstance(type) as Control;
            if (control == null)
                return new LiteralControl(string.Format("Unable to create control {0} from assembly {1}",
                                                        controlName, asm.FullName));

            AddCurrentNodeToControl(control, type);

            // Properties
            foreach (string propertyAlias in properties.Keys)
            {
                PropertyInfo prop = type.GetProperty(propertyAlias);
                if (prop == null)
                {
                    if (HttpContext.Current != null)
                        HttpContext.Current.Trace.Warn("macro",
                                                       string.Format("control property '{0}' doesn't exist or aren't accessible (public)",
                                                                     propertyAlias));

                    continue;
                }

                MacroPropertyModel propModel = model.Properties.Find(m => m.Key == propertyAlias.ToLower());
                // zb-00037 #29875 : values have already been parsed + no need to parse ""
                object propValue = propModel != null && prop != null ? propModel.Value : "";
                // Special case for types of webControls.unit
                if (prop.PropertyType == typeof(Unit))
                    propValue = Unit.Parse(propValue.ToString());
                else
                {
                    foreach (object s in propertyDefinitions)
                    {
                        if (s != null)
                            Trace.Write("macroProp", s.ToString());
                    }

                    Trace.Warn("macro", propertyAlias);

                    object o = propertyDefinitions[propertyAlias];
                    if (o == null)
                        continue;
                    var st = (TypeCode)Enum.Parse(typeof(TypeCode), o.ToString(), true);

                    // Special case for booleans
                    if (prop.PropertyType == typeof(bool))
                    {
                        bool parseResult;
                        if (
                            Boolean.TryParse(propValue.ToString().Replace("1", "true").Replace("0", "false"),
                                             out parseResult))
                            propValue = parseResult;
                        else
                            propValue = false;
                    }
                    else
                        propValue = Convert.ChangeType(propValue, st);
                }

                prop.SetValue(control, Convert.ChangeType(propValue, prop.PropertyType), null);
            }
            return control;
        }

        /// <summary>
        /// Loads an usercontrol using reflection into the macro object
        /// </summary>
        /// <param name="fileName">Filename of the usercontrol - ie. ~wulff.ascx</param>
        /// <param name="attributes">The attributes.</param>
        /// <param name="pageElements">The page elements.</param>
        /// <returns></returns>
        public Control loadUserControl(string fileName, MacroModel model, Hashtable pageElements)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileName), "fileName cannot be empty");
            Debug.Assert(model.Properties != null, "attributes cannot be null");
            Debug.Assert(pageElements != null, "pageElements cannot be null");
            try
            {
                string userControlPath = @"~/" + fileName;

                if (!File.Exists(IOHelper.MapPath(userControlPath)))
                    return new LiteralControl(string.Format("UserControl {0} does not exist.", fileName));

                var oControl = (UserControl)new UserControl().LoadControl(userControlPath);

                int slashIndex = fileName.LastIndexOf("/") + 1;
                if (slashIndex < 0)
                    slashIndex = 0;

                if (!String.IsNullOrEmpty(model.MacroControlIdentifier))
                    oControl.ID = model.MacroControlIdentifier;
                else
                    oControl.ID =
                        string.Format("{0}_{1}", fileName.Substring(slashIndex, fileName.IndexOf(".ascx") - slashIndex),
                                      StateHelper.GetContextValue<int>(macrosAddedKey));

                TraceInfo(loadUserControlKey, string.Format("Usercontrol added with id '{0}'", oControl.ID));

                Type type = oControl.GetType();
                if (type == null)
                {
                    TraceWarn(loadUserControlKey, "Unable to retrieve control type: " + fileName);
                    return oControl;
                }

                AddCurrentNodeToControl(oControl, type);

                foreach (string propertyAlias in properties.Keys)
                {
                    PropertyInfo prop = type.GetProperty(propertyAlias);
                    if (prop == null)
                    {
                        TraceWarn(loadUserControlKey, "Unable to retrieve type from propertyAlias: " + propertyAlias);
                        continue;
                    }

                    MacroPropertyModel propModel = model.Properties.Find(m => m.Key == propertyAlias.ToLower());
                    // zb-00037 #29875 : values have already been parsed + no need to parse ""
                    object propValue = propModel != null && prop != null ? propModel.Value : null;

                    if (propValue == null)
                        continue;

                    // Special case for types of webControls.unit
                    try
                    {
                        if (prop.PropertyType == typeof(Unit))
                            propValue = Unit.Parse(propValue.ToString());
                        else
                        {
                            try
                            {
                                object o = propertyDefinitions[propertyAlias];
                                if (o == null)
                                    continue;
                                var st = (TypeCode)Enum.Parse(typeof(TypeCode), o.ToString(), true);

                                // Special case for booleans
                                if (prop.PropertyType == typeof(bool))
                                {
                                    bool parseResult;
                                    if (
                                        Boolean.TryParse(
                                            propValue.ToString().Replace("1", "true").Replace("0", "false"),
                                            out parseResult))
                                        propValue = parseResult;
                                    else
                                        propValue = false;
                                }
                                else
                                    propValue = Convert.ChangeType(propValue, st);

                                Trace.Write("macro.loadControlProperties",
                                            string.Format("Property added '{0}' with value '{1}'", propertyAlias,
                                                          propValue));
                            }
                            catch (Exception PropException)
                            {
                                HttpContext.Current.Trace.Warn("macro.loadControlProperties",
                                                               string.Format(
                                                                   "Error adding property '{0}' with value '{1}'",
                                                                   propertyAlias, propValue), PropException);
                            }
                        }

                        prop.SetValue(oControl, Convert.ChangeType(propValue, prop.PropertyType), null);
                    }
                    catch (Exception propException)
                    {
                        HttpContext.Current.Trace.Warn("macro.loadControlProperties",
                                                       string.Format(
                                                           "Error adding property '{0}' with value '{1}', maybe it doesn't exists or maybe casing is wrong!",
                                                           propertyAlias, propValue), propException);
                    }
                }
                return oControl;
            }
            catch (Exception e)
            {
                HttpContext.Current.Trace.Warn("macro", string.Format("Error creating usercontrol ({0})", fileName), e);
                return new LiteralControl(
                    string.Format(
                        "<div style=\"color: black; padding: 3px; border: 2px solid red\"><b style=\"color:red\">Error creating control ({0}).</b><br/> Maybe file doesn't exists or the usercontrol has a cache directive, which is not allowed! See the tracestack for more information!</div>",
                        fileName));
            }
        }

        private static void AddCurrentNodeToControl(Control control, Type type)
        {
            var currentNodeProperty = type.GetProperty("CurrentNode");
            if (currentNodeProperty != null && currentNodeProperty.CanWrite && currentNodeProperty.PropertyType.IsAssignableFrom(typeof(Node)))
            {
                currentNodeProperty.SetValue(control, Node.GetCurrent(), null);
            }
            currentNodeProperty = type.GetProperty("currentNode");
            if (currentNodeProperty != null && currentNodeProperty.CanWrite && currentNodeProperty.PropertyType.IsAssignableFrom(typeof(Node)))
            {
                currentNodeProperty.SetValue(control, Node.GetCurrent(), null);
            }
        }

        private void TraceInfo(string category, string message)
        {
            if (HttpContext.Current != null)
                HttpContext.Current.Trace.Write(category, message);
        }

        private void TraceWarn(string category, string message)
        {
            if (HttpContext.Current != null)
                HttpContext.Current.Trace.Warn(category, message);
        }

        /// <summary>
        /// For debug purposes only - should be deleted or made private
        /// </summary>
        /// <param name="type">The type of object (control) to show properties from</param>
        public void macroProperties(Type type)
        {
            PropertyInfo[] myProperties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            HttpContext.Current.Response.Write("<p>" + type.Name + "<br />");
            foreach (PropertyInfo propertyItem in myProperties)
            {
                //				if (propertyItem.CanWrite) 
                HttpContext.Current.Response.Write(propertyItem.Name + " (" + propertyItem.PropertyType +
                                                   ")<br />");
            }
            HttpContext.Current.Response.Write("</p>");
        }

        public static string renderMacroStartTag(Hashtable attributes, int pageId, Guid versionId)
        {
            string div = "<div ";

            IDictionaryEnumerator ide = attributes.GetEnumerator();
            while (ide.MoveNext())
            {
                div += string.Format("umb_{0}=\"{1}\" ", ide.Key, encodeMacroAttribute(ide.Value.ToString()));
            }

            div += "ismacro=\"true\" onresizestart=\"return false;\" umbVersionId=\"" + versionId +
                   "\" umbPageid=\"" +
                   pageId +
                   "\" title=\"This is rendered content from macro\" class=\"umbMacroHolder\"><!-- startUmbMacro -->";

            return div;
        }

        private static string encodeMacroAttribute(string attributeContents)
        {
            // Replace linebreaks
            attributeContents = attributeContents.Replace("\n", "\\n").Replace("\r", "\\r");

            // Replace quotes
            attributeContents =
                attributeContents.Replace("\"", "&quot;");

            // Replace tag start/ends
            attributeContents =
                attributeContents.Replace("<", "&lt;").Replace(">", "&gt;");


            return attributeContents;
        }

        public static string renderMacroEndTag()
        {
            return "<!-- endUmbMacro --></div>";
        }

        public static string GetRenderedMacro(int MacroId, page umbPage, Hashtable attributes, int pageId)
        {
            var m = new macro(MacroId);
            Control c = m.renderMacro(attributes, umbPage.Elements, pageId);
            TextWriter writer = new StringWriter();
            var ht = new HtmlTextWriter(writer);
            c.RenderControl(ht);
            string result = writer.ToString();

            // remove hrefs
            string pattern = "href=\"([^\"]*)\"";
            MatchCollection hrefs =
                Regex.Matches(result, pattern, RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            foreach (Match href in hrefs)
                result = result.Replace(href.Value, "href=\"javascript:void(0)\"");

            return result;
        }

        public static string MacroContentByHttp(int PageID, Guid PageVersion, Hashtable attributes)
        {
            string tempAlias = (attributes["macroalias"] != null)
                                   ? attributes["macroalias"].ToString()
                                   : attributes["macroAlias"].ToString();
            if (!ReturnFromAlias(tempAlias).DontRenderInEditor)
            {
                string querystring = "umbPageId=" + PageID + "&umbVersionId=" + PageVersion;
                IDictionaryEnumerator ide = attributes.GetEnumerator();
                while (ide.MoveNext())
                    querystring += "&umb_" + ide.Key + "=" + HttpContext.Current.Server.UrlEncode(ide.Value.ToString());

                // Create a new 'HttpWebRequest' Object to the mentioned URL.
                string retVal = string.Empty;
                string url = "http://" + HttpContext.Current.Request.ServerVariables["SERVER_NAME"] + ":" +
                             HttpContext.Current.Request.ServerVariables["SERVER_PORT"] +
                             IOHelper.ResolveUrl(SystemDirectories.Umbraco) +
                             "/macroResultWrapper.aspx?" +
                             querystring;

                var myHttpWebRequest = (HttpWebRequest)WebRequest.Create(url);

                // propagate the user's context
                // zb-00004 #29956 : refactor cookies names & handling
                HttpCookie inCookie = StateHelper.Cookies.UserContext.RequestCookie;
                var cookie = new Cookie(inCookie.Name, inCookie.Value, inCookie.Path,
                                        HttpContext.Current.Request.ServerVariables["SERVER_NAME"]);
                myHttpWebRequest.CookieContainer = new CookieContainer();
                myHttpWebRequest.CookieContainer.Add(cookie);

                // Assign the response object of 'HttpWebRequest' to a 'HttpWebResponse' variable.
                HttpWebResponse myHttpWebResponse = null;
                try
                {
                    myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                    if (myHttpWebResponse.StatusCode == HttpStatusCode.OK)
                    {
                        Stream streamResponse = myHttpWebResponse.GetResponseStream();
                        var streamRead = new StreamReader(streamResponse);
                        var readBuff = new Char[256];
                        int count = streamRead.Read(readBuff, 0, 256);
                        while (count > 0)
                        {
                            var outputData = new String(readBuff, 0, count);
                            retVal += outputData;
                            count = streamRead.Read(readBuff, 0, 256);
                        }
                        // Close the Stream object.
                        streamResponse.Close();
                        streamRead.Close();

                        // Find the content of a form
                        string grabStart = "<!-- grab start -->";
                        string grabEnd = "<!-- grab end -->";
                        int grabStartPos = retVal.IndexOf(grabStart) + grabStart.Length;
                        int grabEndPos = retVal.IndexOf(grabEnd) - grabStartPos;
                        retVal = retVal.Substring(grabStartPos, grabEndPos);
                    }
                    else
                        retVal = "<span style=\"color: green\">No macro content available for WYSIWYG editing</span>";

                    // Release the HttpWebResponse Resource.
                    myHttpWebResponse.Close();
                }
                catch (Exception ee)
                {
                    retVal = "<span style=\"color: green\">No macro content available for WYSIWYG editing</span>";
                }
                finally
                {
                    // Release the HttpWebResponse Resource.
                    if (myHttpWebResponse != null)
                        myHttpWebResponse.Close();
                }

                return retVal.Replace("\n", string.Empty).Replace("\r", string.Empty);
            }
            else
                return "<span style=\"color: green\">This macro does not provides rendering in WYSIWYG editor</span>";
        }

        /// <summary>
        /// Adds the XSLT extension namespaces to the XSLT header using 
        /// {0} as the container for the namespace references and
        /// {1} as the container for the exclude-result-prefixes
        /// </summary>
        /// <param name="xslt">The XSLT</param>
        /// <returns></returns>
        public static string AddXsltExtensionsToHeader(string xslt)
        {
            var namespaceList = new StringBuilder();
            var namespaceDeclaractions = new StringBuilder();
            foreach (var extension in GetXsltExtensions())
            {
                namespaceList.Append(extension.Key).Append(' ');
                namespaceDeclaractions.AppendFormat("xmlns:{0}=\"urn:{0}\" ", extension.Key);
            }

            // parse xslt
            xslt = xslt.Replace("{0}", namespaceDeclaractions.ToString());
            xslt = xslt.Replace("{1}", namespaceList.ToString());
            return xslt;
        }

    }

    public class MacroCacheContent
    {
        private readonly Control _control;
        private readonly string _id;

        public MacroCacheContent(Control control, string ID)
        {
            _control = control;
            _id = ID;
        }

        public string ID
        {
            get { return _id; }
        }

        public Control Content
        {
            get { return _control; }
        }
    }

    public class macroCacheRefresh : ICacheRefresher
    {
        #region ICacheRefresher Members

        public string Name
        {
            get
            {
                // TODO:  Add templateCacheRefresh.Name getter implementation
                return "Macro cache refresher";
            }
        }

        public Guid UniqueIdentifier
        {
            get
            {
                // TODO:  Add templateCacheRefresh.UniqueIdentifier getter implementation
                return new Guid("7B1E683C-5F34-43dd-803D-9699EA1E98CA");
            }
        }

        public void RefreshAll()
        {
            macro.ClearAliasCache();
        }

        public void Refresh(Guid Id)
        {
            // Doesn't do anything
        }

        void ICacheRefresher.Refresh(int Id)
        {
            new macro(Id).removeFromCache();
        }

        void ICacheRefresher.Remove(int Id)
        {
            new macro(Id).removeFromCache();
        }

        #endregion
    }

    /// <summary>
    /// Allows App_Code XSLT extensions to be declared using the [XsltExtension] class attribute.
    /// </summary>
    /// <remarks>
    /// An optional XML namespace can be specified using [XsltExtension("MyNamespace")].
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    [AspNetHostingPermission(SecurityAction.Demand, Level = AspNetHostingPermissionLevel.Medium, Unrestricted = false)]
    public class XsltExtensionAttribute : Attribute
    {
        public XsltExtensionAttribute()
        {
            Namespace = String.Empty;
        }

        public XsltExtensionAttribute(string ns)
        {
            Namespace = ns;
        }

        public string Namespace { get; set; }

        public override string ToString()
        {
            return Namespace;
        }
    }
}
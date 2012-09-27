using System;
using System.Linq;
using System.Collections;
using System.Xml;
using umbraco.DataLayer;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using umbraco.cms.businesslogic.cache;
using umbraco.BusinessLogic;
using umbraco.IO;
using umbraco.cms.businesslogic.web;

namespace umbraco.cms.businesslogic.template
{
    /// <summary>
    /// Summary description for Template.
    /// </summary>
    public class Template : CMSNode
    {


        #region Private members

        private string _OutputContentType;
        private string _design;
        private string _alias;
        private string _oldAlias;
        private int _mastertemplate;
        private bool _hasChildrenInitialized = false;
        private bool _hasChildren;

        #endregion

        #region Static members

        public static readonly string UmbracoMasterTemplate = SystemDirectories.Umbraco + "/masterpages/default.master";
        private static Hashtable _templateAliases = new Hashtable();
        private static volatile bool _templateAliasesInitialized = false;
        private static object templateCacheSyncLock = new object();
        private static readonly string UmbracoTemplateCacheKey = "UmbracoTemplateCache";
        private static object _templateLoaderLocker = new object();
        private static Guid _objectType = new Guid("6fbde604-4178-42ce-a10b-8a2600a2f07d");

        #endregion

        public string MasterPageFile
        {
            get { return IOHelper.MapPath(SystemDirectories.Masterpages + "/" + Alias.Replace(" ", "") + ".master"); }
        }

        public static Hashtable TemplateAliases
        {
            get { return _templateAliases; }
            set { _templateAliases = value; }
        }

        #region Constructors
        public Template(int id) : base(id) { }

        public Template(Guid id) : base(id) { }
        #endregion

        /// <summary>
        /// Used to persist object changes to the database. In Version3.0 it's just a stub for future compatibility
        /// </summary>
        public override void Save()
        {
            SaveEventArgs e = new SaveEventArgs();
            FireBeforeSave(e);

            if (!e.Cancel)
            {
                FlushCache();
                base.Save();
                FireAfterSave(e);
            }
        }

        public string GetRawText()
        {
            return base.Text;
        }

        public override string Text
        {
            get
            {
                string tempText = base.Text;
                if (!tempText.StartsWith("#"))
                    return tempText;
                else
                {
                    language.Language lang = language.Language.GetByCultureCode(System.Threading.Thread.CurrentThread.CurrentCulture.Name);
                    if (lang != null)
                    {
                        if (Dictionary.DictionaryItem.hasKey(tempText.Substring(1, tempText.Length - 1)))
                        {
                            Dictionary.DictionaryItem di = new Dictionary.DictionaryItem(tempText.Substring(1, tempText.Length - 1));
                            if (di != null)
                                return di.Value(lang.id);
                        }
                    }

                    return "[" + tempText + "]";
                }
            }
            set
            {
                FlushCache();
                base.Text = value;
            }
        }

        public string OutputContentType
        {
            get { return _OutputContentType; }
            set { _OutputContentType = value; }
        }

        protected override void setupNode()
        {
            base.setupNode();

            IRecordsReader dr = SqlHelper.ExecuteReader("Select alias,design,master from cmsTemplate where nodeId = " + this.Id);
            bool hasRows = dr.Read();
            if (hasRows)
            {
                _alias = dr.GetString("alias");
                _design = dr.GetString("design");
                //set the master template to zero if it's null
                _mastertemplate = dr.IsNull("master") ? 0 : dr.GetInt("master");
            }
            dr.Close();
            
            if (UmbracoSettings.EnableMvcSupport && Template.HasView(this))
                _design = ViewHelper.GetViewFile(this);
            else
                _design = MasterpageHelper.GetMasterpageFile(this);

        }

        private bool isMasterPageSyntax(string code)
        {
            return code.Contains("<%@ Master") || code.Contains("<umbraco:Item") || code.Contains("<asp:") || code.Contains("<umbraco:Macro");
        }

        public new string Path
        {
            get
            {
                List<int> path = new List<int>();
                Template working = this;
                while (working != null)
                {
                    path.Add(working.Id);
                    try
                    {
                        if (working.MasterTemplate != 0)
                        {
                            working = new Template(working.MasterTemplate);
                        }
                        else
                        {
                            working = null;
                        }
                    }
                    catch (ArgumentException)
                    {
                        working = null;
                    }
                }
                path.Add(-1);
                path.Reverse();
                string sPath = string.Join(",", path.ConvertAll(item => item.ToString()).ToArray());
                return sPath;
            }
            set
            {
                base.Path = value;
            }
        }

        public string Alias
        {
            get { return _alias; }
            set
            {
                FlushCache();
                _oldAlias = _alias;
                _alias = value;

                SqlHelper.ExecuteNonQuery("Update cmsTemplate set alias = @alias where NodeId = " + this.Id, SqlHelper.CreateParameter("@alias", _alias));
                _templateAliasesInitialized = false;

                initTemplateAliases();
            }

        }

        public bool HasMasterTemplate
        {
            get { return (_mastertemplate > 0); }
        }


        public override bool HasChildren
        {
            get
            {
                if (!_hasChildrenInitialized)
                {
                    _hasChildren = SqlHelper.ExecuteScalar<int>("select count(NodeId) as tmp from cmsTemplate where master = " + Id) > 0;
                }
                return _hasChildren;
            }
            set
            {
                _hasChildrenInitialized = true;
                _hasChildren = value;
            }
        }

        public int MasterTemplate
        {
            get { return _mastertemplate; }
            set
            {
                FlushCache();
                _mastertemplate = value;

                //set to null if it's zero
                object masterVal = value;
                if (value == 0) masterVal = DBNull.Value;

                SqlHelper.ExecuteNonQuery("Update cmsTemplate set master = @master where NodeId = @nodeId",
                    SqlHelper.CreateParameter("@master", masterVal),
                    SqlHelper.CreateParameter("@nodeId", this.Id));
            }
        }

        public string Design
        {
            get { return _design; }
            set
            {
                FlushCache();

                _design = value.Trim(Environment.NewLine.ToCharArray());
                // NH: Removing an generating the directive can mess up code behind
                // We don't store the masterpage directive in the design value
                //                if (_design.StartsWith("<%@"))
                //                    _design = _design.Substring(_design.IndexOf("%>") + 3).Trim(Environment.NewLine.ToCharArray());


                //we only switch to MVC View editing if the template has a view file, and MVC editing is enabled
                if (UmbracoSettings.EnableMvcSupport && !isMasterPageSyntax(_design))
                    _design = ViewHelper.UpdateViewFile(this);
                else if (UmbracoSettings.UseAspNetMasterPages)
                    _design = MasterpageHelper.UpdateMasterpageFile(this, _oldAlias);
                

                SqlHelper.ExecuteNonQuery("Update cmsTemplate set design = @design where NodeId = @id",
                        SqlHelper.CreateParameter("@design", _design),
                        SqlHelper.CreateParameter("@id", Id));
            }
        }

        public XmlNode ToXml(XmlDocument doc)
        {
            XmlNode template = doc.CreateElement("Template");
            template.AppendChild(xmlHelper.addTextNode(doc, "Name", this.Text));
            template.AppendChild(xmlHelper.addTextNode(doc, "Alias", this.Alias));

            if (this.MasterTemplate != 0)
            {
                template.AppendChild(xmlHelper.addTextNode(doc, "Master", new Template(this.MasterTemplate).Alias));
            }

            template.AppendChild(xmlHelper.addCDataNode(doc, "Design", this.Design));

            return template;
        }

        /// <summary>
        /// Removes any references to this templates from child templates, documenttypes and documents
        /// </summary>
        public void RemoveAllReferences()
        {
            if (HasChildren)
            {
                foreach (Template t in Template.GetAllAsList().FindAll(delegate(Template t) { return t.MasterTemplate == this.Id; }))
                {
                    t.MasterTemplate = 0;
                }
            }

            RemoveFromDocumentTypes();

            // remove from documents
            Document.RemoveTemplateFromDocument(this.Id);


        }

        public void RemoveFromDocumentTypes()
        {
            foreach (DocumentType dt in DocumentType.GetAllAsList().Where(x => x.allowedTemplates.Select(t => t.Id).Contains(this.Id)))
            {
                dt.RemoveTemplate(this.Id);
            }
        }

        public IEnumerable<DocumentType> GetDocumentTypes()
        {
            return DocumentType.GetAllAsList().Where(x => x.allowedTemplates.Select(t => t.Id).Contains(this.Id));
        }

        public static Template MakeNew(string Name, BusinessLogic.User u, Template master)
        {
            Template t = MakeNew(Name, u);
            t.MasterTemplate = master.Id;

            if (UmbracoSettings.EnableMvcSupport)
                ViewHelper.CreateViewFile(t, true);
            else
                MasterpageHelper.CreateMasterpageFile(t, true);
            
            

            /*
            if (UmbracoSettings.UseAspNetMasterPages)
            {
                string design = t.getMasterPageHeader() + "\n";

                foreach (string cpId in master.contentPlaceholderIds())
                {
                    design += "<asp:content ContentPlaceHolderId=\"" + cpId + "\" runat=\"server\">\n\t\n</asp:content>\n\n";
                }

                t.Design = design;
            }*/

            t.Save();
            return t;
        }

        public static Template MakeNew(string name, BusinessLogic.User u)
        {

            // CMSNode MakeNew(int parentId, Guid objectType, int userId, int level, string text, Guid uniqueID)
            CMSNode n = CMSNode.MakeNew(-1, _objectType, u.Id, 1, name, Guid.NewGuid());

            //ensure unique alias 
            name = helpers.Casing.SafeAlias(name);
            if (GetByAlias(name) != null)
                name = EnsureUniqueAlias(name, 1);
            name = name.Replace("/", ".").Replace("\\", "");

            if (name.Length > 100)
                name = name.Substring(0, 95) + "...";

          


            SqlHelper.ExecuteNonQuery("INSERT INTO cmsTemplate (NodeId, Alias, design, master) VALUES (@nodeId, @alias, @design, @master)",
                                      SqlHelper.CreateParameter("@nodeId", n.Id),
                                      SqlHelper.CreateParameter("@alias", name),
                                      SqlHelper.CreateParameter("@design", ' '),
                                      SqlHelper.CreateParameter("@master", DBNull.Value));

            Template t = new Template(n.Id);
            NewEventArgs e = new NewEventArgs();
            t.OnNew(e);

            if (UmbracoSettings.EnableMvcSupport)
                t._design = ViewHelper.CreateViewFile(t);
            else
                t._design = MasterpageHelper.CreateMasterpageFile(t);


            return t;
        }

        private static string EnsureUniqueAlias(string alias, int attempts)
        {
            if (GetByAlias(alias + attempts.ToString()) == null)
                return alias + attempts.ToString();
            else
            {
                attempts++;
                return EnsureUniqueAlias(alias, attempts);
            }
        }

        public static Template GetByAlias(string Alias)
        {
            try
            {
                return new Template(SqlHelper.ExecuteScalar<int>("select nodeId from cmsTemplate where alias = @alias", SqlHelper.CreateParameter("@alias", Alias)));
            }
            catch
            {
                return null;
            }
        }

        [Obsolete("Obsolete, please use GetAllAsList() method instead", true)]
        public static Template[] getAll()
        {
            return GetAllAsList().ToArray();
        }

        public static List<Template> GetAllAsList()
        {
            Guid[] ids = CMSNode.TopMostNodeIds(_objectType);
            List<Template> retVal = new List<Template>();
            foreach (Guid id in ids)
            {
                retVal.Add(new Template(id));
            }
            retVal.Sort(delegate(Template t1, Template t2) { return t1.Text.CompareTo(t2.Text); });
            return retVal;
        }

        public static int GetTemplateIdFromAlias(string alias)
        {
            alias = alias.ToLower();

            initTemplateAliases();
            if (TemplateAliases.ContainsKey(alias))
                return (int)TemplateAliases[alias];
            else
                return 0;
        }

        private static void initTemplateAliases()
        {
            if (!_templateAliasesInitialized)
            {
                lock (_templateLoaderLocker)
                {
                    //double check
                    if (!_templateAliasesInitialized)
                    {
                        _templateAliases.Clear();
                        foreach (Template t in GetAllAsList())
                            TemplateAliases.Add(t.Alias.ToLower(), t.Id);

                        _templateAliasesInitialized = true;
                    }

                }
            }
        }

        public override void delete()
        {
            // don't allow template deletion if it has child templates
            if (this.HasChildren)
            {
                Log.Add(LogTypes.Error, this.Id, "Can't delete a master template. Remove any bindings from child templates first.");
                throw new InvalidOperationException("Can't delete a master template. Remove any bindings from child templates first.");
            }

            // NH: Changed this; if you delete a template we'll remove all references instead of 
            // throwing an exception
            if (DocumentType.GetAllAsList().Where(x => x.allowedTemplates.Select(t => t.Id).Contains(this.Id)).Count() > 0)
            {
                // the uncommented code below have been refactored into removeAllReferences method that clears template
                // from documenttypes, subtemplates and documents.
                RemoveAllReferences();
                /*

                // Added to remove template doctype relationship
                SqlHelper.ExecuteNonQuery("delete from cmsDocumentType where templateNodeId =" + this.Id);

                // Need to update any other template that references this one as it's master to NULL
                SqlHelper.ExecuteNonQuery("update cmsTemplate set [master] = NULL where [master] = " + this.Id);
                */

                // don't allow template deletion if it is in use
                // (get all doc types and filter based on any that have the template id of this one)
                /*
                Log.Add(LogTypes.Error, this.Id, "Can't delete a template that is assigned to existing content");
                throw new InvalidOperationException("Can't delete a template that is assigned to existing content");
            */
            }

            DeleteEventArgs e = new DeleteEventArgs();
            FireBeforeDelete(e);

            if (!e.Cancel)
            {
                //re-set the template aliases
                _templateAliasesInitialized = false;
                initTemplateAliases();

                //delete the template
                SqlHelper.ExecuteNonQuery("delete from cmsTemplate where NodeId =" + this.Id);

                base.delete();

                // remove masterpages
                if (System.IO.File.Exists(MasterPageFile))
                    System.IO.File.Delete(MasterPageFile);

                if (System.IO.File.Exists(Umbraco.Core.IO.IOHelper.MapPath(ViewHelper.ViewPath(this))))
                    System.IO.File.Delete(Umbraco.Core.IO.IOHelper.MapPath(ViewHelper.ViewPath(this)));

                FireAfterDelete(e);
            }
        }

        [Obsolete("This method, doesnt actually do anything, as the file is created when the design is set", false)]
        public void _SaveAsMasterPage()
        {
            //SaveMasterPageFile(ConvertToMasterPageSyntax(Design));
        }

        public string GetMasterContentElement(int masterTemplateId)
        {
            if (masterTemplateId != 0)
            {
                string masterAlias = new Template(masterTemplateId).Alias.Replace(" ", "");
                return
                    String.Format("<asp:Content ContentPlaceHolderID=\"{1}ContentPlaceHolder\" runat=\"server\">",
                    Alias.Replace(" ", ""), masterAlias);
            }
            else
                return
                    String.Format("<asp:Content ContentPlaceHolderID=\"ContentPlaceHolderDefault\" runat=\"server\">",
                    Alias.Replace(" ", ""));
        }

        public List<string> contentPlaceholderIds()
        {
            List<string> retVal = new List<string>();

            string masterPageFile = this.MasterPageFile;
            string mp = System.IO.File.ReadAllText(masterPageFile);
            string pat = "<asp:ContentPlaceHolder+(\\s+[a-zA-Z]+\\s*=\\s*(\"([^\"]*)\"|'([^']*)'))*\\s*/?>";
            Regex r = new Regex(pat, RegexOptions.IgnoreCase);
            Match m = r.Match(mp);

            while (m.Success)
            {
                CaptureCollection cc = m.Groups[3].Captures;
                foreach (Capture c in cc)
                {
                    if (c.Value != "server")
                        retVal.Add(c.Value);
                }

                m = m.NextMatch();
            }

            return retVal;
        }



        public string ConvertToMasterPageSyntax(string templateDesign)
        {
            string masterPageContent = GetMasterContentElement(MasterTemplate) + "\n";

            masterPageContent += templateDesign;

            // Parse the design for getitems
            masterPageContent = EnsureMasterPageSyntax(masterPageContent);

            // append ending asp:content element
            masterPageContent += "\n</asp:Content>" + Environment.NewLine;

            return masterPageContent;
        }

        public string EnsureMasterPageSyntax(string masterPageContent)
        {
            replaceElement(ref masterPageContent, "?UMBRACO_GETITEM", "umbraco:Item", true);
            replaceElement(ref masterPageContent, "?UMBRACO_GETITEM", "umbraco:Item", false);

            // Parse the design for macros
            replaceElement(ref masterPageContent, "?UMBRACO_MACRO", "umbraco:Macro", true);
            replaceElement(ref masterPageContent, "?UMBRACO_MACRO", "umbraco:Macro", false);

            // Parse the design for load childs
            masterPageContent = masterPageContent.Replace("<?UMBRACO_TEMPLATE_LOAD_CHILD/>", getAspNetMasterPageContentContainer()).Replace("<?UMBRACO_TEMPLATE_LOAD_CHILD />", getAspNetMasterPageContentContainer());
            // Parse the design for aspnet forms
            getAspNetMasterPageForm(ref masterPageContent);
            masterPageContent = masterPageContent.Replace("</?ASPNET_FORM>", "</form>");
            // Parse the design for aspnet heads
            masterPageContent = masterPageContent.Replace("</ASPNET_HEAD>", String.Format("<head id=\"{0}Head\" runat=\"server\">", Alias.Replace(" ", "")));
            masterPageContent = masterPageContent.Replace("</?ASPNET_HEAD>", "</head>");
            return masterPageContent;
        }



        public void ImportDesign(string design)
        {
            Design = design; 

            /*
            if (!isMasterPageSyntax(design))
            {
                Design = ConvertToMasterPageSyntax(design);
            }
            else
            {
                Design = design;
            }*/

        }

        public void SaveMasterPageFile(string masterPageContent)
        {
            //this will trigger the helper and store everything
            this.Design = masterPageContent;

            /*

            // Add header to master page if it doesn't exist
            if (!masterPageContent.StartsWith("<%@"))
            {
                masterPageContent = getMasterPageHeader() + "\n" + masterPageContent;
            }
            else
            {
                // verify that the masterpage attribute is the same as the masterpage
                string masterHeader = masterPageContent.Substring(0, masterPageContent.IndexOf("%>") + 2).Trim(Environment.NewLine.ToCharArray());
                // find the masterpagefile attribute
                MatchCollection m = Regex.Matches(masterHeader, "(?<attributeName>\\S*)=\"(?<attributeValue>[^\"]*)\"",
                                  RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                foreach (Match attributeSet in m)
                {
                    if (attributeSet.Groups["attributeName"].Value.ToLower() == "masterpagefile")
                    {
                        // validate the masterpagefile
                        string currentMasterPageFile = attributeSet.Groups["attributeValue"].Value;
                        string currentMasterTemplateFile = currentMasterTemplateFileName();
                        if (currentMasterPageFile != currentMasterTemplateFile)
                        {
                            masterPageContent =
                                masterPageContent.Replace(
                                attributeSet.Groups["attributeName"].Value + "=\"" + currentMasterPageFile + "\"",
                                attributeSet.Groups["attributeName"].Value + "=\"" + currentMasterTemplateFile + "\"");

                        }
                    }
                }

            }

            //we have a Old Alias if the alias and therefor the masterpage file name has changed...
            //so before we save the new masterfile, we'll clear the old one, so we don't up with 
            //Unused masterpage files
            if (!string.IsNullOrEmpty(_oldAlias) && _oldAlias != _alias)
            {

                //Ensure that child templates have the right master masterpage file name
                if (HasChildren)
                {
                    //store children array here because iterating over an Array property object is very inneficient.
                    var c = Children;
                    foreach (CMSNode cmn in c)
                    {
                        Template t = new Template(cmn.Id);
                        t.SaveAsMasterPage();
                        t.Save();
                    }
                }

                //then kill the old file.. 
                string _oldFile = IOHelper.MapPath(SystemDirectories.Masterpages + "/" + _oldAlias.Replace(" ", "") + ".master");

                if (System.IO.File.Exists(_oldFile))
                    System.IO.File.Delete(_oldFile);
            }

            // save the file in UTF-8

            File.WriteAllText(MasterPageFile, masterPageContent, System.Text.Encoding.UTF8);
             * */
        }

        private string getMasterPageHeader()
        {
            return String.Format("<%@ Master Language=\"C#\" MasterPageFile=\"{0}\" AutoEventWireup=\"true\" %>",
                currentMasterTemplateFileName()) + Environment.NewLine;
        }

        private string currentMasterTemplateFileName()
        {
            if (MasterTemplate != 0)
                return SystemDirectories.Masterpages + "/" + new Template(MasterTemplate).Alias.Replace(" ", "") + ".master";
            else
                return UmbracoMasterTemplate;
        }

        private void getAspNetMasterPageForm(ref string design)
        {
            Match formElement = Regex.Match(design, getElementRegExp("?ASPNET_FORM", false), RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            if (formElement != null && formElement.Value != "")
            {
                string formReplace = String.Format("<form id=\"{0}Form\" runat=\"server\">", Alias.Replace(" ", ""));
                if (formElement.Groups.Count == 0)
                {
                    formReplace += "<asp:scriptmanager runat=\"server\"></asp:scriptmanager>";
                }
                design = design.Replace(formElement.Value, formReplace);
            }
        }

        private string getAspNetMasterPageContentContainer()
        {
            return String.Format(
                "<asp:ContentPlaceHolder ID=\"{0}ContentPlaceHolder\" runat=\"server\"></asp:ContentPlaceHolder>",
                Alias.Replace(" ", ""));
        }

        private void replaceElement(ref string design, string elementName, string newElementName, bool checkForQuotes)
        {
            MatchCollection m =
                Regex.Matches(design, getElementRegExp(elementName, checkForQuotes),
                  RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

            foreach (Match match in m)
            {
                GroupCollection groups = match.Groups;

                // generate new element (compensate for a closing trail on single elements ("/"))
                string elementAttributes = groups[1].Value;
                // test for macro alias
                if (elementName == "?UMBRACO_MACRO")
                {
                    Hashtable tags = helpers.xhtml.ReturnAttributes(match.Value);
                    if (tags["macroAlias"] != null)
                        elementAttributes = String.Format(" Alias=\"{0}\"", tags["macroAlias"].ToString()) + elementAttributes;
                    else if (tags["macroalias"] != null)
                        elementAttributes = String.Format(" Alias=\"{0}\"", tags["macroalias"].ToString()) + elementAttributes;
                }
                string newElement = "<" + newElementName + " runat=\"server\" " + elementAttributes.Trim() + ">";
                if (elementAttributes.EndsWith("/"))
                {
                    elementAttributes = elementAttributes.Substring(0, elementAttributes.Length - 1);
                }
                else if (groups[0].Value.StartsWith("</"))
                    // It's a closing element, so generate that instead of a starting element
                    newElement = "</" + newElementName + ">";

                if (checkForQuotes)
                {
                    // if it's inside quotes, we'll change element attribute quotes to single quotes
                    newElement = newElement.Replace("\"", "'");
                    newElement = String.Format("\"{0}\"", newElement);
                }
                design = design.Replace(match.Value, newElement);
            }
        }



        private string getElementRegExp(string elementName, bool checkForQuotes)
        {
            if (checkForQuotes)
                return String.Format("\"<[^>\\s]*\\b{0}(\\b[^>]*)>\"", elementName);
            else
                return String.Format("<[^>\\s]*\\b{0}(\\b[^>]*)>", elementName);

        }


        protected virtual void FlushCache()
        {
            // clear local cache
            cache.Cache.ClearCacheItem(GetCacheKey(Id));
        }

        public static Template GetTemplate(int id)
        {
            return Cache.GetCacheItem<Template>(GetCacheKey(id), templateCacheSyncLock,
                TimeSpan.FromMinutes(30),
                delegate
                {
                    try
                    {
                        return new Template(id);
                    }
                    catch
                    {
                        return null;
                    }
                });
        }

        private void InvalidateCache()
        {
            Cache.ClearCacheItem(GetCacheKey(this.Id));
        }

        private static string GetCacheKey(int id)
        {
            return UmbracoTemplateCacheKey + id;
        }


        public static Template Import(XmlNode n, User u)
        {
            string alias = xmlHelper.GetNodeValue(n.SelectSingleNode("Alias"));

            Template t = Template.GetByAlias(alias);

            if (t == null)
            {
                t = MakeNew(xmlHelper.GetNodeValue(n.SelectSingleNode("Name")), u);
            }

            t.Alias = alias;
            t.ImportDesign(xmlHelper.GetNodeValue(n.SelectSingleNode("Design")));

            return t;
        }

        public static bool HasView(Template t)
        {
            var path = Umbraco.Core.IO.SystemDirectories.MvcViews + "/" + t.Alias.Replace(" ", "") + ".cshtml";
            return System.IO.File.Exists(Umbraco.Core.IO.IOHelper.MapPath(path));
        }


        #region Events
        //EVENTS
        /// <summary>
        /// The save event handler
        /// </summary>
        public delegate void SaveEventHandler(Template sender, SaveEventArgs e);
        /// <summary>
        /// The new event handler
        /// </summary>
        public delegate void NewEventHandler(Template sender, NewEventArgs e);
        /// <summary>
        /// The delete event handler
        /// </summary>
        public delegate void DeleteEventHandler(Template sender, DeleteEventArgs e);


        /// <summary>
        /// Occurs when [before save].
        /// </summary>
        public static event SaveEventHandler BeforeSave;
        /// <summary>
        /// Raises the <see cref="E:BeforeSave"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void FireBeforeSave(SaveEventArgs e)
        {
            if (BeforeSave != null)
                BeforeSave(this, e);
        }

        /// <summary>
        /// Occurs when [after save].
        /// </summary>
        public static event SaveEventHandler AfterSave;
        /// <summary>
        /// Raises the <see cref="E:AfterSave"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void FireAfterSave(SaveEventArgs e)
        {
            if (AfterSave != null)
                AfterSave(this, e);
        }

        /// <summary>
        /// Occurs when [new].
        /// </summary>
        public static event NewEventHandler New;
        /// <summary>
        /// Raises the <see cref="E:New"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void OnNew(NewEventArgs e)
        {
            if (New != null)
                New(this, e);
        }

        /// <summary>
        /// Occurs when [before delete].
        /// </summary>
        public static event DeleteEventHandler BeforeDelete;
        /// <summary>
        /// Raises the <see cref="E:BeforeDelete"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void FireBeforeDelete(DeleteEventArgs e)
        {
            if (BeforeDelete != null)
                BeforeDelete(this, e);
        }

        /// <summary>
        /// Occurs when [after delete].
        /// </summary>
        public static event DeleteEventHandler AfterDelete;
        /// <summary>
        /// Raises the <see cref="E:AfterDelete"/> event.
        /// </summary>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        protected virtual void FireAfterDelete(DeleteEventArgs e)
        {
            if (AfterDelete != null)
                AfterDelete(this, e);
        }
        #endregion

    }
}

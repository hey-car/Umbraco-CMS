using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using ClientDependency.Core;
using Umbraco.Core;
using Umbraco.Core.Models;
using umbraco.cms.businesslogic.propertytype;
using umbraco.cms.businesslogic.web;
using umbraco.cms.helpers;
using umbraco.controls.GenericProperties;
using umbraco.IO;
using umbraco.presentation;
using umbraco.BasePages;
using ContentType = umbraco.cms.businesslogic.ContentType;
using PropertyType = Umbraco.Core.Models.PropertyType;

namespace umbraco.controls
{

    [ClientDependency(ClientDependencyType.Javascript, "ui/jqueryui.js", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Javascript, "ui/jquery.dd.js", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Css, "ui/dd.css", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Css, "Tree/treeIcons.css", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Css, "Tree/Themes/umbraco/style.css", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Css, "GenericProperty/genericproperty.css", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Javascript, "GenericProperty/genericproperty.js", "UmbracoClient")]
    [ClientDependency(ClientDependencyType.Javascript, "js/UmbracoCasingRules.aspx", "UmbracoRoot")]
    public partial class ContentTypeControlNew : UserControl
    {
        // General Private members
        private ContentType _contentType;
        private static string UmbracoPath = SystemDirectories.Umbraco;
        public bool HideStructure { get; set; }
        public Func<DocumentType, DocumentType> DocumentTypeCallback { get; set; }

        // "Tab" tab
        protected uicontrols.Pane Pane8;
        
        // "Structure" tab
        protected DualSelectbox DualAllowedContentTypes = new DualSelectbox();

        // "Info" tab
        public uicontrols.TabPage InfoTabPage;

        // "Generic properties" tab
        public uicontrols.TabPage GenericPropertiesTabPage;
        
        public GenericPropertyWrapper gp;
        private DataTable _dataTypeTable;
        private ArrayList _genericProperties = new ArrayList();
        private ArrayList _sortLists = new ArrayList();

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            LoadContentType();

            SetupInfoPane();
            if (!HideStructure)
            {
                SetupStructurePane();
            }
            SetupGenericPropertiesPane();
            SetupTabPane();

        }

        protected void Page_Load(object sender, EventArgs e)
        {
            pp_newTab.Text = ui.Text("newtab", UmbracoEnsuredPage.CurrentUser);
            pp_alias.Text = ui.Text("alias", UmbracoEnsuredPage.CurrentUser);
            pp_name.Text = ui.Text("name", UmbracoEnsuredPage.CurrentUser);
            pp_allowedChildren.Text = ui.Text("allowedchildnodetypes", UmbracoEnsuredPage.CurrentUser);
            pp_description.Text = ui.Text("editcontenttype", "description");
            pp_icon.Text = ui.Text("icon", UmbracoEnsuredPage.CurrentUser);
            pp_thumbnail.Text = ui.Text("editcontenttype", "thumbnail");


            // we'll disable this...
            if (!Page.IsPostBack && _contentType.MasterContentType != 0)
            {
                string masterName = ContentType.GetContentType(_contentType.MasterContentType).Text;
                tabsMasterContentTypeName.Text = masterName;
                propertiesMasterContentTypeName.Text = masterName;
                PaneTabsInherited.Visible = true;
                PanePropertiesInherited.Visible = true;
            }

            theClientId.Text = this.ClientID;
        }

        protected void save_click(object sender, ImageClickEventArgs e)
        {
            // 2011 01 06 - APN - Modified method to update Xml caches if a doctype alias changed, 
            // also added calls to update the tree if the name has changed
            // ---

            // Keep a reference of the original doctype alias and name
            var originalDocTypeAlias = _contentType.Alias;
            var originalDocTypeName = _contentType.Text;

            // Check if the doctype alias has changed as a result of either the user input or
            // the alias checking performed upon saving
            var docTypeAliasChanged = (string.Compare(originalDocTypeAlias, txtAlias.Text, true) != 0);
            var docTypeNameChanged = (string.Compare(originalDocTypeName, txtName.Text, true) != 0);

            var ea = new SaveClickEventArgs("Saved");
            ea.IconType = BasePage.speechBubbleIcon.success;
            
            //NOTE The saving of the 5 properties (Name, Alias, Icon, Description and Thumbnail) are divided
            //to avoid the multiple cache flushing when each property is set using the legacy ContentType class,
            //which has been reduced to the else-clause.
            //For IContentType and IMediaType the cache will only be flushed upon saving.
            if (_contentType.ContentTypeItem is IContentType || _contentType.ContentTypeItem is IMediaType)
            {
                _contentType.ContentTypeItem.Name = txtName.Text;
                _contentType.ContentTypeItem.Alias = txtAlias.Text;
                _contentType.ContentTypeItem.Icon = ddlIcons.SelectedValue;
                _contentType.ContentTypeItem.Description = description.Text;
                _contentType.ContentTypeItem.Thumbnail = ddlThumbnails.SelectedValue;
                _contentType.ContentTypeItem.AllowedAsRoot = allowAtRoot.Checked;

                int i = 0;
                var ids = SaveAllowedChildTypes();
                _contentType.ContentTypeItem.AllowedContentTypes = ids.Select(x => new ContentTypeSort{ Id = new Lazy<int>(() => x), SortOrder = i++ });

                var tabs = SaveTabs();
                foreach (var tab in tabs)
                {
                    if (_contentType.ContentTypeItem.PropertyGroups.Contains(tab.Item2))
                    {
                        _contentType.ContentTypeItem.PropertyGroups[tab.Item2].SortOrder = tab.Item3;
                    }
                    else
                    {
                        _contentType.ContentTypeItem.PropertyGroups.Add(new PropertyGroup{ Id = tab.Item1, Name = tab.Item2, SortOrder = tab.Item3 });
                    }
                }

                SavePropertyType(ref ea, _contentType.ContentTypeItem);
                UpdatePropertyTypes(_contentType.ContentTypeItem);

                if (DocumentTypeCallback != null)
                {
                    var documentType = _contentType as DocumentType;
                    if (documentType != null)
                    {
                        var result = DocumentTypeCallback(documentType);
                    }
                }

                _contentType.Save();
            }
            else //Legacy approach for supporting MemberType
            {
                if (docTypeNameChanged)
                    _contentType.Text = txtName.Text;

                if (docTypeAliasChanged)
                    _contentType.Alias = txtAlias.Text;

                _contentType.IconUrl = ddlIcons.SelectedValue;
                _contentType.Description = description.Text;
                _contentType.Thumbnail = ddlThumbnails.SelectedValue;

                SavePropertyTypesLegacy(ref ea);

                var tabs = SaveTabs();
                foreach (var tab in tabs)
                {
                    _contentType.SetTabName(tab.Item1, tab.Item2);
                    _contentType.SetTabSortOrder(tab.Item1, tab.Item3);
                }

                _contentType.AllowedChildContentTypeIDs = SaveAllowedChildTypes();
                _contentType.AllowAtRoot = allowAtRoot.Checked;

                _contentType.Save();
            }

            // reload content type (due to caching)
            LoadContentType();

            // Only if the doctype alias changed, cause a regeneration of the xml cache file since
            // the xml element names will need to be updated to reflect the new alias
            if (docTypeAliasChanged)
                RegenerateXmlCaches();

            BindDataGenericProperties(true);

            // we need to re-bind the alias as the SafeAlias method can have changed it
            txtAlias.Text = _contentType.Alias;

            RaiseBubbleEvent(new object(), ea);

            if (docTypeNameChanged)
                UpdateTreeNode();
        }

        /// <summary>
        /// Loads the current ContentType from the id found in the querystring.
        /// The correct type is loaded based on editing location (DocumentType, MediaType or MemberType).
        /// </summary>
        private void LoadContentType()
        {
            int docTypeId = int.Parse(Request.QueryString["id"]);
            LoadContentType(docTypeId);
        }

        private void LoadContentType(int docTypeId)
        {
            //Fairly hacky code to load the ContentType as the real type instead of its base type, so it can be properly saved.
            if (Request.Path.ToLowerInvariant().Contains("editnodetypenew.aspx"))
            {
                _contentType = new DocumentType(docTypeId);
            }
            else if (Request.Path.ToLowerInvariant().Contains("editmediatype.aspx"))
            {
                _contentType = new cms.businesslogic.media.MediaType(docTypeId);
            }
            else
            {
                _contentType = new ContentType(docTypeId);
            }
        }

        /// <summary>
        /// Regenerates the XML caches. Used after a document type alias has been changed.
        /// </summary>
        private void RegenerateXmlCaches()
        {
            Document.RePublishAll();
            library.RefreshContent();
        }

        /// <summary>
        /// Updates the Node in the Tree
        /// </summary>
        private void UpdateTreeNode()
        {
            var clientTools = new ClientTools(this.Page);
            clientTools
                .SyncTree(_contentType.Path, true);
        }

        #region "Info" Pane
        
        private void SetupInfoPane()
        {
            InfoTabPage = TabView1.NewTabPage("Info");
            InfoTabPage.Controls.Add(pnlInfo);

            InfoTabPage.Style.Add("text-align", "center");

            ImageButton Save = InfoTabPage.Menu.NewImageButton();
            Save.Click += new System.Web.UI.ImageClickEventHandler(save_click);

            Save.ImageUrl = UmbracoPath + "/images/editor/save.gif";
            Save.AlternateText = ui.Text("save");
            Save.ID = "save";
            var listOfIcons = new List<ListItem>();
            // Get icons
            // nh css file update, add support for css sprites
            foreach (string iconClass in cms.businesslogic.CMSNode.DefaultIconClasses)
            {
                ListItem li = new ListItem(helper.SpaceCamelCasing((iconClass.Substring(1, iconClass.Length - 1))).Replace("Spr Tree", "").Trim(), iconClass);
                li.Attributes.Add("class", "spriteBackground sprTree " + iconClass.Trim('.'));
                li.Attributes.Add("style", "padding-left:20px !important; background-repeat:no-repeat;");

                if (!this.Page.IsPostBack && li.Value == _contentType.IconUrl) li.Selected = true;
                listOfIcons.Add(li);
            }

            DirectoryInfo dirInfo = new DirectoryInfo(UmbracoContext.Current.Server.MapPath(SystemDirectories.Umbraco + "/images/umbraco"));
            FileInfo[] fileInfo = dirInfo.GetFiles();
            for (int i = 0; i < fileInfo.Length; i++)
            {
                // NH: don't show the sprite file
                if (fileInfo[i].Name != "sprites.png" && fileInfo[i].Name != "sprites_ie6.gif")
                {
                    ListItem li = new ListItem(fileInfo[i].Name, fileInfo[i].Name);
                    li.Attributes.Add("title", this.ResolveClientUrl(SystemDirectories.Umbraco + "/images/umbraco/" + fileInfo[i].Name));

                    if (li.Value == _contentType.IconUrl)
                        li.Selected = true;
                    listOfIcons.Add(li);
                }
            }

            ddlIcons.Items.AddRange(listOfIcons.OrderBy(o => o.Text).ToArray());

            // Get thumbnails
            dirInfo = new DirectoryInfo(IOHelper.MapPath(SystemDirectories.Umbraco + "/images/thumbnails"));
            fileInfo = dirInfo.GetFiles();
            for (int i = 0; i < fileInfo.Length; i++)
            {
                ListItem li = new ListItem(fileInfo[i].Name);
                li.Attributes.Add("title", this.ResolveClientUrl(SystemDirectories.Umbraco + "/images/thumbnails/" + fileInfo[i].Name));
                if (!this.Page.IsPostBack && li.Value == _contentType.Thumbnail) li.Selected = true;
                ddlThumbnails.Items.Add(li);
            }

            Page.ClientScript.RegisterStartupScript(this.GetType(), "thumbnailsDropDown", string.Format(@"
function refreshDropDowns() {{
    jQuery('#{1}').msDropDown({{ showIcon: true, style: 'width:250px;' }});
    jQuery('#{3}').msDropDown({{ showIcon: false, rowHeight: '130', visibleRows: '2', style: 'width:250px;' }});
}}
jQuery(document).ready(function() {{ refreshDropDowns(); }});
", ddlIcons.ClientID, ddlIcons.ClientID, ddlIcons.ClientID, ddlThumbnails.ClientID, 500), true);
            txtName.Text = _contentType.GetRawText();
            txtAlias.Text = _contentType.Alias;
            description.Text = _contentType.GetRawDescription();

        }
        
        #endregion
        
        #region "Structure" Pane

        private void SetupStructurePane()
        {
            DualAllowedContentTypes.ID = "allowedContentTypes";
            DualAllowedContentTypes.Width = 175;

            uicontrols.TabPage tp = TabView1.NewTabPage("Structure");
            tp.Controls.Add(pnlStructure);
            tp.Style.Add("text-align", "center");
            ImageButton Save = tp.Menu.NewImageButton();
            Save.Click += new System.Web.UI.ImageClickEventHandler(save_click);
            Save.ImageUrl = UmbracoPath + "/images/editor/save.gif";

            int[] allowedIds = _contentType.AllowedChildContentTypeIDs;
            if (!Page.IsPostBack)
            {
                string chosenContentTypeIDs = "";
                ContentType[] contentTypes = _contentType.GetAll();
                foreach (ContentType ct in contentTypes.OrderBy(x => x.Text))
                {
                    ListItem li = new ListItem(ct.Text, ct.Id.ToString());
                    DualAllowedContentTypes.Items.Add(li);
                    lstAllowedContentTypes.Items.Add(li);
                    foreach (int i in allowedIds)
                    {
                        if (i == ct.Id)
                        {
                            li.Selected = true;
                            chosenContentTypeIDs += ct.Id + ",";
                        }
                    }
                }
                DualAllowedContentTypes.Value = chosenContentTypeIDs;
            }
            
            allowAtRoot.Checked = _contentType.AllowAtRoot;
        }

        private int[] SaveAllowedChildTypes()
        {
            var tmp = new ArrayList();
            foreach (ListItem li in lstAllowedContentTypes.Items)
            {
                if (li.Selected)
                    tmp.Add(int.Parse(li.Value));
            }
            var ids = new int[tmp.Count];
            for (int i = 0; i < tmp.Count; i++) ids[i] = (int)tmp[i];

            return ids;
        }

        #endregion

        #region "Generic properties" Pane

        private void SetupGenericPropertiesPane()
        {
            GenericPropertiesTabPage = TabView1.NewTabPage("Generic properties");
            GenericPropertiesTabPage.Style.Add("text-align", "center");
            GenericPropertiesTabPage.Controls.Add(pnlProperties);

            ImageButton Save = GenericPropertiesTabPage.Menu.NewImageButton();
            Save.Click += new System.Web.UI.ImageClickEventHandler(save_click);
            Save.ImageUrl = UmbracoPath + "/images/editor/save.gif";

            BindDataGenericProperties(false);
        }

        private void BindDataGenericProperties(bool refresh)
        {
            var tabs = _contentType.getVirtualTabs;
            var propertyTypeGroups = _contentType.PropertyTypeGroups.ToList();
            var dtds = cms.businesslogic.datatype.DataTypeDefinition.GetAll();

            PropertyTypes.Controls.Clear();

            // Add new property
            if (PropertyTypeNew.Controls.Count == 0)
            {
                PropertyTypeNew.Controls.Add(new LiteralControl("<h2 class=\"propertypaneTitel\">Add New Property</h2><ul class='genericPropertyList addNewProperty'>"));
                gp = new GenericPropertyWrapper();
                gp.ID = "GenericPropertyNew";
                gp.Tabs = tabs;
                gp.DataTypeDefinitions = dtds;
                PropertyTypeNew.Controls.Add(gp);
                PropertyTypeNew.Controls.Add(new LiteralControl("</ul>"));
            }
            else if (refresh)
            {
                gp = (GenericPropertyWrapper)PropertyTypeNew.Controls[1];
                gp.ID = "GenericPropertyNew";
                gp.Tabs = tabs;
                gp.DataTypeDefinitions = dtds;
                gp.UpdateEditControl();
                gp.GenricPropertyControl.UpdateInterface();
                gp.GenricPropertyControl.Clear();
            }

            _genericProperties.Clear();
            var inTab = new Hashtable();
            int counter = 0;

            foreach (ContentType.TabI tab in tabs)
            {
                bool hasProperties = false;
                string tabCaption = tab.ContentType == _contentType.Id ? tab.GetRawCaption() : tab.GetRawCaption() + " (inherited from " + new ContentType(tab.ContentType).Text + ")";
                PropertyTypes.Controls.Add(new LiteralControl("<div class='genericPropertyListBox'><h2 class=\"propertypaneTitel\">Tab: " + tabCaption + "</h2>"));

                var propertyGroup = propertyTypeGroups.SingleOrDefault(x => x.ParentId == tab.Id);
                var propertyTypes = propertyGroup == null
                                        ? tab.GetPropertyTypes(_contentType.Id, false)
                                        : propertyGroup.GetPropertyTypes();

                var propertyGroupId = tab.Id;

                if (propertyTypes.Any(x => x.ContentTypeId == _contentType.Id))
                {
                    var propSort = new HtmlInputHidden();
                    propSort.ID = "propSort_" + propertyGroupId.ToString() + "_Content";
                    PropertyTypes.Controls.Add(propSort);
                    _sortLists.Add(propSort);

                    PropertyTypes.Controls.Add(new LiteralControl("<ul class='genericPropertyList' id=\"t_" + propertyGroupId.ToString() + "_Contents\">"));

                    foreach (cms.businesslogic.propertytype.PropertyType pt in propertyTypes)
                    {
                        //If the PropertyType doesn't belong on this ContentType skip it and continue to the next one
                        if(pt.ContentTypeId != _contentType.Id) continue;

                        var gpw = new GenericPropertyWrapper();
                        gpw.ID = "gpw_" + pt.Id;
                        gpw.PropertyType = pt;
                        gpw.Tabs = tabs;
                        gpw.TabId = propertyGroupId;
                        gpw.DataTypeDefinitions = dtds;
                        gpw.Delete += new EventHandler(gpw_Delete);
                        gpw.FullId = "t_" + propertyGroupId.ToString() + "_Contents_" + +pt.Id;

                        PropertyTypes.Controls.Add(gpw);
                        _genericProperties.Add(gpw);
                        if (refresh)
                            gpw.GenricPropertyControl.UpdateInterface();

                        inTab.Add(pt.Id.ToString(), "");
                        counter++;
                        hasProperties = true;
                    }

                    PropertyTypes.Controls.Add(new LiteralControl("</ul>"));

                    var jsSortable = @"                            
                                (function($) {
                                    var propSortId = ""#" + propSort.ClientID + @""";
                                    $(document).ready(function() {
                                        $(propSortId).next("".genericPropertyList"").sortable({containment: 'parent', tolerance: 'pointer',
                                            update: function(event, ui) { 
                                                $(propSortId).val($(this).sortable('serialize'));
                                            }});
                                    });
                                })(jQuery);";

                    Page.ClientScript.RegisterStartupScript(this.GetType(), propSort.ClientID, jsSortable, true);

                    if (!hasProperties)
                    {
                        AddNoPropertiesDefinedMessage();
                    }

                    PropertyTypes.Controls.Add(new LiteralControl("</div>"));
                }
                else
                {
                    AddNoPropertiesDefinedMessage();
                    PropertyTypes.Controls.Add(new LiteralControl("</div>"));
                }
            }

            // Generic properties tab
            counter = 0;
            bool propertyTabHasProperties = false;
            var propertiesPH = new PlaceHolder();
            propertiesPH.ID = "propertiesPH";
            PropertyTypes.Controls.Add(new LiteralControl("<h2 class=\"propertypaneTitel\">Tab: Generic Properties</h2>"));
            PropertyTypes.Controls.Add(propertiesPH);

            var propSort_gp = new HtmlInputHidden();
            propSort_gp.ID = "propSort_general_Content";
            propertiesPH.Controls.Add(propSort_gp);
            _sortLists.Add(propSort_gp);


            propertiesPH.Controls.Add(new LiteralControl("<ul class='genericPropertyList' id=\"t_general_Contents\">"));
            foreach (cms.businesslogic.propertytype.PropertyType pt in _contentType.PropertyTypes)
            {
                //This use to be:
                if (pt.ContentTypeId == _contentType.Id && inTab.ContainsKey(pt.Id.ToString()) == false)
                //But seriously, if it's not on a tab the tabId is 0, it's a lot easier to read IMO
                //if (pt.ContentTypeId == _contentType.Id && pt.TabId == 0)
                {
                    var gpw = new GenericPropertyWrapper();

                    // Changed by duckie, was:
                    // gpw.ID = "gpw_" + editPropertyType.Alias;
                    // Which is NOT unique!
                    gpw.ID = "gpw_" + pt.Id;

                    gpw.PropertyType = pt;
                    gpw.Tabs = tabs;
                    gpw.DataTypeDefinitions = dtds;
                    gpw.Delete += new EventHandler(gpw_Delete);
                    gpw.FullId = "t_general_Contents_" + pt.Id;

                    propertiesPH.Controls.Add(gpw);
                    _genericProperties.Add(gpw);
                    if (refresh)
                        gpw.GenricPropertyControl.UpdateInterface();
                    inTab.Add(pt.Id, "");
                    propertyTabHasProperties = true;
                    counter++;
                }
            }

            propertiesPH.Controls.Add(new LiteralControl("</ul>"));

            var jsSortable_gp = @"                
                    (function($) {
                        var propSortId = ""#" + propSort_gp.ClientID + @""";
                        $(document).ready(function() {
                            $(propSortId).next("".genericPropertyList"").sortable({containment: 'parent', tolerance: 'pointer',
                                update: function(event, ui) { 
                                    $(propSortId).val($(this).sortable('serialize'));
                                }});
                        });
                    })(jQuery);";

            Page.ClientScript.RegisterStartupScript(this.GetType(), "propSort_gp", jsSortable_gp, true);


            if (!propertyTabHasProperties)
            {
                PropertyTypes.Controls.Add(new LiteralControl("<div style=\"margin: 10px; padding: 4px; border: 1px solid #ccc;\">No properties defined on this tab. Click on the \"add a new property\" link at the top to create a new property.</div>"));
                PropertyTypes.Controls.Remove(PropertyTypes.FindControl("propertiesPH"));
            }
            else
                PropertyTypes.Controls.Add(propertiesPH);

        }

        private void SavePropertyType(ref SaveClickEventArgs e, IContentTypeComposition contentTypeItem)
        {
            this.CreateChildControls();

            //The GenericPropertyWrapper control, which contains the details for the PropertyType being added
            GenericProperty gpData = gp.GenricPropertyControl;
            if (string.IsNullOrEmpty(gpData.Name.Trim()) == false && string.IsNullOrEmpty(gpData.Alias.Trim()) == false)
            {
                var propertyTypeAlias = Casing.SafeAliasWithForcingCheck(gpData.Alias.Trim());
                if (contentTypeItem.PropertyTypeExists(propertyTypeAlias) == false)
                {
                    //Find the DataTypeDefinition that the PropertyType should be based on
                    var dataTypeDefinition = ApplicationContext.Current.Services.DataTypeService.GetDataTypeDefinitionById(gpData.Type);
                    var propertyType = new PropertyType(dataTypeDefinition)
                                           {
                                               Alias = propertyTypeAlias,
                                               Name = gpData.Name.Trim(),
                                               Mandatory = gpData.Mandatory,
                                               ValidationRegExp = gpData.Validation,
                                               Description = gpData.Description
                                           };

                    //gpData.Tab == 0 Generic Properties / No Group
                    if (gpData.Tab == 0)
                    {
                        contentTypeItem.AddPropertyType(propertyType);
                    }
                    else
                    {
                        //Find the PropertyGroup by its Id and then set the PropertyType on that group
                        var exists = contentTypeItem.CompositionPropertyGroups.Any(x => x.Id == gpData.Tab);
                        if (exists)
                        {
                            var propertyGroup = contentTypeItem.CompositionPropertyGroups.First(x => x.Id == gpData.Tab);
                            contentTypeItem.AddPropertyType(propertyType, propertyGroup.Name);
                        }
                        else
                        {
                            var tab = gpData.Tabs.FirstOrDefault(x => x.Id == gpData.Tab);
                            if (tab != null)
                            {
                                var caption = tab.GetRawCaption();
                                contentTypeItem.AddPropertyType(propertyType, caption);
                            }
                        }
                    }

                    gpData.Clear();
                }
                else
                {
                    e.Message = ui.Text("contentTypeDublicatePropertyType");
                    e.IconType = BasePage.speechBubbleIcon.warning;
                }
            }
        }

        private void UpdatePropertyTypes(IContentTypeComposition contentTypeItem)
        {
            //Loop through the _genericProperties ArrayList and update all existing PropertyTypes
            foreach (GenericPropertyWrapper gpw in _genericProperties)
            {
                if(gpw.PropertyType == null) continue;

                if(contentTypeItem.PropertyTypes == null || contentTypeItem.PropertyTypes.Any(x => x.Alias == gpw.PropertyType.Alias) == false) continue;
                var propertyType = contentTypeItem.PropertyTypes.First(x => x.Alias == gpw.PropertyType.Alias);
                if (propertyType == null) continue;

                var dataTypeDefinition = ApplicationContext.Current.Services.DataTypeService.GetDataTypeDefinitionById(gpw.GenricPropertyControl.Type);

                propertyType.Alias = gpw.GenricPropertyControl.Alias;
                propertyType.Name = gpw.GenricPropertyControl.Name;
                propertyType.Description = gpw.GenricPropertyControl.Description;
                propertyType.ValidationRegExp = gpw.GenricPropertyControl.Validation;
                propertyType.Mandatory = gpw.GenricPropertyControl.Mandatory;
                propertyType.DataTypeDatabaseType = dataTypeDefinition.DatabaseType;
                propertyType.DataTypeDefinitionId = dataTypeDefinition.Id;
                propertyType.DataTypeId = dataTypeDefinition.ControlId;

                if (propertyType.PropertyGroupId.Value != gpw.GenricPropertyControl.Tab)
                {
                    if (gpw.GenricPropertyControl.Tab == 0)
                    {
                        propertyType.PropertyGroupId = new Lazy<int>(() => 0);
                    }
                    else if (contentTypeItem.PropertyGroups.Any(x => x.Id == gpw.GenricPropertyControl.Tab))
                    {
                        propertyType.PropertyGroupId = new Lazy<int>(() => gpw.GenricPropertyControl.Tab);
                    }
                    else if (contentTypeItem.PropertyGroups.Any(x => x.ParentId == gpw.GenricPropertyControl.Tab))
                    {
                        var propertyGroup = contentTypeItem.PropertyGroups.First(x => x.ParentId == gpw.GenricPropertyControl.Tab);
                        propertyType.PropertyGroupId = new Lazy<int>(() => propertyGroup.Id);
                    }
                    else
                    {
                        var propertyGroup = contentTypeItem.CompositionPropertyGroups.First(x => x.Id == gpw.GenricPropertyControl.Tab);
                        contentTypeItem.AddPropertyGroup(propertyGroup.Name);
                        contentTypeItem.MovePropertyType(propertyType.Alias, propertyGroup.Name);
                    }
                }

                //Is only called to flush cache since gpw.PropertyType.Save() isn't called
                // clear local cache
                cms.businesslogic.cache.Cache.ClearCacheItem("UmbracoPropertyTypeCache" + gpw.PropertyType.Id);
                // clear cache in ContentType
                cms.businesslogic.cache.Cache.ClearCacheItem("ContentType_PropertyTypes_Content:" + contentTypeItem.Id);
                _contentType.ClearVirtualTabs();
            }

            //Update the SortOrder of the PropertyTypes
            foreach (HtmlInputHidden propSorter in _sortLists)
            {
                if (propSorter.Value.Trim() != "")
                {
                    string tabId = propSorter.ID;
                    // remove leading "propSort_" and trailing "_Content"
                    tabId = tabId.Substring(9, tabId.Length - 9 - 8);
                    // calc the position of the prop SO i.e. after "t_<tabId>Contents[]="
                    int propSOPosition = "t_".Length + tabId.Length + "Contents[]=".Length + 1;

                    string[] tempSO = propSorter.Value.Split("&".ToCharArray());
                    for (int i = 0; i < tempSO.Length; i++)
                    {
                        string propSO = tempSO[i].Substring(propSOPosition);

                        int propertyTypeId = int.Parse(propSO);
                        if (contentTypeItem.PropertyTypes != null &&
                            contentTypeItem.PropertyTypes.Any(x => x.Id == propertyTypeId))
                        {
                            var propertyType = contentTypeItem.PropertyTypes.First(x => x.Id == propertyTypeId);
                            propertyType.SortOrder = i;
                        }
                    }
                }
            }
        }

        private void SavePropertyTypesLegacy(ref SaveClickEventArgs e)
        {
            this.CreateChildControls();

            GenericProperty gpData = gp.GenricPropertyControl;
            if (gpData.Name.Trim() != "" && gpData.Alias.Trim() != "")
            {
                if (DoesPropertyTypeAliasExist(gpData))
                {
                    cms.businesslogic.propertytype.PropertyType pt =
                        _contentType.AddPropertyType(
                            cms.businesslogic.datatype.DataTypeDefinition.GetDataTypeDefinition(gpData.Type),
                            Casing.SafeAliasWithForcingCheck(gpData.Alias.Trim()), gpData.Name);
                    pt.Mandatory = gpData.Mandatory;
                    pt.ValidationRegExp = gpData.Validation;
                    pt.Description = gpData.Description;

                    if (gpData.Tab != 0)
                    {
                        _contentType.SetTabOnPropertyType(pt, gpData.Tab);
                    }

                    gpData.Clear();

                }
                else
                {
                    e.Message = ui.Text("contentTypeDublicatePropertyType");
                    e.IconType = BasePage.speechBubbleIcon.warning;
                }
            }

            foreach (GenericPropertyWrapper gpw in _genericProperties)
            {
                cms.businesslogic.propertytype.PropertyType pt = gpw.PropertyType;
                pt.Alias = gpw.GenricPropertyControl.Alias;
                pt.Name = gpw.GenricPropertyControl.Name;
                pt.Description = gpw.GenricPropertyControl.Description;
                pt.ValidationRegExp = gpw.GenricPropertyControl.Validation;
                pt.Mandatory = gpw.GenricPropertyControl.Mandatory;
                pt.DataTypeDefinition = cms.businesslogic.datatype.DataTypeDefinition.GetDataTypeDefinition(gpw.GenricPropertyControl.Type);
                if (gpw.GenricPropertyControl.Tab == 0)
                    _contentType.removePropertyTypeFromTab(pt);
                else
                    _contentType.SetTabOnPropertyType(pt, gpw.GenricPropertyControl.Tab);

                pt.Save();
            }

            // Sort order
            foreach (HtmlInputHidden propSorter in _sortLists)
            {
                if (propSorter.Value.Trim() != "")
                {
                    string tabId = propSorter.ID;
                    // remove leading "propSort_" and trailing "_Content"
                    tabId = tabId.Substring(9, tabId.Length - 9 - 8);
                    // calc the position of the prop SO i.e. after "t_<tabId>Contents[]="
                    int propSOPosition = "t_".Length + tabId.Length + "Contents[]=".Length + 1;

                    string[] tempSO = propSorter.Value.Split("&".ToCharArray());
                    for (int i = 0; i < tempSO.Length; i++)
                    {
                        string propSO = tempSO[i].Substring(propSOPosition);
                        int currentSortOrder = int.Parse(propSO);
                        cms.businesslogic.propertytype.PropertyType.GetPropertyType(currentSortOrder).SortOrder = i;
                    }
                }
            }
        }

        private void AddNoPropertiesDefinedMessage()
        {
            PropertyTypes.Controls.Add(new LiteralControl("<div style=\"margin: 10px; padding: 4px; border: 1px solid #ccc;\">No properties defined on this tab. Click on the \"add a new property\" link at the top to create a new property.</div>"));
        }
        
        private bool DoesPropertyTypeAliasExist(GenericProperty gpData)
        {
            bool hasAlias = _contentType.getPropertyType(Casing.SafeAliasWithForcingCheck(gpData.Alias.Trim())) != null;
            ContentType ct = _contentType;
            while (ct.MasterContentType > 0)
            {
                ct = new ContentType(ct.MasterContentType);
                hasAlias = ct.getPropertyType(Casing.SafeAliasWithForcingCheck(gpData.Alias.Trim())) != null;
            }
            return !hasAlias;
        }

        /// <summary>
        /// Removes a PropertyType, but when???
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void dgGenericPropertiesOfTab_itemcommand(object sender, DataGridCommandEventArgs e)
        {
            // Delete propertytype from contenttype
            if (e.CommandName == "Delete")
            {
                int propertyId = int.Parse(e.Item.Cells[0].Text);
                string rawName = string.Empty;

                if (_contentType.ContentTypeItem is IContentType || _contentType.ContentTypeItem is IMediaType)
                {
                    var propertyType = _contentType.ContentTypeItem.PropertyTypes.FirstOrDefault(x => x.Id == propertyId);
                    if (propertyType != null && string.IsNullOrEmpty(propertyType.Alias) == false)
                    {
                        rawName = propertyType.Name;
                        _contentType.ContentTypeItem.RemovePropertyType(propertyType.Alias);
                        _contentType.Save();
                    }
                }
                else
                {
                    cms.businesslogic.propertytype.PropertyType pt = cms.businesslogic.propertytype.PropertyType.GetPropertyType(propertyId);
                    rawName = pt.GetRawName();
                    pt.delete();
                }

                RaiseBubbleEvent(new object(), new SaveClickEventArgs("Property �" + rawName + "� deleted"));

                BindDataGenericProperties(false);
            }
        }

        /// <summary>
        /// Removes a PropertyType from the current ContentType when user clicks "red x"
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void gpw_Delete(object sender, EventArgs e)
        {
            var gpw = (GenericPropertyWrapper)sender;

            if (_contentType.ContentTypeItem is IContentType || _contentType.ContentTypeItem is IMediaType)
            {
                _contentType.ContentTypeItem.RemovePropertyType(gpw.PropertyType.Alias);
                _contentType.Save();
            }

            gpw.GenricPropertyControl.PropertyType.delete();

            LoadContentType(_contentType.Id);
            BindDataGenericProperties(true);
        }
       
        #endregion

        #region "Tab" Pane

        private void SetupTabPane()
        {
            uicontrols.TabPage tp = TabView1.NewTabPage("Tabs");
            
            pnlTab.Style.Add("text-align", "center");
            tp.Controls.Add(pnlTab);

            ImageButton Save = tp.Menu.NewImageButton();
            Save.Click += new System.Web.UI.ImageClickEventHandler(save_click);
            Save.ID = "SaveButton";
            Save.ImageUrl = UmbracoPath + "/images/editor/save.gif";

            BindTabs();
        }

        private IEnumerable<Tuple<int, string, int>> SaveTabs()
        {
            var tabs = new List<Tuple<int, string, int>>();//TabId, TabName, TabSortOrder
            foreach (DataGridItem dgi in dgTabs.Items)
            {
                int tabid = int.Parse(dgi.Cells[0].Text);
                string tabName = ((TextBox)dgi.FindControl("txtTab")).Text.Replace("'", "''");
                int tabSortOrder;
                if (Int32.TryParse(((TextBox)dgi.FindControl("txtSortOrder")).Text, out tabSortOrder))
                {
                    tabs.Add(new Tuple<int, string, int>(tabid, tabName, tabSortOrder));
                }
            }
            return tabs;
        }

        private void BindTabs()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("name");
            dt.Columns.Add("id");
            dt.Columns.Add("order");

            foreach (var grp in _contentType.PropertyTypeGroups)
            {
                if (grp.ContentTypeId == _contentType.Id && grp.ParentId == 0)
                {
                    DataRow dr = dt.NewRow();
                    dr["name"] = grp.Name;
                    dr["id"] = grp.Id;
                    dr["order"] = grp.SortOrder;
                    dt.Rows.Add(dr);
                }
            }

            if (dt.Rows.Count == 0)
            {
                lttNoTabs.Text = "No custom tabs defined";
                dgTabs.Visible = false;
            }
            else
            {
                lttNoTabs.Text = "";
                dgTabs.Visible = true;
            }
            dgTabs.DataSource = dt;
            dgTabs.DataBind();
        }

        public DataTable DataTypeTable
        {
            get
            {
                if (_dataTypeTable == null)
                {
                    _dataTypeTable = new DataTable();
                    _dataTypeTable.Columns.Add("name");
                    _dataTypeTable.Columns.Add("id");

                    foreach (var dataType in cms.businesslogic.datatype.DataTypeDefinition.GetAll())
                    {
                        DataRow dr = _dataTypeTable.NewRow();
                        dr["name"] = dataType.Text;
                        dr["id"] = dataType.Id.ToString();
                        _dataTypeTable.Rows.Add(dr);
                    }
                }
                return _dataTypeTable;
            }
        }

        public DataTable TabTable
        {
            get
            {
                if (dgTabs.DataSource == null)
                    BindTabs();

                DataTable dt = new DataTable();
                dt.Columns.Add("name");
                dt.Columns.Add("id");

                foreach (DataRow dr in ((DataTable)dgTabs.DataSource).Rows)
                {
                    DataRow dr2 = dt.NewRow();
                    dr2["name"] = dr["name"];
                    dr2["id"] = dr["id"];
                    dt.Rows.Add(dr2);
                }

                DataRow dr1 = dt.NewRow();
                dr1["name"] = "General properties";
                dr1["id"] = 0;
                dt.Rows.Add(dr1);

                return dt;
            }
        }

        /// <summary>
        /// Adds a new Tab to current ContentType when user clicks 'New Tab'-button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void btnNewTab_Click(object sender, EventArgs e)
        {
            if (txtNewTab.Text.Trim() != "")
            {
                if (_contentType.ContentTypeItem is IContentType || _contentType.ContentTypeItem is IMediaType)
                {
                    _contentType.ContentTypeItem.AddPropertyGroup(txtNewTab.Text);
                    _contentType.Save();
                }
                else
                {
                    _contentType.AddVirtualTab(txtNewTab.Text);
                }

                LoadContentType();

                var ea = new SaveClickEventArgs(ui.Text("contentTypeTabCreated"));
                ea.IconType = BasePage.speechBubbleIcon.success;

                RaiseBubbleEvent(new object(), ea);

                txtNewTab.Text = "";

                BindTabs();
                BindDataGenericProperties(true);
            }

            Page.ClientScript.RegisterStartupScript(this.GetType(), "dropDowns", @"
Umbraco.Controls.TabView.onActiveTabChange(function(tabviewid, tabid, tabs) {
    refreshDropDowns();
});
", true);
        }

        /// <summary>
        /// Removes a Tab from current ContentType when user clicks Delete button
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        protected void dgTabs_ItemCommand(object source, DataGridCommandEventArgs e)
        {
            if (e.CommandName == "Delete")
            {
                int propertyGroupId = int.Parse(e.Item.Cells[0].Text);
                if (_contentType.ContentTypeItem is IContentType || _contentType.ContentTypeItem is IMediaType)
                {
                    var propertyGroup = _contentType.ContentTypeItem.PropertyGroups.FirstOrDefault(x => x.Id == propertyGroupId);
                    if (propertyGroup != null && string.IsNullOrEmpty(propertyGroup.Name) == false)
                    {
                        _contentType.ContentTypeItem.PropertyGroups.Remove(propertyGroup.Name);
                        _contentType.Save();
                    }
                }

                _contentType.DeleteVirtualTab(propertyGroupId);

                LoadContentType();

                var ea = new SaveClickEventArgs(ui.Text("contentTypeTabDeleted"));
                ea.IconType = BasePage.speechBubbleIcon.success;

                RaiseBubbleEvent(new object(), ea);

            }

            BindTabs();
            BindDataGenericProperties(true);
        }

        protected void dgTabs_itemdatabound(object sender, DataGridItemEventArgs e)
        {
            if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
            {
                ((DropDownList)e.Item.FindControl("dllTab")).SelectedValue =
                    ((DataRowView)e.Item.DataItem).Row["propertyTypeGroupId"].ToString();
                ((DropDownList)e.Item.FindControl("ddlType")).SelectedValue =
                    ((DataRowView)e.Item.DataItem).Row["type"].ToString();
            }

        }

        #endregion
    }
}
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using System.Xml;
using Umbraco.Core;
using Umbraco.Web;
using umbraco.BasePages;
using umbraco.BusinessLogic.Actions;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.media;
using umbraco.cms.businesslogic.web;
using System.Web.UI;
using System.Collections.Generic;

namespace umbraco.cms.presentation
{
    /// <summary>
    /// Summary description for sort.
    /// </summary>
    public partial class sort : UmbracoEnsuredPage
    {
        private readonly List<SortableNode> _nodes = new List<SortableNode>();

        protected void Page_Load(object sender, EventArgs e)
        {
            sortDone.Text = ui.Text("sort", "sortDone");
        }

        protected override void OnPreRender(EventArgs e)
        {
            base.OnPreRender(e);

            ScriptManager.GetCurrent(Page).Services.Add(new ServiceReference("../webservices/nodesorter.asmx"));
            ScriptManager.GetCurrent(Page).Services.Add(new ServiceReference("../webservices/legacyAjaxCalls.asmx"));

            var app = Request.GetItemAsString("app");
            var icon = "../images/umbraco/doc.gif";

            int parentId;
            if (int.TryParse(Request.GetItemAsString("ID"), out parentId))
            {
                if (app == "media")
                {
                    icon = "../images/umbraco/mediaPhoto.gif";
                    var mediaService = ApplicationContext.Current.Services.MediaService;

                    if (parentId == -1)
                    {
                        foreach (var child in mediaService.GetRootMedia().ToList().OrderBy(x => x.SortOrder))
                            _nodes.Add(CreateNode(child.Id, child.SortOrder, child.Name, child.CreateDate, icon));
                    }
                    else
                    {
                        var children = mediaService.GetChildren(parentId);
                        foreach (var child in children.OrderBy(x => x.SortOrder))
                            _nodes.Add(CreateNode(child.Id, child.SortOrder, child.Name, child.CreateDate, icon));
                    }
                }

                if (app == "content")
                {
                    var contentService = ApplicationContext.Current.Services.ContentService;

                    if (parentId == -1)
                    {
                        foreach (var child in contentService.GetRootContent().ToList().OrderBy(x => x.SortOrder))
                            _nodes.Add(CreateNode(child.Id, child.SortOrder, child.Name, child.CreateDate, icon));
                    }
                    else
                    {
                        var children = contentService.GetChildren(parentId);
                        foreach (var child in children.OrderBy(x => x.SortOrder))
                            _nodes.Add(CreateNode(child.Id, child.SortOrder, child.Name, child.CreateDate, icon));
                    }
                }

                // "hack for stylesheet"
                // TODO: I can't see where this is being used at all..?
                if (app == "settings")
                {
                    icon = "../images/umbraco/settingCss.gif";
                    var ss = new StyleSheet(parentId);
                    foreach (var child in ss.Properties)
                        _nodes.Add(CreateNode(child.Id, child.sortOrder, child.Text, child.CreateDateTime, icon));
                }

                bindNodesToList(string.Empty);
            }
        }

        public void bindNodesToList(string sortBy)
        {
            if (string.IsNullOrEmpty(sortBy) == false)
            {
                switch (sortBy)
                {
                    case "nodeName":
                        _nodes.Sort(new nodeNameCompare());
                        break;
                    case "createDate":
                        _nodes.Sort(new createDateCompare());
                        break;
                }
            }

            foreach (var n in _nodes)
                lt_nodes.Text += string.Format("<tr id='node_{0}'><td>{1}</td><td class='nowrap'>{2} {3}</td><td style='text-align: center;'>{4}</td></tr>", n.id, n.Name, n.createDate.ToShortDateString(), n.createDate.ToShortTimeString(), n.sortOder);
        }

        private static SortableNode CreateNode(int id, int sortOrder, string name, DateTime createDateTime, string icon)
        {
            var node = new SortableNode
                            {
                                id = id,
                                sortOder = sortOrder,
                                Name = name,
                                icon = icon,
                                createDate = createDateTime
                            };
            return node;
        }

        public struct SortableNode
        {
            public int id;
            public int sortOder;
            public string Name;
            public string icon;
            public DateTime createDate;
        }


        #region Web Form Designer generated code

        protected override void OnInit(EventArgs e)
        {
            //
            // CODEGEN: This call is required by the ASP.NET Web Form Designer.
            //
            InitializeComponent();
            base.OnInit(e);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
        }

        #endregion
    }

    public class nodeNameCompare : IComparer<sort.SortableNode>
    {

        #region IComparer<CMSNode> Members

        public int Compare(sort.SortableNode x, sort.SortableNode y)
        {
            var returnValue = String.Compare(x.Name, y.Name, StringComparison.Ordinal);

            return returnValue;
        }

        #endregion
    }

    public class createDateCompare : IComparer<sort.SortableNode>
    {

        #region IComparer<CMSNode> Members

        public int Compare(sort.SortableNode x, sort.SortableNode y)
        {
            var returnValue = x.createDate.CompareTo(y.createDate);

            return returnValue;
        }

        #endregion
    }
}
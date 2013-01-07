using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using Umbraco.Core.IO;
using umbraco.interfaces;
using umbraco.IO;
using Content = umbraco.cms.businesslogic.Content;

namespace umbraco.editorControls
{
    [ValidationProperty("IsValid")]
    public class uploadField : HtmlInputFile, IDataEditor
    {
        private const String Thumbnailext = ".jpg";
		private readonly cms.businesslogic.datatype.FileHandlerData _data;
        private readonly String _thumbnails;
        private String _text;
        private readonly MediaFileSystem _fs; 

        public uploadField(IData Data, string ThumbnailSizes)
        {
            _fs = FileSystemProviderManager.Current.GetFileSystemProvider<MediaFileSystem>();
            _data = (cms.businesslogic.datatype.FileHandlerData) Data; //this is always FileHandlerData
            _thumbnails = ThumbnailSizes;
        }

        /// <summary>
        /// Internal logic for validation controls to detect whether or not it's valid (has to be public though) 
        /// </summary>
        /// <value>Am I valid?</value>
        public string IsValid
        {
            get
            {
                string tempText = Text;
                bool isEmpty = PostedFile == null || String.IsNullOrEmpty(PostedFile.FileName);
                // checkbox, if it's used the file will be deleted and we should throw a validation error
                if (Page.Request[ClientID + "clear"] != null && Page.Request[ClientID + "clear"] != "")
                    return "";
                else if (!isEmpty)
                    return PostedFile.FileName;
                else if (!String.IsNullOrEmpty(tempText))
                    return tempText;
                else
                    return "";
            }
        }

        public String Text
        {
            get { return _text; }
            set { _text = value; }
        }

        #region IDataEditor Members

        public Control Editor
        {
            get { return this; }
        }

        public virtual bool TreatAsRichTextEditor
        {
            get { return false; }
        }

        public bool ShowLabel
        {
            get { return true; }
        }

        public void Save()
        {
            // Clear data
            if (helper.Request(ClientID + "clear") == "1")
            {
                // delete file
                DeleteFile(_text);

                // set filename in db to nothing
                _text = "";
                _data.Value = _text;


                foreach (string prop in "umbracoExtension,umbracoBytes,umbracoWidth,umbracoHeight".Split(','))
                {
                    try
                    {
                        var bytesControl = FindControlRecursive<noEdit>(Page, "prop_" + prop);
                        if (bytesControl != null)
                        {
                            bytesControl.RefreshLabel(string.Empty);
                        }
                    }
                    catch
                    {
                        //if first one fails we can assume that props don't exist
                        break;
                    }
                }
            }

            if (PostedFile != null && PostedFile.FileName != String.Empty)
            {
                _data.Value = PostedFile;

                // we update additional properties post image upload
                if (_data.Value != DBNull.Value && !string.IsNullOrEmpty(_data.Value.ToString()))
                {
	                var content = _data.LoadedContentItem;
                    
					// update extension in UI				
	                UpdateLabelValue("umbracoExtension", "prop_umbracoExtension", Page, content);					
                    // update file size in UI
					UpdateLabelValue("umbracoBytes", "prop_umbracoBytes", Page, content);
					UpdateLabelValue("umbracoWidth", "prop_umbracoWidth", Page, content);
					UpdateLabelValue("umbracoHeight", "prop_umbracoHeight", Page, content);                    
                }
                Text = _data.Value.ToString();
            }
        }

        #endregion

		private static void UpdateLabelValue(string propAlias, string controlId, Page controlPage, Content content)
		{
			var extensionControl = FindControlRecursive<noEdit>(controlPage, controlId);
			if (extensionControl != null)
			{
				if (content.getProperty(propAlias) != null && content.getProperty(propAlias).Value != null)
				{
					extensionControl.RefreshLabel(content.getProperty(propAlias).Value.ToString());
				}
			}
		}

        [Obsolete("This method is now obsolete due to a change in the way that files are handled.  If you need to check if a URL for an uploaded file is safe you should implement your own as this method will be removed in a future version", false)]
        public string SafeUrl(string url)
        {
            if (!String.IsNullOrEmpty(url))
                return Regex.Replace(url, @"[^a-zA-Z0-9\-\.\/\:]{1}", "_");
            else
                return String.Empty;
        }

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            if (_data != null && _data.Value != null)
                Text = _data.Value.ToString();
        }

        private void DeleteFile(string fileUrl)
        {
            if (fileUrl.Length > 0)
            {
                var relativeFilePath = _fs.GetRelativePath(fileUrl);

                // delete old file
                if (_fs.FileExists(relativeFilePath))
                    _fs.DeleteFile(relativeFilePath);

                string extension = (relativeFilePath.Substring(relativeFilePath.LastIndexOf(".") + 1, relativeFilePath.Length - relativeFilePath.LastIndexOf(".") - 1));
                extension = extension.ToLower();

                //check for thumbnails
                if (",jpeg,jpg,gif,bmp,png,tiff,tif,".IndexOf("," + extension + ",") > -1)
                {
                    //delete thumbnails
                    string relativeThumbFilePath = relativeFilePath.Replace("." + extension, "_thumb");

                    try
                    {
                        if (_fs.FileExists(relativeThumbFilePath + Thumbnailext))
                            _fs.DeleteFile(relativeThumbFilePath + Thumbnailext);
                    }
                    catch
                    {
                    }

                    if (_thumbnails != "")
                    {
                        string[] thumbnailSizes = _thumbnails.Split(";".ToCharArray());
                        foreach (string thumb in thumbnailSizes)
                        {
                            if (thumb != "")
                            {
                                string relativeExtraThumbFilePath = relativeThumbFilePath + "_" + thumb + Thumbnailext;

                                try
                                {
                                    if (_fs.FileExists(relativeExtraThumbFilePath))
                                        _fs.DeleteFile(relativeExtraThumbFilePath);
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively finds a control with the specified identifier.
        /// </summary>
        /// <typeparam name="T">
        /// The type of control to be found.
        /// </typeparam>
        /// <param name="parent">
        /// The parent control from which the search will start.
        /// </param>
        /// <param name="id">
        /// The identifier of the control to be found.
        /// </param>
        /// <returns>
        /// The control with the specified identifier, otherwise <see langword="null"/> if the control 
        /// is not found.
        /// </returns>
        private static T FindControlRecursive<T>(Control parent, string id) where T : Control
        {
            if ((parent is T) && (parent.ID == id))
            {
                return (T) parent;
            }

            foreach (Control control in parent.Controls)
            {
                var foundControl = FindControlRecursive<T>(control, id);
                if (foundControl != null)
                {
                    return foundControl;
                }
            }
            return default(T);
        }

        /// <summary> 
        /// Render this control to the output parameter specified.
        /// </summary>
        /// <param name="output"> The HTML writer to write out to </param>
        protected override void Render(HtmlTextWriter output)
        {
            if (!string.IsNullOrEmpty(Text))
            {
                var relativeFilePath = _fs.GetRelativePath(_text);
                var ext = relativeFilePath.Substring(relativeFilePath.LastIndexOf(".") + 1, relativeFilePath.Length - relativeFilePath.LastIndexOf(".") - 1);
                var relativeThumbFilePath = relativeFilePath.Replace("." + ext, "_thumb.jpg");
                var hasThumb = false;
                try
                {
                    hasThumb = _fs.FileExists(relativeThumbFilePath);
                    // 4.8.0 added support for png thumbnails (but for legacy it might have been jpg - hence the check before)
                    if (!hasThumb && (ext == "gif" || ext == "png"))
                    {
                        relativeThumbFilePath = relativeFilePath.Replace("." + ext, "_thumb.png");
                        hasThumb = _fs.FileExists(relativeThumbFilePath);
                    }
                }
                catch
                {
                }
                if (hasThumb)
                {
                    var thumb = new Image
                    {
                        ImageUrl = _fs.GetUrl(relativeThumbFilePath), 
                        BorderStyle = BorderStyle.None
                    };

                    output.WriteLine("<a href=\"" + _fs.GetUrl(relativeFilePath) + "\" target=\"_blank\">");
                    thumb.RenderControl(output);
                    output.WriteLine("</a><br/>");
                }
                else
                    output.WriteLine("<a href=\"" + _fs.GetUrl(relativeFilePath) + "\" target=\"_blank\">" +
                                     _fs.GetUrl(relativeFilePath) + "</a><br/>");

                output.WriteLine("<input type=\"checkbox\" id=\"" + ClientID + "clear\" name=\"" + ClientID +
                                 "clear\" value=\"1\"/> <label for=\"" + ClientID + "clear\">" + ui.Text("uploadClear") +
                                 "</label><br/>");
            }
            base.Render(output);
        }
    }
}
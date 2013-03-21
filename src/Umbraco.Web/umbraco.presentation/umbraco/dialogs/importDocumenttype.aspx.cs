using System;
using System.Linq;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Xml;
using System.Xml.Linq;
using Umbraco.Core;
using umbraco.IO;

namespace umbraco.presentation.umbraco.dialogs
{
	/// <summary>
	/// Summary description for importDocumentType.
	/// </summary>
	public class importDocumentType : BasePages.UmbracoEnsuredPage
	{
	    public importDocumentType()
	    {

            CurrentApp = BusinessLogic.DefaultApps.settings.ToString();

	    }
		protected Literal FeedBackMessage;
		protected Literal jsShowWindow;
		protected Panel Wizard;
		protected HtmlTable Table1;
		protected HtmlInputHidden tempFile;
		protected HtmlInputFile documentTypeFile;
		protected Button submit;
		protected Panel Confirm;
		protected Literal dtName;
		protected Literal dtAlias;
		protected Button import;
		protected Literal dtNameConfirm;
		protected Panel done;
		private string tempFileName = "";

		private void Page_Load(object sender, EventArgs e)
		{
			if (!IsPostBack) 
			{
				submit.Text = ui.Text("import");
				import.Text = ui.Text("import");
			} 
		}

		#region Web Form Designer generated code
		override protected void OnInit(EventArgs e)
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
			this.submit.Click += new System.EventHandler(this.submit_Click);
			this.import.Click += new System.EventHandler(this.import_Click);
			this.Load += new System.EventHandler(this.Page_Load);

		}
		#endregion

		private void import_Click(object sender, EventArgs e)
		{
			/*XmlDocument xd = new XmlDocument();
			xd.Load(tempFile.Value);
			cms.businesslogic.packager.Installer.ImportDocumentType(xd.DocumentElement, base.getUser(), true);
			dtNameConfirm.Text = xd.DocumentElement.SelectSingleNode("/DocumentType/Info/Name").FirstChild.Value;*/

		    var element = XElement.Parse(tempFile.Value);
		    var importContentTypes = ApplicationContext.Current.Services.PackagingService.ImportContentTypes(element);
		    var contentType = importContentTypes.FirstOrDefault();
		    if (contentType != null)
		        dtNameConfirm.Text = contentType.Name;

		    Wizard.Visible = false;
			Confirm.Visible = false;
			done.Visible = true;
		}

		private void submit_Click(object sender, System.EventArgs e)
		{
			tempFileName = "justDelete_" + Guid.NewGuid().ToString() + ".udt";
			string fileName = IOHelper.MapPath(SystemDirectories.Data + "/" + tempFileName);
			tempFile.Value = fileName;

			documentTypeFile.PostedFile.SaveAs(fileName);

			XmlDocument xd = new XmlDocument();
			xd.Load(fileName);
			dtName.Text = xd.DocumentElement.SelectSingleNode("/DocumentType/Info/Name").FirstChild.Value;
			dtAlias.Text = xd.DocumentElement.SelectSingleNode("/DocumentType/Info/Alias").FirstChild.Value;


			Wizard.Visible = false;
			done.Visible = false;
			Confirm.Visible = true;

		}

	}
}

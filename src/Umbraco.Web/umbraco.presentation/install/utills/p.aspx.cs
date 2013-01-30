﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Umbraco.Web.Install.UpgradeScripts;
using umbraco.DataLayer.Utility.Installer;
using umbraco.DataLayer;

namespace umbraco.presentation.install.utills
{
    public partial class p : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

            // Stop Caching in IE
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);

            // Stop Caching in Firefox
            Response.Cache.SetNoStore();

            string feed = Request.QueryString["feed"];
            string url = "http://our.umbraco.org/html/twitter";

            if (feed == "progress")
            {
                Response.ContentType = "application/json";
                Response.Write(Helper.getProgress());
            }
            else
            {
                if (feed == "blogs")
                    url = "http://our.umbraco.org/html/blogs";

                if (feed == "sitebuildervids")
                    url = "http://umbraco.org/feeds/videos/site-builder-foundation-html";

                if (feed == "developervids")
                    url = "http://umbraco.org/feeds/videos/developer-foundation-html";

                string XmlResponse = library.GetXmlDocumentByUrl(url).Current.OuterXml;

                if (!XmlResponse.Contains("System.Net.WebException"))
                {
                    Response.Write(library.GetXmlDocumentByUrl(url).Current.OuterXml);
                }
                else
                {
                    Response.Write("We can't connect to umbraco.tv right now.  Click <strong>Set up your new website</strong> above to continue.");
                }
            }
        }


        [System.Web.Services.WebMethod]
        [System.Web.Script.Services.ScriptMethod]
        public static string installOrUpgrade()
        {
            Helper.setProgress(5, "Opening database connection...", "");

            IInstallerUtility installer;

            // Build the new connection string
            //DbConnectionStringBuilder connectionStringBuilder = CreateConnectionString();
            Helper.setProgress(5, "Connecting...", "");

            // Try to connect to the database
            try
            {
                var sqlHelper = DataLayerHelper.CreateSqlHelper(GlobalSettings.DbDSN);
                installer = sqlHelper.Utility.CreateInstaller();

                if (!installer.CanConnect)
                    throw new Exception("The installer cannot connect to the database.");
                else
                    Helper.setProgress(20, "Connection opened", "");
            }
            catch (Exception ex)
            {
                var error = new Exception("Database connection initialisation failed.", ex);
                Helper.setProgress(-5, "Database connection initialisation failed.", 
                    string.Format("{0}<br />Connection string: {1}", error.InnerException.Message, GlobalSettings.DbDSN));

                return error.Message;
            }


            if (installer.CanConnect)
            {
                if (installer.IsLatestVersion)
                {

                    Helper.setProgress(90, "Refreshing content cache", "");

                    library.RefreshContent();

                    Helper.setProgress(100, "Database is up-to-date", "");

                    return "upgraded";

                }
                else
                {
                    if (installer.IsEmpty)
                    {
                        Helper.setProgress(35, "Installing tables...", "");
                        //do install
                        try
                        {
                            installer.Install();
                            Helper.setProgress(100, "Installation completed!", "");
                            installer = null;

                            library.RefreshContent();
                            return "installed";
                        }
                        catch (Exception SqlExp)
                        {
                            Helper.setProgress(35, "Error installing tables", SqlExp.InnerException.ToString());
                            return "error";
                        }
 
                    } //else if (m_Installer.CurrentVersion == DatabaseVersion.None || m_Installer.CanUpgrade) {
                    //Helper.setProgress(35, "Updating database tables...", "");
                    //m_Installer.Install();

                      //      library.RefreshContent();
                    //      return "installed";
                    //  }
                    else if (installer.CurrentVersion == DatabaseVersion.None || installer.CanUpgrade)
                    {
                        Helper.setProgress(35, "Updating database tables...", "");
                        installer.Install();

                        Helper.setProgress(100, "Upgrade completed!", "");

                        installer = null;

                        library.RefreshContent();

                        return "upgraded";
                    }
                }



            }

            return "no connection;";
        }

        

    }
}
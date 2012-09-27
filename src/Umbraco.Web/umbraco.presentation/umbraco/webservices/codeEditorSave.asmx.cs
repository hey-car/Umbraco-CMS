using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Script.Services;
using System.Web.Services;
using System.Web.UI;
using System.Xml;
using System.Xml.Xsl;
using umbraco.BasePages;
using umbraco.cms.businesslogic.macro;
using umbraco.cms.businesslogic.template;
using umbraco.cms.businesslogic.web;
using umbraco.presentation.cache;
using System.Net;
using umbraco.IO;
using System.Collections;
using umbraco.NodeFactory;
using umbraco.scripting;

namespace umbraco.presentation.webservices
{
    /// <summary>
    /// Summary description for codeEditorSave
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]
    [ScriptService]
    public class codeEditorSave : WebService
    {
        [WebMethod]
        public string Save(string fileName, string fileAlias, string fileContents, string fileType, int fileID,
                           int masterID, bool ignoreDebug)
        {
            return "Not implemented";
        }

        [WebMethod]
        public string SaveCss(string fileName, string oldName, string fileContents, int fileID)
        {
            if (BasePage.ValidateUserContextID(BasePage.umbracoUserContextID))
            {
                string returnValue = "false";
                StyleSheet stylesheet = new StyleSheet(fileID);

                if (stylesheet != null)
                {
                    stylesheet.Content = fileContents;
                    stylesheet.Text = fileName;
                    try
                    {
                        stylesheet.saveCssToFile();
                        stylesheet.Save();
                        returnValue = "true";


                        //deletes the old css file if the name was changed... 
                        if (fileName != oldName)
                        {
                            string p = IOHelper.MapPath(SystemDirectories.Css + "/" + oldName + ".css");
                            if (System.IO.File.Exists(p))
                                System.IO.File.Delete(p);
                        }

                    }
                    catch (Exception ex)
                    {
                        return ex.ToString();
                    }

                    //this.speechBubble(speechBubbleIcon.save, ui.Text("speechBubbles", "editStylesheetSaved", base.getUser()), "");
                }
                return returnValue;
            }
            return "false";
        }

        [WebMethod]
        public string SaveXslt(string fileName, string oldName, string fileContents, bool ignoreDebugging)
        {
            if (BasePage.ValidateUserContextID(BasePage.umbracoUserContextID))
            {

                // validate file
                IOHelper.ValidateEditPath(IOHelper.MapPath(SystemDirectories.Xslt + "/" + fileName),
                                          SystemDirectories.Xslt);
                // validate extension
                IOHelper.ValidateFileExtension(IOHelper.MapPath(SystemDirectories.Xslt + "/" + fileName),
                                               new List<string>() { "xsl", "xslt" });


                StreamWriter SW;
                string tempFileName = IOHelper.MapPath(SystemDirectories.Xslt + "/" + DateTime.Now.Ticks + "_temp.xslt");
                SW = File.CreateText(tempFileName);
                SW.Write(fileContents);
                SW.Close();

                // Test the xslt
                string errorMessage = "";

                if (!ignoreDebugging)
                {
                    try
                    {
                        // Check if there's any documents yet
                        string xpath = UmbracoSettings.UseLegacyXmlSchema ? "/root/node" : "/root/*";
                        if (content.Instance.XmlContent.SelectNodes(xpath).Count > 0)
                        {
                            XmlDocument macroXML = new XmlDocument();
                            macroXML.LoadXml("<macro/>");

                            XslCompiledTransform macroXSLT = new XslCompiledTransform();
                            page umbPage = new page(content.Instance.XmlContent.SelectSingleNode("//* [@parentID = -1]"));

                            XsltArgumentList xslArgs;
                            xslArgs = macro.AddMacroXsltExtensions();
                            library lib = new library(umbPage);
                            xslArgs.AddExtensionObject("urn:umbraco.library", lib);
                            HttpContext.Current.Trace.Write("umbracoMacro", "After adding extensions");

                            // Add the current node
                            xslArgs.AddParam("currentPage", "", library.GetXmlNodeById(umbPage.PageID.ToString()));

                            HttpContext.Current.Trace.Write("umbracoMacro", "Before performing transformation");

                            // Create reader and load XSL file
                            // We need to allow custom DTD's, useful for defining an ENTITY
                            XmlReaderSettings readerSettings = new XmlReaderSettings();
                            readerSettings.ProhibitDtd = false;
                            using (XmlReader xmlReader = XmlReader.Create(tempFileName, readerSettings))
                            {
                                XmlUrlResolver xslResolver = new XmlUrlResolver();
                                xslResolver.Credentials = CredentialCache.DefaultCredentials;
                                macroXSLT.Load(xmlReader, XsltSettings.TrustedXslt, xslResolver);
                                xmlReader.Close();
                                // Try to execute the transformation
                                HtmlTextWriter macroResult = new HtmlTextWriter(new StringWriter());
                                macroXSLT.Transform(macroXML, xslArgs, macroResult);
                                macroResult.Close();

                                File.Delete(tempFileName);
                            }
                        }
                        else
                        {
                            //errorMessage = ui.Text("developer", "xsltErrorNoNodesPublished");
                            File.Delete(tempFileName);
                            //base.speechBubble(speechBubbleIcon.info, ui.Text("errors", "xsltErrorHeader", base.getUser()), "Unable to validate xslt as no published content nodes exist.");
                        }
                    }
                    catch (Exception errorXslt)
                    {
                        File.Delete(tempFileName);

                        errorMessage = (errorXslt.InnerException ?? errorXslt).ToString();

                        // Full error message
                        errorMessage = errorMessage.Replace("\n", "<br/>\n");
                        //closeErrorMessage.Visible = true;

                        string[] errorLine;
                        // Find error
                        MatchCollection m = Regex.Matches(errorMessage, @"\d*[^,],\d[^\)]", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
                        foreach (Match mm in m)
                        {
                            errorLine = mm.Value.Split(',');

                            if (errorLine.Length > 0)
                            {
                                int theErrorLine = int.Parse(errorLine[0]);
                                int theErrorChar = int.Parse(errorLine[1]);

                                errorMessage = "Error in XSLT at line " + errorLine[0] + ", char " + errorLine[1] +
                                               "<br/>";
                                errorMessage += "<span style=\"font-family: courier; font-size: 11px;\">";

                                string[] xsltText = fileContents.Split("\n".ToCharArray());
                                for (int i = 0; i < xsltText.Length; i++)
                                {
                                    if (i >= theErrorLine - 3 && i <= theErrorLine + 1)
                                        if (i + 1 == theErrorLine)
                                        {
                                            errorMessage += "<b>" + (i + 1) + ": &gt;&gt;&gt;&nbsp;&nbsp;" +
                                                            Server.HtmlEncode(xsltText[i].Substring(0, theErrorChar));
                                            errorMessage +=
                                                "<span style=\"text-decoration: underline; border-bottom: 1px solid red\">" +
                                                Server.HtmlEncode(
                                                    xsltText[i].Substring(theErrorChar,
                                                                          xsltText[i].Length - theErrorChar)).
                                                    Trim() + "</span>";
                                            errorMessage += " &lt;&lt;&lt;</b><br/>";
                                        }
                                        else
                                            errorMessage += (i + 1) + ": &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;" +
                                                            Server.HtmlEncode(xsltText[i]) + "<br/>";
                                }
                                errorMessage += "</span>";
                            }
                        }


                    }
                }

                if (errorMessage == "" && fileName.ToLower().EndsWith(".xslt"))
                {
                    //Hardcoded security-check... only allow saving files in xslt directory... 
                    string savePath = IOHelper.MapPath(SystemDirectories.Xslt + "/" + fileName);

                    if (savePath.StartsWith(IOHelper.MapPath(SystemDirectories.Xslt + "/")))
                    {
                        //deletes the old xslt file
                        if (fileName != oldName)
                        {

                            string p = IOHelper.MapPath(SystemDirectories.Xslt + "/" + oldName);
                            if (System.IO.File.Exists(p))
                                System.IO.File.Delete(p);
                        }

                        SW = File.CreateText(savePath);
                        SW.Write(fileContents);
                        SW.Close();
                        errorMessage = "true";

                       
                    }
                    else
                    {
                        errorMessage = "Illegal path";
                    }
                }

                File.Delete(tempFileName);

                return errorMessage;
            }
            return "false";
        }

        [WebMethod]
        public string SaveDLRScript(string fileName, string oldName, string fileContents, bool ignoreDebugging)
        {

            if (BasePage.ValidateUserContextID(BasePage.umbracoUserContextID))
            {
                if (string.IsNullOrEmpty(fileName))
                    throw new ArgumentNullException("fileName");

                List<string> allowedExtensions = new List<string>();
                foreach (MacroEngineLanguage lang in MacroEngineFactory.GetSupportedUILanguages())
                {
                    if (!allowedExtensions.Contains(lang.Extension))
                        allowedExtensions.Add(lang.Extension);
                }


                // validate file
                IOHelper.ValidateEditPath(IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + fileName),
                                          SystemDirectories.MacroScripts);
                // validate extension
                IOHelper.ValidateFileExtension(IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + fileName),
                                               allowedExtensions);


                StreamWriter SW;

                //As Files Can Be Stored In Sub Directories, So We Need To Get The Exeuction Directory Correct
                var lastOccurance = fileName.LastIndexOf('/') + 1;
                var directory = fileName.Substring(0, lastOccurance);
                var fileNameWithExt = fileName.Substring(lastOccurance);
                var tempFileName =
                    IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + directory + DateTime.Now.Ticks + "_" +
                                     fileNameWithExt);

                //SW = File.CreateText(tempFileName);
                SW = new StreamWriter(tempFileName, false, Encoding.UTF8);
                SW.Write(fileContents);
                SW.Close();

                var errorMessage = "";
                if (!ignoreDebugging)
                {
                    var root = Document.GetRootDocuments().FirstOrDefault();
                    if (root != null)
                    {
                        var args = new Hashtable();
                        var n = new Node(root.Id);
                        args.Add("currentPage", n);

                        try
                        {
                            var engine = MacroEngineFactory.GetByFilename(tempFileName);
                            var tempErrorMessage = "";
                            var xpath = UmbracoSettings.UseLegacyXmlSchema ? "/root/node" : "/root/*";
                            if (
                                !engine.Validate(fileContents, tempFileName, Node.GetNodeByXpath(xpath),
                                                 out tempErrorMessage))
                                errorMessage = tempErrorMessage;
                        }
                        catch (Exception err)
                        {
                            errorMessage = err.ToString();
                        }
                    }
                }

                if (errorMessage == "")
                {
                    var savePath = IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + fileName);

                    //deletes the file
                    if (fileName != oldName)
                    {
                        var p = IOHelper.MapPath(SystemDirectories.MacroScripts + "/" + oldName);
                        if (File.Exists(p))
                            File.Delete(p);
                    }

                    SW = new StreamWriter(savePath, false, Encoding.UTF8);
                    SW.Write(fileContents);
                    SW.Close();
                    errorMessage = "true";

                    
                }

                File.Delete(tempFileName);


                return errorMessage.Replace("\n", "<br/>\n");
            }

            return "false";
        }

        [WebMethod]
        public string SaveScript(string filename, string oldName, string contents)
        {
            if (BasePage.ValidateUserContextID(BasePage.umbracoUserContextID))
            {

                // validate file
                IOHelper.ValidateEditPath(IOHelper.MapPath(SystemDirectories.Scripts + "/" + filename),
                                          SystemDirectories.Scripts);
                // validate extension
                IOHelper.ValidateFileExtension(IOHelper.MapPath(SystemDirectories.Scripts + "/" + filename),
                                               UmbracoSettings.ScriptFileTypes.Split(',').ToList());


                string val = contents;
                string returnValue = "false";
                try
                {
                    string saveOldPath = "";
                    if (oldName.StartsWith("~/"))
                        saveOldPath = IOHelper.MapPath(oldName);
                    else
                        saveOldPath = IOHelper.MapPath(SystemDirectories.Scripts + "/" + oldName);

                    string savePath = "";
                    if (filename.StartsWith("~/"))
                        savePath = IOHelper.MapPath(filename);
                    else
                        savePath = IOHelper.MapPath(SystemDirectories.Scripts + "/" + filename);


                    //Directory check.. only allow files in script dir and below to be edited
                    if (savePath.StartsWith(IOHelper.MapPath(SystemDirectories.Scripts + "/")) || savePath.StartsWith(IOHelper.MapPath(SystemDirectories.Masterpages + "/")))
                    {
                        //deletes the old file
                        if (savePath != saveOldPath)
                        {
                            if (System.IO.File.Exists(saveOldPath))
                                System.IO.File.Delete(saveOldPath);
                        }

                        StreamWriter SW;
                        SW = File.CreateText(savePath);
                        SW.Write(val);
                        SW.Close();

                        returnValue = "true";
                    }
                    else
                    {
                        returnValue = "illegalPath";
                    }
                }
                catch
                {
                    returnValue = "false";
                }


                return returnValue;
            }
            return "false";
        }
        
        [WebMethod]
        public string SaveTemplate(string templateName, string templateAlias, string templateContents, int templateID, int masterTemplateID)
        {
            if (BasePage.ValidateUserContextID(BasePage.umbracoUserContextID))
            {
                var _template = new Template(templateID);
                string retVal = "false";

                if (_template != null)
                {
                    _template.Text = templateName;
                    _template.Alias = templateAlias;
                    _template.MasterTemplate = masterTemplateID;
                    _template.Design = templateContents;

                    _template.Save();

                    retVal = "true";

                    // Clear cache in rutime
                    if (UmbracoSettings.UseDistributedCalls)
                        dispatcher.Refresh(new Guid("dd12b6a0-14b9-46e8-8800-c154f74047c8"), _template.Id);
                    else
                        template.ClearCachedTemplate(_template.Id);
                }
                return retVal;
            }
            return "false";
        }

    }
}
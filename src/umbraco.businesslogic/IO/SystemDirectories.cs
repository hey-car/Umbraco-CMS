﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using umbraco.BusinessLogic;
using System.Web;
using System.IO;

namespace umbraco.IO
{
    //all paths has a starting but no trailing /
    public class SystemDirectories
    {
        public static string Bin
        {
            get
            {
                return IOHelper.returnPath("umbracoBinDirectory", "~/bin");
            }
        }

        public static string Base
        {
            get
            {
                return IOHelper.returnPath("umbracoBaseDirectory", "~/base");
            }
        }

        public static string Config
        {
            get
            {
                return IOHelper.returnPath("umbracoConfigDirectory", "~/config");
            }
        }
                
        public static string Css
        {
            get
            {
                return IOHelper.returnPath("umbracoCssDirectory", "~/css");
            }
        }

        public static string Data
        {
            get
            {
                return IOHelper.returnPath("umbracoStorageDirectory", "~/App_Data");
            }
        }

        public static string Install
        {
            get
            {
                return IOHelper.returnPath("umbracoInstallPath", "~/install");
            }
        }

        public static string Masterpages
        {
            get
            {
                return IOHelper.returnPath("umbracoMasterPagesPath", "~/masterpages");
            }
        }

       
        public static string Media
        {
            get
            {
                return IOHelper.returnPath("umbracoMediaPath", "~/media");
            }
        }

        [Obsolete("Please use MacroScripts instead!", true)]
        public static string Python
        {
            get
            {
                return MacroScripts;
            }
        }

        public static string MacroScripts
        {
            get
            {
                // for legacy we test for the python path first, but else we use the new default location
                string tempPath = IOHelper.returnPath("umbracoPythonPath", "") == String.Empty
                                      ? IOHelper.returnPath("umbracoMacroScriptPath", "~/macroScripts")
                                      : IOHelper.returnPath("umbracoPythonPath", "~/python");
                return tempPath;
            }
        }

        public static string Scripts
        {
            get
            {
                return IOHelper.returnPath("umbracoScriptsPath", "~/scripts");
            }
        }

        public static string Umbraco
        {
            get
            {
                return IOHelper.returnPath("umbracoPath", "~/umbraco");
            }
        }

        public static string Umbraco_client
        {
            get
            {
                return IOHelper.returnPath("umbracoClientPath", "~/umbraco_client");
            }
        }

        public static string Usercontrols
        {
            get
            {
                return IOHelper.returnPath("umbracoUsercontrolsPath", "~/usercontrols");
            }
        }

        public static string Webservices
        {
            get
            {
                return IOHelper.returnPath("umbracoWebservicesPath", "~/umbraco/webservices");
            }
        }

        public static string Xslt
        {
            get {
                return IOHelper.returnPath("umbracoXsltPath", "~/xslt");
            }
        }

        public static string Packages
        {
            get
            {
                //by default the packages folder should exist in the data folder
                return IOHelper.returnPath("umbracoPackagesPath", Data + IOHelper.DirSepChar + "packages");
            }
        }

        public static string Preview
        {
            get
            {
                //by default the packages folder should exist in the data folder
                return IOHelper.returnPath("umbracoPreviewPath", Data + IOHelper.DirSepChar + "preview");
            }
        }

        public static string Root
        {
            get
            {
                string appPath = HttpRuntime.AppDomainAppVirtualPath ?? string.Empty;
                if (appPath == "/")
                    appPath = string.Empty;

                return appPath;
            }
        }
    }


    
}

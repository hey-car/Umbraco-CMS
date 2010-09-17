﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using umbraco.DataLayer;
using umbraco.BusinessLogic;

namespace umbraco.presentation.umbraco.webservices
{
    /// <summary>
    /// Summary description for $codebehindclassname$
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    public class TagsAutoCompleteHandler : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {

            context.Response.ContentType = "text/plain";

            int count = 2;
            string prefixText = context.Request.QueryString["q"];
            string group = context.Request.QueryString["group"];
            string id = context.Request.QueryString["id"];
           
            string sql;

            IRecordsReader rr;

            try
            {
                //if all is correct
                if (!String.IsNullOrEmpty(group) && !String.IsNullOrEmpty(id))
                {
                    sql = @"SELECT TOP (20) tag FROM cmsTags WHERE tag LIKE @prefix AND cmsTags.id not in 
                        (SELECT tagID FROM cmsTagRelationShip WHERE NodeId = @nodeId) AND cmstags.[group] = @group;";

                    rr = SqlHelper.ExecuteReader(sql,
                        SqlHelper.CreateParameter("@count", count),
                        SqlHelper.CreateParameter("@prefix", prefixText + "%"),
                        SqlHelper.CreateParameter("@nodeId", id),
                        SqlHelper.CreateParameter("@group", group)
                        );



                }
                else
                {
                    //fallback...
                    sql = "SELECT TOP (20) tag FROM cmsTags WHERE tag LIKE @prefix";

                    rr = SqlHelper.ExecuteReader(sql,
                       SqlHelper.CreateParameter("@count", count),
                       SqlHelper.CreateParameter("@prefix", prefixText + "%")
                       );
                }


                while (rr.Read())
                {
                    context.Response.Write(rr.GetString("tag") + Environment.NewLine);
                }


            }
            catch (Exception ex)
            {
                Log.Add(LogTypes.Debug, -1, ex.ToString());
            }

        }

        public static ISqlHelper SqlHelper
        {
            get { return Application.SqlHelper; }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}

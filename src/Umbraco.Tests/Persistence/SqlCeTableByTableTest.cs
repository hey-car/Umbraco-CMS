﻿using System;
using System.Configuration;
using System.Data.SqlServerCe;
using System.IO;
using NUnit.Framework;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Tests.TestHelpers;

namespace Umbraco.Tests.Persistence
{
    [TestFixture]
    public class SqlCeTableByTableTest : BaseTableByTableTest
    {
        private Database _database;

        #region Overrides of BaseTableByTableTest

        [SetUp]
        public override void Initialize()
        {
            string path = TestHelper.CurrentAssemblyDirectory;
            AppDomain.CurrentDomain.SetData("DataDirectory", path);

            //Delete database file before continueing
            string filePath = string.Concat(path, "\\test.sdf");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            //Create the Sql CE database
            var engine = new SqlCeEngine("Datasource=|DataDirectory|test.sdf");
            engine.CreateDatabase();

            SyntaxConfig.SqlSyntaxProvider = SqlCeSyntaxProvider.Instance;

            _database = new Database("Datasource=|DataDirectory|test.sdf",
                                     "System.Data.SqlServerCe.4.0");
        }

        [TearDown]
        public override void TearDown()
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", null);
        }

        public override Database Database
        {
            get { return _database; }
        }

        #endregion
    }
}
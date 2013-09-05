﻿using System.Linq;
using System.Collections.ObjectModel;
using Lucene.Net.Documents;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Core.Models;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.ObjectResolution;
using Umbraco.Core.PropertyEditors;
using Umbraco.Web;
using Umbraco.Tests.TestHelpers;
using umbraco.BusinessLogic;

namespace Umbraco.Tests.PublishedContent
{
    [TestFixture]
    public class PublishedContentMoreTests
    {
        // read http://stackoverflow.com/questions/7713326/extension-method-that-works-on-ienumerablet-and-iqueryablet
        // and http://msmvps.com/blogs/jon_skeet/archive/2010/10/28/overloading-and-generic-constraints.aspx
        // and http://blogs.msdn.com/b/ericlippert/archive/2009/12/10/constraints-are-not-part-of-the-signature.aspx

        private PluginManager _pluginManager;

        [SetUp]
        public void Setup()
        {
            // this is so the model factory looks into the test assembly
            _pluginManager = PluginManager.Current;
            PluginManager.Current = new PluginManager(false)
                {
                    AssembliesToScan = _pluginManager.AssembliesToScan
                        .Union(new[] { typeof (PublishedContentMoreTests).Assembly})
                };

            PropertyValueConvertersResolver.Current =
                new PropertyValueConvertersResolver();
            PublishedContentModelFactoryResolver.Current = 
                new PublishedContentModelFactoryResolver();
            Resolution.Freeze();

            var caches = CreatePublishedContent();

            ApplicationContext.Current = new ApplicationContext(false) { IsReady = true };
            var factory = new FakeHttpContextFactory("http://umbraco.local/");
            StateHelper.HttpContext = factory.HttpContext;
            var context = new UmbracoContext(
                factory.HttpContext,
                ApplicationContext.Current,
                caches);
            UmbracoContext.Current = context;
        }
        
        [TearDown]
        public void TearDown()
        {
            PluginManager.Current = _pluginManager;
            ApplicationContext.Current.DisposeIfDisposable();
            ApplicationContext.Current = null;
        }

        [Test]
        public void First()
        {
            var content = UmbracoContext.Current.ContentCache.GetAtRoot().First();
            Assert.AreEqual("Content 1", content.Name);
        }

        [Test]
        public void DefaultContentSetIsSiblings()
        {
            var content = UmbracoContext.Current.ContentCache.GetAtRoot().First();
            Assert.AreEqual(0, content.Index());
            Assert.IsTrue(content.IsFirst());
        }

        [Test]
        public void RunOnLatestContentSet()
        {
            // get first content
            var content = UmbracoContext.Current.ContentCache.GetAtRoot().First();
            var id = content.Id;
            Assert.IsTrue(content.IsFirst());

            // reverse => should be last, but set has not changed => still first
            content = UmbracoContext.Current.ContentCache.GetAtRoot().Reverse().First(x => x.Id == id);
            Assert.IsTrue(content.IsFirst());
            Assert.IsFalse(content.IsLast());

            // reverse + new set => now it's last
            content = UmbracoContext.Current.ContentCache.GetAtRoot().Reverse().ToContentSet().First(x => x.Id == id);
            Assert.IsFalse(content.IsFirst());
            Assert.IsTrue(content.IsLast());

            // reverse that set => should be first, but no new set => still last
            content = UmbracoContext.Current.ContentCache.GetAtRoot().Reverse().ToContentSet().Reverse().First(x => x.Id == id);
            Assert.IsFalse(content.IsFirst());
            Assert.IsTrue(content.IsLast());
        }

        [Test]
        public void Distinct()
        {
            var content = UmbracoContext.Current.ContentCache.GetAtRoot()
                .Distinct()
                .Distinct()
                .ToContentSet()
                .First();

            Assert.AreEqual("Content 1", content.Name);
            Assert.IsTrue(content.IsFirst());
            Assert.IsFalse(content.IsLast());

            content = content.Next();
            Assert.AreEqual("Content 2", content.Name);
            Assert.IsFalse(content.IsFirst());
            Assert.IsFalse(content.IsLast());

            content = content.Next();
            Assert.AreEqual("Content 2Sub", content.Name);
            Assert.IsFalse(content.IsFirst());
            Assert.IsTrue(content.IsLast());
        }

        [Test]
        public void Position()
        {
            var content = UmbracoContext.Current.ContentCache.GetAtRoot()
                .Where(x => x.GetPropertyValue<int>("prop1") == 1234)
                .ToContentSet()
                .ToArray();

            Assert.IsTrue(content.First().IsFirst());
            Assert.IsFalse(content.First().IsLast());
            Assert.IsFalse(content.First().Next().IsFirst());
            Assert.IsFalse(content.First().Next().IsLast());
            Assert.IsFalse(content.First().Next().Next().IsFirst());
            Assert.IsTrue(content.First().Next().Next().IsLast());
        }

        static SolidPublishedCaches CreatePublishedContent()
        {
            var caches = new SolidPublishedCaches();
            var cache = caches.ContentCache;

            var props = new[]
                    {
                        new PublishedPropertyType("prop1", System.Guid.Empty, 1, 1), 
                    };

            var contentType1 = new PublishedContentType(1, "ContentType1", props);
            var contentType2 = new PublishedContentType(2, "ContentType2", props);
            var contentType2s = new PublishedContentType(3, "ContentType2Sub", props);

            cache.Add(new SolidPublishedContent(contentType1)
                {
                    Id = 1,
                    SortOrder = 0,
                    Name = "Content 1",
                    UrlName = "content-1",
                    Path = "/1",
                    Level = 1,
                    Url = "/content-1",
                    ParentId = -1,
                    ChildIds = new int[] {},
                    Properties = new Collection<IPublishedProperty>
                        {
                            new SolidPublishedProperty
                                {
                                    Alias = "prop1",
                                    HasValue = true,
                                    Value = 1234,
                                    RawValue = "1234"
                                }
                        }
                });

            cache.Add(new SolidPublishedContent(contentType2)
                {
                    Id = 2,
                    SortOrder = 1,
                    Name = "Content 2",
                    UrlName = "content-2",
                    Path = "/2",
                    Level = 1,
                    Url = "/content-2",
                    ParentId = -1,
                    ChildIds = new int[] { },
                    Properties = new Collection<IPublishedProperty>
                            {
                                new SolidPublishedProperty
                                    {
                                        Alias = "prop1",
                                        HasValue = true,
                                        Value = 1234,
                                        RawValue = "1234"
                                    }
                            }
                });

            cache.Add(new SolidPublishedContent(contentType2s)
            {
                Id = 3,
                SortOrder = 2,
                Name = "Content 2Sub",
                UrlName = "content-2sub",
                Path = "/3",
                Level = 1,
                Url = "/content-2sub",
                ParentId = -1,
                ChildIds = new int[] { },
                Properties = new Collection<IPublishedProperty>
                            {
                                new SolidPublishedProperty
                                    {
                                        Alias = "prop1",
                                        HasValue = true,
                                        Value = 1234,
                                        RawValue = "1234"
                                    }
                            }
            });

            return caches;
        }
    }
}

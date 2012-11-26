﻿using System;
using System.Linq;
using NUnit.Framework;
using Umbraco.Core.Models;
using Umbraco.Core.Models.Membership;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.Caching;
using Umbraco.Core.Persistence.Querying;
using Umbraco.Core.Persistence.Repositories;
using Umbraco.Core.Persistence.UnitOfWork;
using Umbraco.Tests.TestHelpers;
using Umbraco.Tests.TestHelpers.Entities;

namespace Umbraco.Tests.Persistence.Repositories
{
    [TestFixture]
    public class ContentRepositoryTest : BaseDatabaseFactoryTest
    {
        [SetUp]
        public override void Initialize()
        {
            base.Initialize();

            CreateTestData();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void Can_Instantiate_Repository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();

            // Act
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Assert
            Assert.That(repository, Is.Not.Null);
        }

        [Test]
        public void Can_Perform_Add_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var contentTypeRepository = RepositoryResolver.ResolveByType<IContentTypeRepository, IContentType, int>(unitOfWork);
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            ContentType contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
            Content textpage = MockedContent.CreateSimpleContent(contentType);

            // Act
            contentTypeRepository.AddOrUpdate(contentType);
            repository.AddOrUpdate(textpage);
            unitOfWork.Commit();

            // Assert
            Assert.That(contentType.HasIdentity, Is.True);
            Assert.That(textpage.HasIdentity, Is.True);
        }

        [Test]
        public void Can_Perform_Multiple_Adds_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var contentTypeRepository = RepositoryResolver.ResolveByType<IContentTypeRepository, IContentType, int>(unitOfWork);
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            ContentType contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
            Content textpage = MockedContent.CreateSimpleContent(contentType);
            
            // Act
            contentTypeRepository.AddOrUpdate(contentType);
            repository.AddOrUpdate(textpage);
            unitOfWork.Commit();
            
            Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Page 1", textpage.Id);
            repository.AddOrUpdate(subpage);
            unitOfWork.Commit();

            // Assert
            Assert.That(contentType.HasIdentity, Is.True);
            Assert.That(textpage.HasIdentity, Is.True);
            Assert.That(subpage.HasIdentity, Is.True);
            Assert.That(textpage.Id, Is.EqualTo(subpage.ParentId));
        }

        [Test]
        public void Can_Perform_Multiple_Adds_On_ContentRepository_With_RepositoryResolver()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var contentTypeRepository = RepositoryResolver.ResolveByType<IContentTypeRepository, IContentType, int>(unitOfWork);
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            ContentType contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
            Content textpage = MockedContent.CreateSimpleContent(contentType);

            // Act
            contentTypeRepository.AddOrUpdate(contentType);
            repository.AddOrUpdate(textpage);
            unitOfWork.Commit();

            var repository2 = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);
            Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Page 1", textpage.Id);
            repository2.AddOrUpdate(subpage);
            unitOfWork.Commit();

            // Assert
            Assert.That(contentType.HasIdentity, Is.True);
            Assert.That(textpage.HasIdentity, Is.True);
            Assert.That(subpage.HasIdentity, Is.True);
            Assert.That(textpage.Id, Is.EqualTo(subpage.ParentId));
        }

        [Test]
        public void Can_Verify_Fresh_Entity_Is_Not_Dirty()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var content = repository.Get(1048);
            bool dirty = ((Content) content).IsDirty();

            // Assert
            Assert.That(dirty, Is.False);
        }

        [Test]
        public void Can_Perform_Update_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var content = repository.Get(1047);
            content.Name = "About 2";
            repository.AddOrUpdate(content);
            unitOfWork.Commit();
            var updatedContent = repository.Get(1047);

            // Assert
            Assert.That(updatedContent.Id, Is.EqualTo(content.Id));
            Assert.That(updatedContent.Name, Is.EqualTo(content.Name));
        }

        [Test]
        public void Can_Perform_Delete_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var contentTypeRepository = RepositoryResolver.ResolveByType<IContentTypeRepository, IContentType, int>(unitOfWork);
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            var contentType = contentTypeRepository.Get(1045);
            var content = new Content(1048, contentType);
            content.Name = "Textpage 2 Child Node";
            content.CreatorId = 0;
            content.WriterId = 0;

            // Act
            repository.AddOrUpdate(content);
            unitOfWork.Commit();
            var id = content.Id;

            var repository2 = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);
            repository2.Delete(content);
            unitOfWork.Commit();

            var content1 = repository2.Get(id);

            // Assert
            Assert.That(content1, Is.Null);
        }

        [Test]
        public void Can_Perform_Get_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var content = repository.Get(1048);

            // Assert
            Assert.That(content.Id, Is.EqualTo(1048));
            Assert.That(content.CreateDate, Is.GreaterThan(DateTime.MinValue));
            Assert.That(content.UpdateDate, Is.GreaterThan(DateTime.MinValue));
            Assert.That(content.ParentId, Is.Not.EqualTo(0));
            Assert.That(content.Name, Is.EqualTo("Text Page 2"));
            Assert.That(content.SortOrder, Is.EqualTo(1));
            Assert.That(content.Version, Is.Not.EqualTo(Guid.Empty));
            Assert.That(content.ContentTypeId, Is.EqualTo(1045));
            Assert.That(content.Path, Is.Not.Empty);
            Assert.That(content.Properties.Any(), Is.True);
        }

        [Test]
        public void Can_Perform_GetByQuery_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var query = Query<IContent>.Builder.Where(x => x.Level == 2);
            var result = repository.GetByQuery(query);

            // Assert
            Assert.That(result.Count(), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Perform_GetAll_By_Param_Ids_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var contents = repository.GetAll(1047, 1048);

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.EqualTo(2));
        }

        [Test]
        public void Can_Perform_GetAll_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var contents = repository.GetAll();

            // Assert
            Assert.That(contents, Is.Not.Null);
            Assert.That(contents.Any(), Is.True);
            Assert.That(contents.Count(), Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Can_Perform_Exists_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var exists = repository.Exists(1046);

            // Assert
            Assert.That(exists, Is.True);
        }

        [Test]
        public void Can_Perform_Count_On_ContentRepository()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            int level = 2;
            var query = Query<IContent>.Builder.Where(x => x.Level == level);
            var result = repository.Count(query);

            // Assert
            Assert.That(result, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Can_Verify_Keys_Set()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var textpage = repository.Get(1046);
            var subpage = repository.Get(1047);
            var trashed = repository.Get(1049);

            // Assert
            Assert.That(textpage.Key.ToString().ToUpper(), Is.EqualTo("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));
            Assert.That(subpage.Key.ToString().ToUpper(), Is.EqualTo("FF11402B-7E53-4654-81A7-462AC2108059"));
            Assert.That(trashed.Key, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void Can_Get_Content_By_Guid_Key()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);

            // Act
            var query = Query<IContent>.Builder.Where(x => x.Key == new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0"));
            var content = repository.GetByQuery(query).SingleOrDefault();

            // Assert
            Assert.That(content, Is.Not.Null);
            Assert.That(content.Id, Is.EqualTo(1046));

        }

        [Test]
        public void Can_Create_Different_Language_Version()
        {
            // Arrange
            var provider = new PetaPocoUnitOfWorkProvider();
            var unitOfWork = provider.GetUnitOfWork();
            var repository = RepositoryResolver.ResolveByType<IContentRepository, IContent, int>(unitOfWork);
            var content = repository.Get(1047);

            // Act
            content.Language = "da-DK";
            content.Name = "Tekst Side 1";
            repository.AddOrUpdate(content);
            unitOfWork.Commit();

            var latest = repository.Get(1047);
            var english = repository.GetByLanguage(1047, "en-US");
            var danish = repository.GetByLanguage(1047, "da-DK");

            // Assert
            Assert.That(latest.Name, Is.EqualTo("Tekst Side 1"));
            Assert.That(english.Name, Is.EqualTo("Text Page 1"));
            Assert.That(danish.Name, Is.EqualTo("Tekst Side 1"));
        }

        public void CreateTestData()
        {
            //Create and Save ContentType "umbTextpage" -> 1045
            ContentType contentType = MockedContentTypes.CreateSimpleContentType("umbTextpage", "Textpage");
            contentType.Key = new Guid("1D3A8E6E-2EA9-4CC1-B229-1AEE19821522");
            ServiceContext.ContentTypeService.Save(contentType);

            //Create and Save Content "Homepage" based on "umbTextpage" -> 1046
            Content textpage = MockedContent.CreateSimpleContent(contentType);
            textpage.Key = new Guid("B58B3AD4-62C2-4E27-B1BE-837BD7C533E0");
            ServiceContext.ContentService.Save(textpage, 0);

            //Create and Save Content "Text Page 1" based on "umbTextpage" -> 1047
            Content subpage = MockedContent.CreateSimpleContent(contentType, "Text Page 1", textpage.Id);
            subpage.Key = new Guid("FF11402B-7E53-4654-81A7-462AC2108059");
            ServiceContext.ContentService.Save(subpage, 0);

            //Create and Save Content "Text Page 1" based on "umbTextpage" -> 1048
            Content subpage2 = MockedContent.CreateSimpleContent(contentType, "Text Page 2", textpage.Id);
            ServiceContext.ContentService.Save(subpage2, 0);

            //Create and Save Content "Text Page Deleted" based on "umbTextpage" -> 1049
            Content trashed = MockedContent.CreateSimpleContent(contentType, "Text Page Deleted", -20);
            trashed.Trashed = true;
            ServiceContext.ContentService.Save(trashed, 0);
        }
    }
}
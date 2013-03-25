﻿using System.Globalization;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.Repositories;

namespace Umbraco.Core.Persistence.Factories
{
    internal class UmbracoEntityFactory : IEntityFactory<UmbracoEntity, EntityRepository.UmbracoEntityDto>
    {
        public UmbracoEntity BuildEntity(EntityRepository.UmbracoEntityDto dto)
        {
            var entity = new UmbracoEntity(dto.Trashed)
                             {
                                 CreateDate = dto.CreateDate,
                                 CreatorId = dto.UserId.Value,
                                 Id = dto.NodeId,
                                 Key = dto.UniqueId.Value,
                                 Level = dto.Level,
                                 Name = dto.Text,
                                 NodeObjectTypeId = dto.NodeObjectType.Value,
                                 ParentId = dto.ParentId,
                                 Path = dto.Path,
                                 SortOrder = dto.SortOrder,
                                 HasChildren = dto.Children > 0,
                                 IsPublished = dto.HasPublishedVersion.HasValue && dto.HasPublishedVersion.Value > 0
                             };
            return entity;
        }

        public EntityRepository.UmbracoEntityDto BuildDto(UmbracoEntity entity)
        {
            var node = new EntityRepository.UmbracoEntityDto
                           {
                               CreateDate = entity.CreateDate,
                               Level = short.Parse(entity.Level.ToString(CultureInfo.InvariantCulture)),
                               NodeId = entity.Id,
                               NodeObjectType = entity.NodeObjectTypeId,
                               ParentId = entity.ParentId,
                               Path = entity.Path,
                               SortOrder = entity.SortOrder,
                               Text = entity.Name,
                               Trashed = entity.Trashed,
                               UniqueId = entity.Key,
                               UserId = entity.CreatorId
                           };
            return node;
        }
    }
}
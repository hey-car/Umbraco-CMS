using System;
using System.Collections.Generic;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.UnitOfWork;

namespace Umbraco.Core.Services
{
    internal class MemberTypeService : IMemberTypeService
    {
        private readonly IDatabaseUnitOfWorkProvider _uowProvider;
        private readonly RepositoryFactory _repositoryFactory;

        public MemberTypeService()
            : this(new PetaPocoUnitOfWorkProvider(), new RepositoryFactory())
        {}

        public MemberTypeService(RepositoryFactory repositoryFactory)
            : this(new PetaPocoUnitOfWorkProvider(), repositoryFactory)
        { }

        public MemberTypeService(IDatabaseUnitOfWorkProvider provider, RepositoryFactory repositoryFactory)
        {
            if (provider == null) throw new ArgumentNullException("provider");
            if (repositoryFactory == null) throw new ArgumentNullException("repositoryFactory");
            _uowProvider = provider;
            _repositoryFactory = repositoryFactory;
        }

        public IEnumerable<IMemberType> GetAllMemberTypes(params int[] ids)
        {
            using (var repository = _repositoryFactory.CreateMemberTypeRepository(_uowProvider.GetUnitOfWork()))
            {
                return repository.GetAll(ids);
            }
        }

    }
}
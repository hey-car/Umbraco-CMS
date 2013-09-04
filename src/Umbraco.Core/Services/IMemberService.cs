﻿using System;
using System.Collections.Generic;
using Umbraco.Core.Models;

namespace Umbraco.Core.Services
{
    /// <summary>
    /// Defines the MemberService, which is an easy access to operations involving (umbraco) members.
    /// </summary>
    internal interface IMemberService : IMembershipMemberService
    {
        IMember GetById(int id);
        IMember GetByKey(Guid id);
        IEnumerable<IMember> GetMembersByMemberType(string memberTypeAlias);
        IEnumerable<IMember> GetMembersByGroup(string memberGroupName);
        IEnumerable<IMember> GetAllMembers(params int[] ids);
    }

    /// <summary>
    /// Defines part of the MemberService, which is specific to methods used by the membership provider.
    /// </summary>
    /// <remarks>
    /// Idea is to have this is an isolated interface so that it can be easily 'replaced' in the membership provider impl.
    /// </remarks>
    internal interface IMembershipMemberService : IService
    {
        IMember CreateMember(string username, string email, string password, string memberTypeAlias, int userId = 0);

        IMember GetById(object id);

        IMember GetByEmail(string email);

        IMember GetByUsername(string login);

        void Delete(IMember membershipUser);

        void Save(IMember membershipUser);
    }
}
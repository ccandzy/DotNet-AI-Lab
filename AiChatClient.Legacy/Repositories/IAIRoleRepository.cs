using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Entities;

namespace Repositories
{
    public interface IAIRoleRepository
    {
        Task<List<AIRoleEntity>> GetEnabledRolesAsync();
    }
}

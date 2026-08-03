using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Settings;

namespace Services
{
    public interface IAIRoleService
    {
        Task<List<AIRole>> GetRolesAsync();
    }
}

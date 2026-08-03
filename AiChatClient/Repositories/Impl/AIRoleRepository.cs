using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Data;
using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Impl
{
    public class AIRoleRepository : IAIRoleRepository
    {
        private readonly AppDbContext _context;


        public AIRoleRepository(
            AppDbContext context)
        {
            _context = context;
        }


        public async Task<List<AIRoleEntity>> GetEnabledRolesAsync()
        {
            return await _context.AIRoles
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }
    }
}

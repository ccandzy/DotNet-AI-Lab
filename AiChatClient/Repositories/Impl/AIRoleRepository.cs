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
        private readonly IDbContextFactory<AppDbContext> _contextFactory;


        public AIRoleRepository(
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }


        public async Task<List<AIRoleEntity>> GetEnabledRolesAsync()
        {
            await using var context = await _contextFactory
                .CreateDbContextAsync();

            return await context.AIRoles
                .AsNoTracking()
                .Where(x => x.IsEnabled)
                .ToListAsync();
        }
    }
}

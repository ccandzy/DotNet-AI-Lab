using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Mappers;
using AiChatClient.Settings;
using Repositories;

namespace Services.Impl
{
    public class AIRoleService : IAIRoleService
    {
        private readonly IAIRoleRepository _repository;


        public AIRoleService(
            IAIRoleRepository repository)
        {
            _repository = repository;
        }


        public async Task<List<AIRole>> GetRolesAsync()
        {
            var entities =
                await _repository.GetEnabledRolesAsync();


            return entities
                .Select(AIRoleMapper.ToModel)
                .ToList();
        }
    }
}

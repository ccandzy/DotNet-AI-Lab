using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiChatClient.Settings
{
    public class AIRole
    {
        public Guid Id { get; set; }


        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; }


        /// <summary>
        /// 角色描述
        /// </summary>
        public string Description { get; set; }


        /// <summary>
        /// 图标
        /// </summary>
        public string Avatar { get; set; }


        /// <summary>
        /// 系统提示词
        /// </summary>
        public string SystemPrompt { get; set; }


        public bool IsEnabled { get; set; }


        public DateTime CreateTime { get; set; }
    }
}

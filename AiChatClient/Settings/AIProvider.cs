using System;
using System.Collections.Generic;
using System.Text;

namespace Settings
{
    public class AIProvider
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }
    }
}

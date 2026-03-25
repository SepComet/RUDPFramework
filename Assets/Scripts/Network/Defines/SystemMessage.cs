using System;

namespace Network.Defines
{
    public class SystemMessage
    {
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Level { get; set; } = "info"; // "info", "warning", "error"
    }
}
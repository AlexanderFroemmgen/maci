using Backend.Data.Persistence.Model;

namespace Backend.Data.Transfer
{
    public class LogMessageDto
    {
        public string Key { get; set; }

        public LogMessageType Type { get; set; }

        public int Offset { get; set; }

        public string Message { get; set; }
    }
}
using System.IO;

namespace Backend.Util
{
    public static class RandomUtils
    {
        public static string GetRandomIdentifier(int length)
        {
            return Path.GetRandomFileName().Replace(".", "").Substring(0, length);
        }
    }
}
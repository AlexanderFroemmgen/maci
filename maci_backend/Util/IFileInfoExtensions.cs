using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace Backend.Util
{
    public static class IFileInfoExtensions
    {
        public static string ReadToEnd(this IFileInfo fileInfo)
        {
            using (var reader = new StreamReader(fileInfo.CreateReadStream()))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
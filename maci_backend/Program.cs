using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
               .UseUrls("http://*:63658")
               .UseKestrel()
               .UseContentRoot(Directory.GetCurrentDirectory())
               .UseStartup<Startup>()
               .Build();

            host.Run();
        }
    }
}
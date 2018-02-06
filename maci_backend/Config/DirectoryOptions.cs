using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Config
{
    public class DirectoryOptions
    {
        public string DataLocation { get; set; }

        public void CopyDirectoryRecursively(DirectoryInfo fromDi, string destPath)
        {
            new DirectoryInfo(destPath).Create();

            foreach (var dirEntry in fromDi.GetDirectories())
            {
                var tmpDestPath = destPath + "/" + dirEntry.Name;

                new DirectoryInfo(tmpDestPath).Create();
                CopyDirectoryRecursively(dirEntry, tmpDestPath);
            }

            foreach (var dirEntry in fromDi.GetFiles())
            {
                dirEntry.CopyTo(destPath + "/" + dirEntry.Name);
            }
        }

        public IEnumerable<string> GetAllFilesRecursively(string dirPath)
        {
            var result = new List<string>();
            GetAllFilesRecursively(new DirectoryInfo(dirPath), result);
            return result;
        }

        private void GetAllFilesRecursively(DirectoryInfo di, List<string> result, string prefix = "")
        {
            foreach (var dirEntry in di.GetFiles())
            {
                result.Add(prefix + dirEntry.Name);
            }

            foreach (var dirEntry in di.GetDirectories())
            {
                GetAllFilesRecursively(dirEntry, result, prefix + dirEntry.Name + "/");
            }
        }

        public string GetFileContents(string relativePath)
        {
            using (var reader = new StreamReader(new FileStream(relativePath, FileMode.Open)))
            {
                return reader.ReadToEnd();
            }
        }

        public void SetFileContents(string relativePath, string content)
        {
            using (var writer = new StreamWriter(new FileStream(relativePath, FileMode.Create)))
            {
                writer.Write(content);
            }
        }
    }
}

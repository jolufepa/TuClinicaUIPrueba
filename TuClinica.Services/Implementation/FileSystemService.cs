using System.IO; // <-- Importante
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class FileSystemService : IFileSystemService
    {
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }
    }
}
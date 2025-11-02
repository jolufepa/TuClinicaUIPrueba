namespace TuClinica.Core.Interfaces.Services
{
    public interface IFileSystemService
    {
        bool FileExists(string path);
        string ReadAllText(string path);
    }
}
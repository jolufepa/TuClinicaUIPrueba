using System.Threading.Tasks;

namespace TuClinica.Core.Interfaces.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(string sourceFilePath, int patientId);
        void OpenFile(string relativePath);
        void DeleteFile(string relativePath);
        string GetFullPath(string relativePath);
    }
}
namespace TuClinica.Core.Interfaces.Services
{
    public interface IFileDialogService
    {
        (bool Ok, string FilePath) ShowSaveDialog(string filter, string title, string defaultFileName);
        (bool Ok, string FilePath) ShowOpenDialog(string filter, string title);
    }
}

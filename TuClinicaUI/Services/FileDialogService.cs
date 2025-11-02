using Microsoft.Win32; // <-- AÑADE ESTE USING
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.UI.Services
{
    public class FileDialogService : IFileDialogService
    {
        public (bool Ok, string FilePath) ShowOpenDialog(string filter, string title)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = filter,
                Title = title
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                return (true, openFileDialog.FileName);
            }
            return (false, string.Empty);
        }

        public (bool Ok, string FilePath) ShowSaveDialog(string filter, string title, string defaultFileName)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName
            };

            bool? result = saveFileDialog.ShowDialog();

            if (result == true)
            {
                return (true, saveFileDialog.FileName);
            }
            return (false, string.Empty);
        }
    }
}
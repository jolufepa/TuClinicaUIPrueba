using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // Necesitas este using para RelayCommand y AsyncRelayCommand
using Microsoft.Extensions.DependencyInjection;
using System; // Agregado para IServiceProvider
using System.Threading.Tasks;
using System.Windows; // Para MessageBox y PasswordBox
using TuClinica.Core.Interfaces.Services; // Para IAuthService
using TuClinica.UI.Views; // Para LoginWindow

namespace TuClinica.UI.ViewModels
{
    // Asegúrate de que la clase sea 'partial' si usas [ObservableProperty]
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _username = string.Empty;

        // *** DEFINICIÓN MANUAL DE COMANDOS ***
        public IAsyncRelayCommand<object> LoginAsyncCommand { get; }
        public IRelayCommand<Window> CloseWindowCommand { get; }
        // *** FIN DEFINICIÓN MANUAL ***

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _closeWindowFlag;

        public LoginViewModel(IAuthService authService, IServiceProvider serviceProvider)
        {
            _authService = authService;
            _serviceProvider = serviceProvider;

            // *** INICIALIZACIÓN MANUAL DE COMANDOS EN EL CONSTRUCTOR ***
            LoginAsyncCommand = new AsyncRelayCommand<object?>(LoginAsync); // Asegúrate que el tipo coincida (object?)
            CloseWindowCommand = new RelayCommand<Window?>(CloseWindow); // Asegúrate que el tipo coincida (Window?)
            // *** FIN INICIALIZACIÓN MANUAL ***
        }

        // Método que ejecuta el comando LoginAsyncCommand
        private async Task LoginAsync(object? parameter) // El parámetro debe ser nullable (object?)
        {
            ErrorMessage = null;
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            if (passwordBox == null) return;

            string password = passwordBox.Password;
            bool success = await _authService.LoginAsync(Username, password);

            if (success)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
                CloseWindowFlag = true; // Señal para cerrar esta ventana
            }
            else
            {
                ErrorMessage = "Usuario o contraseña incorrectos.";
                passwordBox.Clear();
            }
        }

        // Método que ejecuta el comando CloseWindowCommand
        private void CloseWindow(Window? window) // El parámetro debe ser nullable (Window?)
        {
            window?.Close();
        }
    }
}
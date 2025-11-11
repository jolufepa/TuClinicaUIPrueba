// En: TuClinicaUI/ViewModels/LoginViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input; // Necesitas este using para RelayCommand y AsyncRelayCommand
using Microsoft.Extensions.DependencyInjection;
using System; // Agregado para IServiceProvider
using System.Threading.Tasks;
using System.Windows; // Para MessageBox y PasswordBox
using TuClinica.Core.Interfaces.Services; // Para IAuthService
using TuClinica.Services.Implementation;
using TuClinica.UI.Views; // Para LoginWindow

namespace TuClinica.UI.ViewModels
{
    // Asegúrate de que la clase sea 'partial' si usas [ObservableProperty]
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IAuthService _authService;

        // --- CAMBIO 1: Reemplazar IServiceProvider ---
        private readonly IServiceScopeFactory _scopeFactory;

        private readonly IInactivityService _inactivityService;

        [ObservableProperty]
        private string _username = string.Empty;

        // *** DEFINICIÓN MANUAL DE COMANDOS (Sin cambios) ***
        public IAsyncRelayCommand<object> LoginAsyncCommand { get; }
        public IRelayCommand<Window> CloseWindowCommand { get; }
        // *** FIN DEFINICIÓN MANUAL ***

        [ObservableProperty]
        private string? _errorMessage;

        [ObservableProperty]
        private bool _closeWindowFlag;

        // --- CAMBIO 2: Actualizar el constructor ---
        public LoginViewModel(IAuthService authService,
                              IServiceScopeFactory scopeFactory, // <-- MODIFICADO
                              IInactivityService inactivityService)
        {
            _authService = authService;
            _scopeFactory = scopeFactory; // <-- MODIFICADO
            _inactivityService = inactivityService;

            // *** INICIALIZACIÓN MANUAL DE COMANDOS EN EL CONSTRUCTOR (Sin cambios) ***
            LoginAsyncCommand = new AsyncRelayCommand<object?>(LoginAsync); // Asegúrate que el tipo coincida (object?)
            CloseWindowCommand = new RelayCommand<Window?>(CloseWindow); // Asegúrate que el tipo coincida (Window?)
            // *** FIN INICIALIZACIÓN MANUAL ***
        }


        private async Task LoginAsync(object? parameter) // El parámetro debe ser nullable (object?)
        {
            ErrorMessage = null;
            var passwordBox = parameter as System.Windows.Controls.PasswordBox;
            if (passwordBox == null) return;

            string password = passwordBox.Password;
            bool success = await _authService.LoginAsync(Username, password);

            if (success)
            {
                _inactivityService.Start();

                // --- CAMBIO 3: Usar 'scopeFactory' para resolver la ventana 'Singleton' ---
                // Aunque MainWindow es Singleton, es más limpio resolverlo desde un
                // ámbito raíz (el propio 'AppHost.Services') o, si estamos en un
                // servicio 'Transient' como este, crear un scope para resolverlo.
                using (var scope = _scopeFactory.CreateScope())
                {
                    var mainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
                    mainWindow.Show();
                }

                CloseWindowFlag = true; // Señal para cerrar esta ventana
            }
            else
            {
                ErrorMessage = "Usuario o contraseña incorrectos.";
                passwordBox.Clear();
            }
        }


        private void CloseWindow(Window? window) // El parámetro debe ser nullable (Window?)
        {
            window?.Close();
        }
    }
}
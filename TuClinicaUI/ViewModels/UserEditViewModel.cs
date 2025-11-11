using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TuClinica.Core.Enums;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using BCrypt.Net;

namespace TuClinica.UI.ViewModels
{
    // MUY IMPORTANTE: Asegúrate de que la clase sigue siendo 'partial'
    public partial class UserEditViewModel : BaseViewModel
    {
        private readonly IUserRepository _userRepository;
        private readonly IDialogService _dialogService;
        private User _userToEdit;
        private bool _isNewUser;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))] // Esto DEBERÍA funcionar
        private string _username = string.Empty;

        [ObservableProperty]
        private UserRole _selectedRole;

        [ObservableProperty]
        private bool _isActive;

        public List<UserRole> AvailableRoles { get; } = Enum.GetValues(typeof(UserRole)).Cast<UserRole>().ToList();
        public string Title => _isNewUser ? "Nuevo Usuario" : "Editar Usuario";
        public bool? DialogResult { get; private set; }
        [ObservableProperty]
        private string? _collegeNumber;

        [ObservableProperty]
        private string? _specialty;

        public UserEditViewModel(IUserRepository userRepository, IDialogService dialogService)
        {
            _userRepository = userRepository;
            _dialogService = dialogService;
            _userToEdit = new User();
            _isNewUser = true;
            LoadUserData();
        }

        public void LoadUserData(User? user = null)
        {
            _isNewUser = (user == null);
            _userToEdit = user ?? new User { IsActive = true, Role = UserRole.Recepcionista };
            Username = _userToEdit.Username; // Esto dispara OnUsernameChanged si es diferente
            SelectedRole = _userToEdit.Role;
            IsActive = _userToEdit.IsActive;
            CollegeNumber = _userToEdit.CollegeNumber;
            Specialty = _userToEdit.Specialty;
            OnPropertyChanged(nameof(Title));
            // SaveCommand.NotifyCanExecuteChanged(); // [NotifyCanExecuteChangedFor] debería hacerlo
        }

        // *** AÑADIDO: Método parcial que se ejecuta cuando 'Username' cambia ***
        // Este método lo crea automáticamente el [ObservableProperty] si la clase es 'partial'
        partial void OnUsernameChanged(string value)
        {
            // Forzamos manualmente la reevaluación del comando Save
            SaveCommand.NotifyCanExecuteChanged();
        }


        // *** MÉTODO CanSave ***
        private bool CanSave()
        {
            bool canSaveResult = !string.IsNullOrWhiteSpace(Username);
            return canSaveResult;
        }



        [RelayCommand(CanExecute = nameof(CanSave))]
        private async Task SaveAsync(object? parameter)
        {

            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password ?? string.Empty;

            // 1. Validaciones básicas
            if (string.IsNullOrWhiteSpace(Username))
            {
                _dialogService.ShowMessage("El nombre de usuario no puede estar vacío.", "Error");
                return;
            }
            if (_isNewUser && string.IsNullOrWhiteSpace(password))
            {
                _dialogService.ShowMessage("Un usuario nuevo debe tener una contraseña.", "Error");
                return;
            }

            // --- INICIO DE LA VALIDACIÓN REACTIVADA ---
            bool usernameTaken = await _userRepository.IsUsernameTakenAsync(Username, _isNewUser ? 0 : _userToEdit.Id);
            if (usernameTaken)
            {
                _dialogService.ShowMessage($"El nombre de usuario '{Username}' ya está en uso por otro usuario.", "Nombre Duplicado");
                return; // Detener el guardado
            }
            // --- FIN DE LA VALIDACIÓN REACTIVADA ---

            _userToEdit.Username = Username;
            _userToEdit.Role = SelectedRole;
            _userToEdit.IsActive = IsActive;
            _userToEdit.CollegeNumber = this.CollegeNumber;
            _userToEdit.Specialty = this.Specialty;

            if (!string.IsNullOrWhiteSpace(password))
            {
                // ... (código de hashing como antes) ...
                try
                {
                    _userToEdit.HashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
                }
                catch (Exception hashEx)
                {
                    _dialogService.ShowMessage($"Error al procesar la contraseña:\n{hashEx.Message}", "Error Contraseña");
                    return;
                }
            }
            // ... (resto del try...catch para guardar, como antes) ...
            try
            {
                // ... (AddAsync/UpdateAsync y SaveChangesAsync) ...
                if (_isNewUser)
                {
                    await _userRepository.AddAsync(_userToEdit);
                }
                else
                {
                    _userRepository.Update(_userToEdit); // Usamos Update síncrono base
                }
                int affectedRows = await _userRepository.SaveChangesAsync();

                if (affectedRows > 0 || !_isNewUser)
                {
                    DialogResult = true;
                    OnPropertyChanged(nameof(DialogResult));
                }
                else if (_isNewUser && affectedRows == 0)
                {
                    _dialogService.ShowMessage("El usuario se procesó pero no se guardó (0 filas afectadas). Contacte soporte.", "Advertencia");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al guardar el usuario:\n{ex.Message}", "Error de Base de Datos");
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
            OnPropertyChanged(nameof(DialogResult));
        }
    }
}
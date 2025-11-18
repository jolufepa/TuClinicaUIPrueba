using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq; // Necesario para OrderBy
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class OdontogramPreviewViewModel : ObservableObject
    {
        // --- PROPIEDADES CAMBIADAS: De 4 cuadrantes a 2 arcos ---
        // Esto simplifica el binding en la vista pequeña (Preview)

        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _upperArch = new();

        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _lowerArch = new();

        // Comando para abrir el odontograma completo
        public IRelayCommand OpenFullOdontogramCommand { get; }

        public OdontogramPreviewViewModel()
        {
            OpenFullOdontogramCommand = new RelayCommand(OpenFull);
        }

        private void OpenFull()
        {
            // Envía mensaje para abrir el odontograma completo (OdontogramWindow)
            WeakReferenceMessenger.Default.Send(new OpenOdontogramMessage());
        }

        /// <summary>
        /// Carga los dientes desde el odontograma maestro (PatientFileViewModel)
        /// y los organiza visualmente en dos filas (arriba y abajo).
        /// </summary>
        public void LoadFromMaster(ObservableCollection<ToothViewModel> master)
        {
            // Limpiar arcos
            UpperArch.Clear();
            LowerArch.Clear();

            // --- LÓGICA DE ORDENACIÓN VISUAL (FDI) ---

            // FILA SUPERIOR:
            // 1. Cuadrante 1 (Derecha paciente -> Izquierda pantalla): 18 a 11
            foreach (var tooth in master.Where(t => t.ToothNumber >= 11 && t.ToothNumber <= 18).OrderByDescending(t => t.ToothNumber))
            {
                UpperArch.Add(tooth);
            }

            // 2. Cuadrante 2 (Izquierda paciente -> Derecha pantalla): 21 a 28
            foreach (var tooth in master.Where(t => t.ToothNumber >= 21 && t.ToothNumber <= 28).OrderBy(t => t.ToothNumber))
            {
                UpperArch.Add(tooth);
            }

            // FILA INFERIOR:
            // 3. Cuadrante 4 (Derecha paciente -> Izquierda pantalla): 48 a 41
            foreach (var tooth in master.Where(t => t.ToothNumber >= 41 && t.ToothNumber <= 48).OrderByDescending(t => t.ToothNumber))
            {
                LowerArch.Add(tooth);
            }

            // 4. Cuadrante 3 (Izquierda paciente -> Derecha pantalla): 31 a 38
            foreach (var tooth in master.Where(t => t.ToothNumber >= 31 && t.ToothNumber <= 38).OrderBy(t => t.ToothNumber))
            {
                LowerArch.Add(tooth);
            }
        }
    }
}
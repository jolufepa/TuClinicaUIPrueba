using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq; // Necesario para OrderBy
using TuClinica.Core.Models;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class OdontogramPreviewViewModel : ObservableObject
    {
        // --- PROPIEDADES CAMBIADAS: De 4 cuadrantes a 2 arcos ---

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
            // Envía mensaje para abrir el odontograma completo
            WeakReferenceMessenger.Default.Send(new OpenOdontogramMessage());
        }

        /// <summary>
        /// Carga los dientes desde el odontograma maestro (PatientFileViewModel)
        /// </summary>
        public void LoadFromMaster(ObservableCollection<ToothViewModel> master)
        {
            // Limpiar arcos
            UpperArch.Clear();
            LowerArch.Clear();

            // --- LÓGICA ACTUALIZADA ---

            // Cuadrante 1 (18 -> 11)
            foreach (var tooth in master.Where(t => t.ToothNumber >= 11 && t.ToothNumber <= 18).OrderByDescending(t => t.ToothNumber))
            {
                UpperArch.Add(tooth);
            }

            // Cuadrante 2 (21 -> 28)
            foreach (var tooth in master.Where(t => t.ToothNumber >= 21 && t.ToothNumber <= 28).OrderBy(t => t.ToothNumber))
            {
                UpperArch.Add(tooth);
            }

            // Cuadrante 4 (48 -> 41)
            foreach (var tooth in master.Where(t => t.ToothNumber >= 41 && t.ToothNumber <= 48).OrderByDescending(t => t.ToothNumber))
            {
                LowerArch.Add(tooth);
            }

            // Cuadrante 3 (31 -> 38)
            foreach (var tooth in master.Where(t => t.ToothNumber >= 31 && t.ToothNumber <= 38).OrderBy(t => t.ToothNumber))
            {
                LowerArch.Add(tooth);
            }
        }
    }
}
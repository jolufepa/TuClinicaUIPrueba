using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class OdontogramPreviewViewModel : ObservableObject
    {
        // Cuadrante 1: 18 → 11
        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _teethQuadrant1 = new();

        // Cuadrante 2: 21 → 28
        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _teethQuadrant2 = new();

        // Cuadrante 3: 38 → 31
        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _teethQuadrant3 = new();

        // Cuadrante 4: 41 → 48
        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _teethQuadrant4 = new();

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
            // Limpiar cuadrantes
            TeethQuadrant1.Clear();
            TeethQuadrant2.Clear();
            TeethQuadrant3.Clear();
            TeethQuadrant4.Clear();

            foreach (var tooth in master)
            {
                if (tooth.ToothNumber >= 11 && tooth.ToothNumber <= 18)
                    TeethQuadrant1.Add(tooth);
                else if (tooth.ToothNumber >= 21 && tooth.ToothNumber <= 28)
                    TeethQuadrant2.Add(tooth);
                else if (tooth.ToothNumber >= 31 && tooth.ToothNumber <= 38)
                    TeethQuadrant3.Add(tooth);
                else if (tooth.ToothNumber >= 41 && tooth.ToothNumber <= 48)
                    TeethQuadrant4.Add(tooth);
            }
        }
    }
}
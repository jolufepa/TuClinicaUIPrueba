using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.Linq;
using TuClinica.Core.Models;
using TuClinica.UI.Messages;

namespace TuClinica.UI.ViewModels
{
    public partial class OdontogramPreviewViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _upperArch = new();

        [ObservableProperty]
        private ObservableCollection<ToothViewModel> _lowerArch = new();

        // Colecciones separadas para poder mover los puentes de abajo junto con los dientes de abajo
        public ObservableCollection<DentalConnector> UpperConnectors { get; } = new();
        public ObservableCollection<DentalConnector> LowerConnectors { get; } = new();

        public IRelayCommand OpenFullOdontogramCommand { get; }

        public OdontogramPreviewViewModel()
        {
            OpenFullOdontogramCommand = new RelayCommand(OpenFull);
        }

        private void OpenFull()
        {
            WeakReferenceMessenger.Default.Send(new OpenOdontogramMessage());
        }

        public void LoadFromMaster(ObservableCollection<ToothViewModel> masterTeeth, ObservableCollection<DentalConnector> masterConnectors)
        {
            UpperArch.Clear();
            LowerArch.Clear();
            UpperConnectors.Clear();
            LowerConnectors.Clear();

            // 1. Cargar Dientes Superiores (11-18, 21-28)
            foreach (var t in masterTeeth.Where(x => (x.ToothNumber >= 11 && x.ToothNumber <= 18) || (x.ToothNumber >= 21 && x.ToothNumber <= 28)).OrderBy(x => x.ToothNumber))
                UpperArch.Add(t);

            // 2. Cargar Dientes Inferiores (31-38, 41-48)
            foreach (var t in masterTeeth.Where(x => (x.ToothNumber >= 31 && x.ToothNumber <= 38) || (x.ToothNumber >= 41 && x.ToothNumber <= 48)).OrderBy(x => x.ToothNumber))
                LowerArch.Add(t);

            // 3. Cargar Conectores
            if (masterConnectors != null)
            {
                foreach (var conn in masterConnectors)
                {
                    if (!conn.ToothSequence.Any()) continue;

                    // Si el primer diente es < 30 (1x o 2x), es superior
                    if (conn.ToothSequence.First() < 30)
                        UpperConnectors.Add(conn);
                    else
                        LowerConnectors.Add(conn);
                }
            }
        }
    }
}
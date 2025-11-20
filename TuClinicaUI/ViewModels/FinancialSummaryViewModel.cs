using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

namespace TuClinica.UI.ViewModels
{
    public partial class FinancialSummaryViewModel : BaseViewModel
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDialogService _dialogService;
        private readonly IPdfService _pdfService;

        [ObservableProperty] private DateTime _startDate = DateTime.Today;
        [ObservableProperty] private DateTime _endDate = DateTime.Today;

        [ObservableProperty] private ObservableCollection<FinancialTransactionDto> _transactions = new();

        [ObservableProperty] private decimal _totalCharges;
        [ObservableProperty] private decimal _totalPayments;
        [ObservableProperty] private decimal _netBalance;

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand PrintReportCommand { get; }
        public IRelayCommand SetTodayCommand { get; }

        public FinancialSummaryViewModel(
            IServiceScopeFactory scopeFactory,
            IDialogService dialogService,
            IPdfService pdfService)
        {
            _scopeFactory = scopeFactory;
            _dialogService = dialogService;
            _pdfService = pdfService;

            SearchCommand = new AsyncRelayCommand(SearchAsync);
            PrintReportCommand = new AsyncRelayCommand(PrintReportAsync);
            SetTodayCommand = new RelayCommand(() => { StartDate = DateTime.Today; EndDate = DateTime.Today; SearchCommand.Execute(null); });

            // Cargar hoy por defecto al iniciar
            _ = SearchAsync();
        }

        private async Task SearchAsync()
        {
            if (StartDate > EndDate)
            {
                _dialogService.ShowMessage("La fecha de inicio no puede ser mayor que la final.", "Fechas incorrectas");
                return;
            }

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var entryRepo = scope.ServiceProvider.GetRequiredService<IClinicalEntryRepository>();
                    var payRepo = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

                    // 1. Obtener datos crudos
                    var entries = await entryRepo.GetByDateRangeAsync(StartDate, EndDate);
                    var payments = await payRepo.GetByDateRangeAsync(StartDate, EndDate);

                    // 2. Unificar en DTOs
                    var list = new List<FinancialTransactionDto>();

                    foreach (var e in entries)
                    {
                        list.Add(new FinancialTransactionDto
                        {
                            Date = e.VisitDate,
                            PatientName = e.Patient?.PatientDisplayInfo ?? "Desconocido",
                            Description = e.Diagnosis ?? "Sin descripción",
                            Type = TransactionType.Cargo,
                            ChargeAmount = e.TotalCost,
                            PaymentAmount = 0
                        });
                    }

                    foreach (var p in payments)
                    {
                        string desc = p.Method ?? "Pago";
                        if (!string.IsNullOrEmpty(p.Observaciones)) desc += $" - {p.Observaciones}";

                        list.Add(new FinancialTransactionDto
                        {
                            Date = p.PaymentDate,
                            PatientName = p.Patient?.PatientDisplayInfo ?? "Desconocido",
                            Description = desc,
                            Type = TransactionType.Abono,
                            ChargeAmount = 0,
                            PaymentAmount = p.Amount
                        });
                    }

                    // 3. Ordenar y actualizar UI
                    Transactions.Clear();
                    foreach (var item in list.OrderBy(x => x.Date))
                    {
                        Transactions.Add(item);
                    }

                    // 4. Calcular totales
                    TotalCharges = list.Sum(x => x.ChargeAmount);
                    TotalPayments = list.Sum(x => x.PaymentAmount);
                    NetBalance = TotalPayments - TotalCharges; // Flujo de caja neto
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar el resumen: {ex.Message}", "Error");
            }
        }

        private async Task PrintReportAsync()
        {
            if (!Transactions.Any())
            {
                _dialogService.ShowMessage("No hay datos para imprimir.", "Aviso");
                return;
            }

            try
            {
                string path = await _pdfService.GenerateFinancialReportPdfAsync(StartDate, EndDate, Transactions.ToList());
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Error al generar PDF: {ex.Message}", "Error");
            }
        }
    }
}
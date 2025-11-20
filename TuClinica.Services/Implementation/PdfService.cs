using iTextSharp.text.pdf;
using iTextSharp.text; // Para BaseFont de iText
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;        // Para SKCanvas, SKPaint, SKColors, SKSvgCanvas
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;
using System.Collections.Generic;
using System.Text.Json;
using TuClinica.Core.Enums;

namespace TuClinica.Services.Implementation
{
    // --- DTOs INTERNOS ---
    internal class OdontogramToothState
    {
        public int ToothNumber { get; set; }
        public ToothCondition FullCondition { get; set; }
        public ToothCondition OclusalCondition { get; set; }
        public ToothCondition MesialCondition { get; set; }
        public ToothCondition DistalCondition { get; set; }
        public ToothCondition VestibularCondition { get; set; }
        public ToothCondition LingualCondition { get; set; }

        public ToothRestoration FullRestoration { get; set; }
        public ToothRestoration OclusalRestoration { get; set; }
        public ToothRestoration MesialRestoration { get; set; }
        public ToothRestoration DistalRestoration { get; set; }
        public ToothRestoration VestibularRestoration { get; set; }
        public ToothRestoration LingualRestoration { get; set; }
    }

    // *** CORRECCIÓN ODONTOGRAMA: Usar ConnectorType en lugar de string ***
    internal class DentalConnectorDto
    {
        public ConnectorType Type { get; set; }
        public List<int> ToothSequence { get; set; } = new();
        public string ColorHex { get; set; }
    }

    internal class OdontogramPersistenceWrapperDto
    {
        public List<OdontogramToothState> Teeth { get; set; } = new();
        public List<DentalConnectorDto> Connectors { get; set; } = new();
    }

    internal class PdfHistoryEvent
    {
        public DateTime Timestamp { get; set; }
        public string Doctor { get; set; } = string.Empty;
        public string Concept { get; set; } = string.Empty;
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
    }

    public class PdfService : IPdfService
    {
        private readonly AppSettings _settings;
        private readonly string _baseBudgetsPath;
        private readonly IPatientRepository _patientRepository;
        private readonly string _basePrescriptionsPath;
        private readonly string _baseOdontogramsPath;
        private readonly string _baseReportsPath; // Nueva ruta

        // Colores Definidos
        private static readonly string ColorTableHeaderBg = "#D9E5F6";
        private static readonly string ColorTableBorder = "#9BC2E6";
        private static readonly string ColorTotalsBg = "#F2F2F2";

        public PdfService(AppSettings settings, string baseBudgetsPath, string basePrescriptionsPath, IPatientRepository patientRepository)
        {
            _settings = settings;
            _baseBudgetsPath = baseBudgetsPath;
            _basePrescriptionsPath = basePrescriptionsPath;
            _baseOdontogramsPath = Path.Combine(GetDataFolderPath(), "odontogramas");
            _baseReportsPath = Path.Combine(GetDataFolderPath(), "reportes"); // Nueva carpeta

            Directory.CreateDirectory(_baseBudgetsPath);
            Directory.CreateDirectory(_basePrescriptionsPath);
            Directory.CreateDirectory(_baseOdontogramsPath);
            Directory.CreateDirectory(_baseReportsPath);

            _patientRepository = patientRepository;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private static string GetDataFolderPath()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            return Path.Combine(appDataFolder, "Data");
        }

        // =================================================================================================
        // 1. GENERACIÓN DE PRESUPUESTO PDF (LÓGICA RESTAURADA COMPLETA)
        // =================================================================================================
        public async Task<string> GenerateBudgetPdfAsync(Budget budget)
        {
            var patient = budget.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(budget.PatientId);
                if (loadedPatient == null) throw new InvalidOperationException($"Paciente ID {budget.PatientId} no encontrado.");
                budget.Patient = loadedPatient;
                patient = loadedPatient;
            }

            string yearFolder = Path.Combine(_baseBudgetsPath, budget.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientNameClean = patient.Name.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string fileName = $"{budget.BudgetNumber}_{patientSurnameClean}_{patientNameClean}.pdf";
            string filePath = Path.Combine(yearFolder, fileName);

            string maskedDoc = MaskDni(patient.DocumentNumber);

            await Task.Run(() =>
            {
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(11).FontFamily(Fonts.Calibri));

                        page.Header().Element(c => ComposeHeader(c, budget, maskedDoc));
                        page.Content().Element(c => ComposeContent(c, budget));
                        page.Footer().Element(c => ComposeFooter(c));
                    });
                })
                .GeneratePdf(filePath);
            });

            return filePath;
        }

        private void ComposeHeader(IContainer container, Budget budget, string maskedDoc)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text(text => text.Span("PRESUPUESTO").Bold().FontSize(18).Underline());

                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem(1).Column(col =>
                    {
                        string logoPath = string.Empty;
                        if (!string.IsNullOrEmpty(_settings.ClinicLogoPath))
                        {
                            logoPath = Path.Combine(AppContext.BaseDirectory, _settings.ClinicLogoPath);
                        }

                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                        {
                            try
                            {
                                var imageContainer = col.Item().PaddingBottom(5).MaxHeight(3.5f, Unit.Centimetre);
                                imageContainer.Image(logoPath);
                            }
                            catch { }
                        }

                        col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental").Bold().FontSize(12));
                        col.Item().Text(text => text.Span($"CIF: {_settings.ClinicCif ?? "N/A"}"));
                        col.Item().Text(text => text.Span(_settings.ClinicAddress ?? "Dirección"));
                        col.Item().Text(text => text.Span($"Tel: {_settings.ClinicPhone ?? "N/A"}"));

                        if (!string.IsNullOrWhiteSpace(_settings.ClinicEmail))
                        {
                            col.Item().Text(text => text.Span(_settings.ClinicEmail).FontColor(Colors.Blue.Medium).Underline());
                        }
                    });

                    row.RelativeItem(1).Column(col =>
                    {
                        col.Item().PaddingTop(3.8f, Unit.Centimetre).Column(patientCol =>
                        {
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text(text => text.Span("Paciente:").FontSize(11).Bold());
                                r.RelativeItem().Text(text => text.Span($"{budget.Patient?.Name} {budget.Patient?.Surname}").FontSize(11));
                            });
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text(text => text.Span("Documento:").FontSize(11).Bold());
                                r.RelativeItem().Text(text => text.Span(maskedDoc).FontSize(11));
                            });
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text(text => text.Span("Teléfono:").FontSize(11).Bold());
                                r.RelativeItem().Text(text => text.Span(budget.Patient?.Phone ?? "N/A").FontSize(11));
                            });
                        });
                    });
                });
            });
        }

        private void ComposeContent(IContainer container, Budget budget)
        {
            container.PaddingVertical(25).Column(col =>
            {
                col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).Row(row =>
                {
                    row.ConstantItem(100).Text(text => text.Span("PRESUPUESTO:").Bold());
                    row.RelativeItem(2).Text(text => text.Span(budget.BudgetNumber));

                    row.ConstantItem(50).Text(text => text.Span("Fecha:").Bold());
                    row.RelativeItem(1).Text(text => text.Span($"{budget.IssueDate:dd/MM/yyyy}"));
                });

                col.Item().PaddingVertical(10).Element(c => ComposeTable(c, budget));
                col.Item().PaddingVertical(10).AlignRight().Element(c => ComposeTotals(c, budget));
            });
        }

        private void ComposeTable(IContainer container, Budget budget)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Descripción"));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Cantidad"));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Precio Unit."));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Total ítem"));
                });

                foreach (var item in budget.Items ?? Enumerable.Empty<BudgetLineItem>())
                {
                    table.Cell().Element(c => BodyCellStyle(c)).Text(text => text.Span(item.Description));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(item.Quantity.ToString()));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span($"{item.UnitPrice:N2} €"));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span($"{item.LineTotal:N2} €"));
                }
            });
        }

        private void ComposeTotals(IContainer container, Budget budget)
        {
            decimal discountAmount = budget.Subtotal * (budget.DiscountPercent / 100);
            decimal baseImponible = budget.Subtotal - discountAmount;
            decimal vatAmount = baseImponible * (budget.VatPercent / 100);

            static IContainer TotalsLabelCell(IContainer c) =>
                c.Background(ColorTotalsBg).Border(1).BorderColor(ColorTableBorder)
                 .PaddingHorizontal(5).PaddingVertical(2).AlignRight();

            static IContainer TotalsValueCell(IContainer c) =>
                c.Background(ColorTotalsBg).Border(1).BorderColor(ColorTableBorder)
                 .PaddingHorizontal(5).PaddingVertical(2).AlignRight();

            container.Width(250, Unit.Point).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                table.Cell().Element(TotalsLabelCell).Text(text => text.Span("Subtotal:").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{budget.Subtotal:N2} €"));

                if (discountAmount > 0)
                {
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Descuento ({budget.DiscountPercent}%):").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"-{discountAmount:N2} €"));
                }

                table.Cell().Element(TotalsLabelCell).Text(text => text.Span("Base Imponible:").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{baseImponible:N2} €"));

                table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"IVA ({budget.VatPercent}%):").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"+{vatAmount:N2} €"));

                var totalLabelCell = table.Cell().Element(TotalsLabelCell);
                totalLabelCell.Text(text => text.Span("Total (Contado):").FontSize(12).Bold());

                var totalValueCell = table.Cell().Element(TotalsValueCell);
                totalValueCell.Text(text => text.Span($"{budget.TotalAmount:N2} €").FontSize(12).Bold());

                if (budget.NumberOfMonths > 0)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(5);

                    decimal monthlyPayment = 0;
                    decimal totalFinanced = 0;

                    if (budget.InterestRate == 0)
                    {
                        monthlyPayment = budget.TotalAmount / budget.NumberOfMonths;
                        totalFinanced = budget.TotalAmount;
                    }
                    else
                    {
                        try
                        {
                            double totalV = (double)budget.TotalAmount;
                            double i = (double)(budget.InterestRate / 100) / 12;
                            int n = budget.NumberOfMonths;
                            double monthlyPaymentDouble = (totalV * i) / (1 - Math.Pow(1 + i, -n));
                            monthlyPayment = (decimal)Math.Round(monthlyPaymentDouble, 2, MidpointRounding.AwayFromZero);
                            totalFinanced = monthlyPayment * n;
                        }
                        catch { }
                    }

                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Plazos:").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{budget.NumberOfMonths} meses"));

                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Cuota Mensual:").FontSize(12).Bold().FontColor(Colors.Blue.Darken2));
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{monthlyPayment:N2} €").FontSize(12).Bold().FontColor(Colors.Blue.Darken2));

                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Total Financiado:").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{totalFinanced:N2} €"));
                }
            });
        }

        private void ComposeFooter(IContainer container)
        {
            string legalText = "De conformidad con lo establecido en el RGPD 2016/679...";
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Column(col =>
            {
                col.Item().PaddingTop(5).Text(text => text.Span(legalText).FontSize(7).FontColor(Colors.Grey.Medium));
                col.Item().PaddingTop(5).AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }

        // =================================================================================================
        // 2. GENERACIÓN DE RECETA PDF (OFICIAL) - RESTAURADA
        // =================================================================================================
        public async Task<string> GeneratePrescriptionPdfAsync(Prescription prescription)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PlantillaReceta.pdf");
            string yearFolder = Path.Combine(_basePrescriptionsPath, prescription.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            var patient = prescription.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(prescription.PatientId);
                if (loadedPatient == null) throw new InvalidOperationException("Datos de paciente no encontrados.");
                prescription.Patient = loadedPatient;
                patient = loadedPatient;
            }
            if (!prescription.Items.Any()) throw new InvalidOperationException("Receta sin items.");
            var firstItem = prescription.Items.First();

            string patientNameClean = patient!.Name.Replace(' ', '_').Replace(".", "");
            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "");
            string patientDocClean = patient.DocumentNumber.Replace(' ', '_').Replace(".", "");
            string comprehensiveIdentifier = $"{patientSurnameClean}_{patientNameClean}_{patientDocClean}";

            string fileNameSuffix = prescription.Id > 0 ? prescription.Id.ToString() : prescription.IssueDate.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Receta_{comprehensiveIdentifier}_{fileNameSuffix}.pdf";
            string outputPath = Path.Combine(yearFolder, fileName);

            if (!File.Exists(templatePath)) throw new FileNotFoundException("Plantilla no encontrada.", templatePath);

            int diasTratamiento = firstItem.DurationInDays ?? 1;
            int unidadesPorToma = 1;
            int tomasAlDia = 1;
            int unidadesPorEnvase = 30;

            if (!string.IsNullOrWhiteSpace(firstItem.Quantity))
            {
                var match = System.Text.RegularExpressions.Regex.Match(firstItem.Quantity, @"\d+");
                if (match.Success) int.TryParse(match.Value, out unidadesPorToma);
            }
            if (!string.IsNullOrWhiteSpace(firstItem.DosagePauta))
            {
                var match = System.Text.RegularExpressions.Regex.Match(firstItem.DosagePauta.ToLower(), @"cada\s+(\d+)\s*(horas?|hs?)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int hours) && hours > 0) tomasAlDia = 24 / hours;
            }

            int totalUnits = diasTratamiento * unidadesPorToma * tomasAlDia;
            int numEnvases = (int)Math.Ceiling((double)totalUnits / unidadesPorEnvase);
            if (numEnvases == 0) numEnvases = 1;

            string medicFull = firstItem.MedicationName ?? "";

            await Task.Run(() =>
            {
                PdfReader reader = null;
                PdfStamper stamper = null;
                FileStream fs = null;
                try
                {
                    reader = new PdfReader(templatePath);
                    fs = new FileStream(outputPath, FileMode.Create);
                    stamper = new PdfStamper(reader, fs);
                    AcroFields form = stamper.AcroFields;
                    BaseFont font = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

                    form.SetFieldProperty("Medic", "textfont", font, null);
                    form.SetFieldProperty("Medic", "textsize", 8f, null);

                    string fecha = prescription.IssueDate.ToString("dd/MM/yyyy");
                    string nombre = $"{patient.Name} {patient.Surname}";

                    form.SetField("CIF", _settings.ClinicCif ?? "");
                    form.SetField("NombrePac", nombre);
                    form.SetField("DNIPac", patient.DocumentNumber);
                    form.SetField("NombrePacCop", nombre);
                    form.SetField("DNIPacCop", patient.DocumentNumber);

                    form.SetField("Unidades", totalUnits.ToString());
                    form.SetField("Pauta", firstItem.DosagePauta ?? "");
                    form.SetField("Fecha", fecha);
                    form.SetField("NumEnv", numEnvases.ToString());
                    form.SetField("DurTrat", diasTratamiento.ToString());
                    form.SetField("Medic", medicFull);
                    form.SetField("MedicamentoNombre", firstItem.MedicationName ?? "");
                    form.SetField("Fecha_af_date", fecha);

                    form.SetField("UnidadesCop", totalUnits.ToString());
                    form.SetField("PautaCop", firstItem.DosagePauta ?? "");
                    form.SetField("FechaCop", fecha);
                    form.SetField("NumEnvCop", numEnvases.ToString());
                    form.SetField("DurTratCop", diasTratamiento.ToString());
                    form.SetField("MedicCop", medicFull);
                    form.SetField("MedicamentoNombreCop", firstItem.MedicationName ?? "");
                    form.SetField("Fecha_Cop_af_date", fecha);

                    form.SetField("Indicaciones", prescription.Instructions ?? "");
                    form.SetField("PrescriptorNombre", prescription.PrescriptorName ?? "");
                    form.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");
                    form.SetField("Num. Col.", prescription.PrescriptorCollegeNum ?? "");
                    form.SetField("Especialidad", prescription.PrescriptorSpecialty ?? "");

                    form.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");
                    form.SetField("Num. Col.Cop", prescription.PrescriptorCollegeNum ?? "");
                    form.SetField("EspecialidadCop", prescription.PrescriptorSpecialty ?? "");

                    stamper.FormFlattening = true;
                }
                finally { stamper?.Close(); fs?.Dispose(); reader?.Close(); }
            });
            return outputPath;
        }

        // =================================================================================================
        // 3. GENERACIÓN DE RECETA PDF (BÁSICA) - RESTAURADA
        // =================================================================================================
        public async Task<string> GenerateBasicPrescriptionPdfAsync(Prescription prescription)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PlantillaRecetaBasica.pdf");
            string yearFolder = Path.Combine(_basePrescriptionsPath, prescription.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);
            var patient = prescription.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(prescription.PatientId);
                if (loadedPatient == null) throw new InvalidOperationException("Datos de paciente no encontrados.");
                prescription.Patient = loadedPatient;
                patient = loadedPatient;
            }
            if (!prescription.Items.Any()) throw new InvalidOperationException("Receta sin items.");
            var firstItem = prescription.Items.First();

            string patientNameClean = patient!.Name.Replace(' ', '_').Replace(".", "");
            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "");
            string patientDocClean = patient.DocumentNumber.Replace(' ', '_').Replace(".", "");
            string comprehensiveIdentifier = $"{patientSurnameClean}_{patientNameClean}_{patientDocClean}";

            string fileNameSuffix = prescription.Id > 0 ? prescription.Id.ToString() : prescription.IssueDate.ToString("yyyyMMdd_HHmmss");
            string fileName = $"RecetaBasica_{comprehensiveIdentifier}_{fileNameSuffix}.pdf";
            string outputPath = Path.Combine(yearFolder, fileName);

            if (!File.Exists(templatePath)) throw new FileNotFoundException("Plantilla Básica no encontrada.", templatePath);

            int diasTratamiento = firstItem.DurationInDays ?? 1;
            int unidadesPorToma = 1;
            int tomasAlDia = 1;
            int unidadesPorEnvase = 30;
            if (!string.IsNullOrWhiteSpace(firstItem.Quantity))
            {
                var match = System.Text.RegularExpressions.Regex.Match(firstItem.Quantity, @"\d+");
                if (match.Success) int.TryParse(match.Value, out unidadesPorToma);
            }
            if (!string.IsNullOrWhiteSpace(firstItem.DosagePauta))
            {
                var match = System.Text.RegularExpressions.Regex.Match(firstItem.DosagePauta.ToLower(), @"cada\s+(\d+)\s*(horas?|hs?)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int hours) && hours > 0) tomasAlDia = 24 / hours;
            }
            int totalUnits = diasTratamiento * unidadesPorToma * tomasAlDia;
            int numEnvases = (int)Math.Ceiling((double)totalUnits / unidadesPorEnvase);
            if (numEnvases == 0) numEnvases = 1;

            string medicFull = firstItem.MedicationName ?? "";

            await Task.Run(() =>
            {
                PdfReader reader = null;
                PdfStamper stamper = null;
                FileStream fs = null;
                try
                {
                    reader = new PdfReader(templatePath);
                    fs = new FileStream(outputPath, FileMode.Create);
                    stamper = new PdfStamper(reader, fs);
                    AcroFields form = stamper.AcroFields;

                    string fechaFormato = prescription.IssueDate.ToString("dd/MM/yyyy");
                    string nombre = $"{patient.Name} {patient.Surname}";

                    form.SetField("CIF", _settings.ClinicCif ?? "");
                    form.SetField("NombrePac", nombre);
                    form.SetField("DNIPac", patient.DocumentNumber);
                    form.SetField("NombrePacCop", nombre);
                    form.SetField("DNIPacCop", patient.DocumentNumber);
                    form.SetField("Unidades", totalUnits.ToString());
                    form.SetField("Pauta", firstItem.DosagePauta ?? "");
                    form.SetField("Fecha", fechaFormato);
                    form.SetField("NumEnv", numEnvases.ToString());
                    form.SetField("DurTrat", diasTratamiento.ToString());
                    form.SetField("Medic", medicFull);
                    form.SetField("MedicamentoNombre", firstItem.MedicationName ?? "");
                    form.SetField("Fecha_af_date", fechaFormato);

                    form.SetField("UnidadesCop", totalUnits.ToString());
                    form.SetField("PautaCop", firstItem.DosagePauta ?? "");
                    form.SetField("FechaCop", fechaFormato);
                    form.SetField("NumEnvCop", numEnvases.ToString());
                    form.SetField("DurTratCop", diasTratamiento.ToString());
                    form.SetField("MedicCop", firstItem.MedicationName);
                    form.SetField("MedicamentoNombreCop", firstItem.MedicationName ?? "");
                    form.SetField("Fecha_Cop_af_date", fechaFormato);

                    form.SetField("Indicaciones", prescription.Instructions ?? "");
                    form.SetField("PrescriptorNombre", prescription.PrescriptorName ?? "");
                    form.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");
                    form.SetField("Num. Col.", prescription.PrescriptorCollegeNum ?? "");
                    form.SetField("Especialidad", prescription.PrescriptorSpecialty ?? "");

                    form.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");
                    form.SetField("Num. Col.Cop", prescription.PrescriptorCollegeNum ?? "");
                    form.SetField("EspecialidadCop", prescription.PrescriptorSpecialty ?? "");

                    stamper.FormFlattening = true;
                }
                finally { stamper?.Close(); fs?.Dispose(); reader?.Close(); }
            });
            return outputPath;
        }

        // =================================================================================================
        // 4. GENERACIÓN DE ODONTOGRAMA PDF (CORREGIDO CON ENUM)
        // =================================================================================================
        public async Task<string> GenerateOdontogramPdfAsync(Patient patient, string odontogramJsonState)
        {
            string yearFolder = Path.Combine(_baseOdontogramsPath, DateTime.Now.Year.ToString());
            Directory.CreateDirectory(yearFolder);
            string fileName = $"Odontograma_{patient.Surname}_{patient.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(yearFolder, fileName);

            List<OdontogramToothState> teeth = new List<OdontogramToothState>();

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // INTENTAR DESERIALIZAR WRAPPER
                // Ahora DentalConnectorDto usa el Enum correcto, por lo que no fallará.
                try
                {
                    var wrapper = JsonSerializer.Deserialize<OdontogramPersistenceWrapperDto>(odontogramJsonState, options);
                    if (wrapper != null && wrapper.Teeth != null)
                    {
                        teeth = wrapper.Teeth;
                    }
                    else
                    {
                        // Fallback: Lista antigua
                        teeth = JsonSerializer.Deserialize<List<OdontogramToothState>>(odontogramJsonState, options) ?? new List<OdontogramToothState>();
                    }
                }
                catch
                {
                    // Si falla el wrapper, intentamos la lista directa (formato antiguo)
                    teeth = JsonSerializer.Deserialize<List<OdontogramToothState>>(odontogramJsonState, options) ?? new List<OdontogramToothState>();
                }
            }
            catch
            {
                teeth = new List<OdontogramToothState>();
            }

            await Task.Run(() =>
            {
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(1.0f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));

                        page.Header().Column(col =>
                        {
                            col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental").Bold().FontSize(14));
                            col.Item().Text(text => text.Span($"Odontograma de: {patient.PatientDisplayInfo}").FontSize(16).Bold());
                            col.Item().Text(text => text.Span($"Fecha de Emisión: {DateTime.Now:dd/MM/yyyy HH:mm}"));
                            col.Item().PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                        });

                        page.Content().PaddingTop(20).Column(col =>
                        {
                            col.Item().Element(c => ComposeOdontogramGraphic(c, teeth));
                            col.Item().PaddingTop(30).Element(c => ComposeOdontogramLegend(c));
                        });

                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("Página ");
                            text.CurrentPageNumber();
                        });
                    });
                })
                .GeneratePdf(filePath);
            });

            return filePath;
        }

        private void ComposeOdontogramGraphic(IContainer container, List<OdontogramToothState> teeth)
        {
            container.Column(mainCol =>
            {
                // Superior
                mainCol.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Row(r =>
                    {
                        // Cuadrante 1 (18-11) -> isUpper = true
                        for (int i = 18; i >= 11; i--) AddGeometricTooth(r, GetTooth(teeth, i), true);
                        r.ConstantItem(15);
                        // Cuadrante 2 (21-28) -> isUpper = true
                        for (int i = 21; i <= 28; i++) AddGeometricTooth(r, GetTooth(teeth, i), true);
                    });
                });

                mainCol.Item().Height(30);

                // Inferior
                mainCol.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Row(r =>
                    {
                        // Cuadrante 4 (48-41) -> isUpper = false
                        for (int i = 48; i >= 41; i--) AddGeometricTooth(r, GetTooth(teeth, i), false);
                        r.ConstantItem(15);
                        // Cuadrante 3 (31-38) -> isUpper = false
                        for (int i = 31; i <= 38; i++) AddGeometricTooth(r, GetTooth(teeth, i), false);
                    });
                });
            });
        }

        private OdontogramToothState GetTooth(List<OdontogramToothState> teeth, int number)
        {
            // Si la lista está vacía o no se encuentra, devolvemos uno nuevo "Sano" (blanco)
            return teeth.FirstOrDefault(t => t.ToothNumber == number) ?? new OdontogramToothState { ToothNumber = number };
        }

        private void AddGeometricTooth(RowDescriptor row, OdontogramToothState tooth, bool isUpper)
        {
            float size = 35f;

            row.AutoItem().Padding(2).Column(col =>
            {
                if (isUpper) col.Item().AlignCenter().Text(tooth.ToothNumber.ToString()).FontSize(9).Bold();

                string svgContent = GenerateToothSvg(size, size, tooth, isUpper);
                col.Item().Width(size).Height(size).Svg(svgContent);

                if (!isUpper) col.Item().AlignCenter().Text(tooth.ToothNumber.ToString()).FontSize(9).Bold();
            });
        }

        private string GenerateToothSvg(float width, float height, OdontogramToothState tooth, bool isUpper)
        {
            using var stream = new MemoryStream();
            using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
            {
                DrawSchematicTooth(canvas, width, height, tooth, isUpper);
            }
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private void DrawSchematicTooth(SKCanvas canvas, float w, float h, OdontogramToothState tooth, bool isUpper)
        {
            float cx = w / 2;
            float cy = h / 2;
            float radius = w / 3.5f;

            using var paintStroke = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1, IsAntialias = true };

            SKPaint GetPaint(ToothCondition c, ToothRestoration r)
            {
                SKColor color = SKColors.White;
                // Prioridad Restauraciones
                if (r == ToothRestoration.Obturacion) color = SKColor.Parse("#3498DB"); // Azul
                else if (r == ToothRestoration.Sellador) color = SKColor.Parse("#2ECC71"); // Verde
                else if (r == ToothRestoration.ProtesisFija) color = SKColor.Parse("#C0392B"); // Rojo oscuro
                else if (r == ToothRestoration.Corona) color = SKColor.Parse("#F1C40F"); // Dorado
                // Prioridad Condiciones
                else if (c == ToothCondition.Caries) color = SKColor.Parse("#E74C3C"); // Rojo
                else if (c == ToothCondition.Fractura) color = SKColor.Parse("#F1C40F"); // Amarillo/Naranja

                // Importante: IsStroke = false para rellenar
                return new SKPaint { Color = color, IsStroke = false, IsAntialias = true };
            }

            // Geometría básica (Cuadrado con trapecios)
            float m = 1;
            var tl = new SKPoint(m, m); var tr = new SKPoint(w - m, m);
            var bl = new SKPoint(m, h - m); var br = new SKPoint(w - m, h - m);
            float off = radius * 0.7f;
            var c_tl = new SKPoint(cx - off, cy - off); var c_tr = new SKPoint(cx + off, cy - off);
            var c_bl = new SKPoint(cx - off, cy + off); var c_br = new SKPoint(cx + off, cy + off);

            using var pathTop = new SKPath(); pathTop.MoveTo(tl); pathTop.LineTo(tr); pathTop.LineTo(c_tr); pathTop.LineTo(c_tl); pathTop.Close();
            using var pathBot = new SKPath(); pathBot.MoveTo(bl); pathBot.LineTo(br); pathBot.LineTo(c_br); pathBot.LineTo(c_bl); pathBot.Close();
            using var pathLeft = new SKPath(); pathLeft.MoveTo(tl); pathLeft.LineTo(bl); pathLeft.LineTo(c_bl); pathLeft.LineTo(c_tl); pathLeft.Close();
            using var pathRight = new SKPath(); pathRight.MoveTo(tr); pathRight.LineTo(br); pathRight.LineTo(c_br); pathRight.LineTo(c_tr); pathRight.Close();

            // Mapeo de caras
            var condTop = isUpper ? tooth.VestibularCondition : tooth.LingualCondition;
            var restTop = isUpper ? tooth.VestibularRestoration : tooth.LingualRestoration;

            var condBot = isUpper ? tooth.LingualCondition : tooth.VestibularCondition;
            var restBot = isUpper ? tooth.LingualRestoration : tooth.VestibularRestoration;

            bool isRightQuadrant = (tooth.ToothNumber >= 11 && tooth.ToothNumber <= 18) || (tooth.ToothNumber >= 41 && tooth.ToothNumber <= 48);
            var condLeft = isRightQuadrant ? tooth.DistalCondition : tooth.MesialCondition;
            var restLeft = isRightQuadrant ? tooth.DistalRestoration : tooth.MesialRestoration;
            var condRight = isRightQuadrant ? tooth.MesialCondition : tooth.DistalCondition;
            var restRight = isRightQuadrant ? tooth.MesialRestoration : tooth.DistalRestoration;

            // Dibujar Rellenos y Bordes
            canvas.DrawPath(pathTop, GetPaint(condTop, restTop)); canvas.DrawPath(pathTop, paintStroke);
            canvas.DrawPath(pathBot, GetPaint(condBot, restBot)); canvas.DrawPath(pathBot, paintStroke);
            canvas.DrawPath(pathLeft, GetPaint(condLeft, restLeft)); canvas.DrawPath(pathLeft, paintStroke);
            canvas.DrawPath(pathRight, GetPaint(condRight, restRight)); canvas.DrawPath(pathRight, paintStroke);

            // Centro (Oclusal)
            using var paintCenter = GetPaint(tooth.OclusalCondition, tooth.OclusalRestoration);
            canvas.DrawCircle(cx, cy, radius, paintCenter);
            canvas.DrawCircle(cx, cy, radius, paintStroke);

            // Restauraciones Completas
            if (IsFullRestoration(tooth.FullRestoration))
            {
                using var paintFull = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
                if (tooth.FullRestoration == ToothRestoration.Corona) paintFull.Color = SKColor.Parse("#F1C40F");
                else if (tooth.FullRestoration == ToothRestoration.Endodoncia) paintFull.Color = SKColor.Parse("#9B59B6");
                else if (tooth.FullRestoration == ToothRestoration.Implante) paintFull.Color = SKColor.Parse("#808080");
                else paintFull.Color = SKColors.Gray;

                // Dibujar círculo grande sobre todo
                canvas.DrawCircle(cx, cy, w / 2 - 2, paintFull);
            }

            // Extracción (X roja)
            if (tooth.FullCondition == ToothCondition.ExtraccionIndicada)
            {
                using var paintEx = new SKPaint { Color = SKColor.Parse("#E74C3C"), IsStroke = true, StrokeWidth = 2.5f, IsAntialias = true };
                canvas.DrawLine(0, 0, w, h, paintEx);
                canvas.DrawLine(w, 0, 0, h, paintEx);
            }

            // Ausente (X negra)
            if (tooth.FullCondition == ToothCondition.Ausente)
            {
                using var paintAbsent = new SKPaint { Color = SKColor.Parse("#333333"), IsStroke = true, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawLine(0, 0, w, h, paintAbsent);
                canvas.DrawLine(w, 0, 0, h, paintAbsent);
            }
        }

        private bool IsFullRestoration(ToothRestoration r)
        {
            return r == ToothRestoration.Corona || r == ToothRestoration.Implante || r == ToothRestoration.Endodoncia || r == ToothRestoration.ProtesisFija;
        }

        private void ComposeOdontogramLegend(IContainer container)
        {
            container.AlignCenter().Row(row =>
            {
                row.Spacing(15);
                void AddItem(string text, string type)
                {
                    row.AutoItem().Row(r =>
                    {
                        string svgContent = GenerateLegendItemSvg(14, 14, type);
                        r.AutoItem().Width(14).Height(14).Svg(svgContent);
                        r.AutoItem().PaddingLeft(4).Text(text).FontSize(9);
                    });
                }
                AddItem("Sano", "white");
                AddItem("Caries", "red");
                AddItem("Obturación", "blue");
                AddItem("Corona", "gold");
                AddItem("Ausente", "x_black");
                AddItem("Extracción", "x_red");
                AddItem("Endodoncia", "purple");
                AddItem("Implante", "grey");
                // AddItem("Puente", "line_blue"); // Opcional si no se dibujan
            });
        }

        private string GenerateLegendItemSvg(float width, float height, string type)
        {
            using var stream = new MemoryStream();
            using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
            {
                var stroke = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1 };

                if (type == "x_red" || type == "x_black")
                {
                    var color = type == "x_red" ? SKColor.Parse("#E74C3C") : SKColor.Parse("#333333");
                    var paintX = new SKPaint { Color = color, IsStroke = true, StrokeWidth = 2 };
                    canvas.DrawLine(0, 0, width, height, paintX);
                    canvas.DrawLine(width, 0, 0, height, paintX);
                }
                else
                {
                    SKColor fill = SKColors.White;
                    if (type == "red") fill = SKColor.Parse("#E74C3C");
                    else if (type == "blue") fill = SKColor.Parse("#3498DB");
                    else if (type == "gold") fill = SKColor.Parse("#F1C40F");
                    else if (type == "purple") fill = SKColor.Parse("#9B59B6");
                    else if (type == "grey") fill = SKColor.Parse("#808080");

                    var paintFill = new SKPaint { Color = fill, Style = SKPaintStyle.Fill };
                    canvas.DrawRect(0, 0, width, height, paintFill);
                    canvas.DrawRect(0, 0, width, height, stroke);
                }
            }
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // =================================================================================================
        // 5. GENERACIÓN DE HISTORIAL PDF (LÓGICA RESTAURADA)
        // =================================================================================================
        public async Task<byte[]> GenerateHistoryPdfAsync(Patient patient, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance)
        {
            return await Task.Run(() =>
            {
                return QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));
                        page.Header().Element(c => ComposeHistoryHeader(c, patient));
                        page.Content().Element(c => ComposeHistoryContent(c, entries, payments, totalBalance));
                        page.Footer().Element(c => ComposeHistoryFooter(c));
                    });
                }).GeneratePdf();
            });
        }

        private void ComposeHistoryHeader(IContainer container, Patient patient)
        {
            container.Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem(1).Column(col =>
                    {
                        string logoPath = string.Empty;
                        if (!string.IsNullOrEmpty(_settings.ClinicLogoPath))
                        {
                            logoPath = Path.Combine(AppContext.BaseDirectory, _settings.ClinicLogoPath);
                        }
                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                        {
                            try
                            {
                                var imageContainer = col.Item().PaddingBottom(5).MaxHeight(2.5f, Unit.Centimetre);
                                imageContainer.Image(logoPath);
                            }
                            catch { }
                        }
                        col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental").Bold().FontSize(12));
                        col.Item().Text(text => text.Span($"CIF: {_settings.ClinicCif ?? "N/A"}"));
                        col.Item().Text(text => text.Span(_settings.ClinicAddress ?? "Dirección"));
                        col.Item().Text(text => text.Span($"Tel: {_settings.ClinicPhone ?? "N/A"}"));
                    });
                    row.RelativeItem(1).Column(col =>
                    {
                        col.Item().AlignRight().Text(text => text.Span("Historial de Cuenta").Bold().FontSize(16));
                        col.Item().AlignRight().Text(text => text.Span($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}"));
                    });
                });
                column.Item().PaddingTop(20).Column(col =>
                {
                    col.Item().Text(text => text.Span("Paciente:").FontSize(12));
                    col.Item().Text(text => text.Span($"{patient.Name} {patient.Surname}").Bold().FontSize(14));
                    col.Item().Text(text => text.Span($"Documento: {patient.DocumentNumber} ({patient.DocumentType})"));
                });
                column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
            });
        }

        private void ComposeHistoryContent(IContainer container, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance)
        {
            var unifiedHistory = new List<PdfHistoryEvent>();
            foreach (var entry in entries)
            {
                unifiedHistory.Add(new PdfHistoryEvent
                {
                    Timestamp = entry.VisitDate,
                    Doctor = entry.DoctorName,
                    Concept = entry.Diagnosis ?? "N/A",
                    Debe = entry.TotalCost,
                    Haber = 0
                });
            }
            foreach (var payment in payments)
            {
                string concept = $"Abono (Método: {payment.Method ?? "N/A"})";
                if (!string.IsNullOrWhiteSpace(payment.Observaciones)) concept += $" - {payment.Observaciones}";
                unifiedHistory.Add(new PdfHistoryEvent
                {
                    Timestamp = payment.PaymentDate,
                    Doctor = "",
                    Concept = concept,
                    Debe = 0,
                    Haber = payment.Amount
                });
            }
            var sortedHistory = unifiedHistory.OrderBy(e => e.Timestamp).ToList();

            container.PaddingVertical(15).Column(col =>
            {
                col.Spacing(20);
                col.Item().Column(tableCol =>
                {
                    tableCol.Item().PaddingBottom(5).Text(text => text.Span("Historial de Cuenta").Bold().FontSize(14));
                    tableCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(65);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Fecha"));
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Doctor"));
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Concepto/Observaciones"));
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text(text => text.Span("Debe"));
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text(text => text.Span("Haber"));
                        });
                        foreach (var item in sortedHistory)
                        {
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span($"{item.Timestamp:dd/MM/yy}"));
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span(item.Doctor));
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span(item.Concept));
                            string debeText = item.Debe > 0 ? $"{item.Debe:C}" : "";
                            table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(debeText).FontColor(Colors.Red.Medium));
                            string haberText = item.Haber > 0 ? $"{item.Haber:C}" : "";
                            table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(haberText).FontColor(Colors.Green.Medium));
                        }
                    });
                });
                col.Item().AlignRight().Width(250, Unit.Point).Column(totalCol =>
                {
                    totalCol.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5);
                    totalCol.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text => text.Span("Total Cargado:").Bold());
                        row.RelativeItem().AlignRight().Text(text => text.Span($"{entries.Sum(e => e.TotalCost):C}"));
                    });
                    totalCol.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text => text.Span("Total Abonado:").Bold());
                        row.RelativeItem().AlignRight().Text(text => text.Span($"{payments.Sum(p => p.Amount):C}"));
                    });
                    totalCol.Item().Row(row =>
                    {
                        row.Spacing(10);
                        var (color, label) = totalBalance > 0 ? (Colors.Red.Medium, "SALDO PENDIENTE:") : (Colors.Green.Medium, "SALDO A FAVOR:");
                        if (totalBalance == 0) (color, label) = (Colors.Black, "SALDO TOTAL:");
                        row.RelativeItem().Text(text => text.Span(label).FontColor(color).Bold().FontSize(14));
                        row.RelativeItem().AlignRight().Text(text => text.Span($"{totalBalance:C}").FontColor(color).Bold().FontSize(14));
                    });
                });
            });
        }

        private void ComposeHistoryFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Column(col =>
            {
                col.Item().PaddingTop(5).AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }

        // =================================================================================================
        // 6. GENERACIÓN DE REPORTE FINANCIERO (NUEVO)
        // =================================================================================================
        public async Task<string> GenerateFinancialReportPdfAsync(DateTime startDate, DateTime endDate, List<FinancialTransactionDto> transactions)
        {
            string fileName = $"Reporte_Economico_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf";
            string filePath = Path.Combine(_baseReportsPath, fileName);

            decimal totalCharges = transactions.Sum(t => t.ChargeAmount);
            decimal totalPayments = transactions.Sum(t => t.PaymentAmount);
            decimal balance = totalPayments - totalCharges; // Balance del periodo (Caja - Producción)

            await Task.Run(() =>
            {
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));

                        page.Header().Element(c => ComposeFinancialHeader(c, startDate, endDate));
                        page.Content().Element(c => ComposeFinancialContent(c, transactions, totalCharges, totalPayments, balance));
                        page.Footer().Element(c => ComposeFooter(c));
                    });
                })
                .GeneratePdf(filePath);
            });

            return filePath;
        }

        private void ComposeFinancialHeader(IContainer container, DateTime start, DateTime end)
        {
            container.Column(col =>
            {
                // Logo y Datos Clínica
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        string logoPath = string.Empty;
                        if (!string.IsNullOrEmpty(_settings.ClinicLogoPath))
                            logoPath = Path.Combine(AppContext.BaseDirectory, _settings.ClinicLogoPath);

                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                        {
                            try { c.Item().MaxHeight(2.0f, Unit.Centimetre).Image(logoPath); } catch { }
                        }
                        c.Item().Text(_settings.ClinicName).Bold();
                    });

                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text("REPORTE ECONÓMICO").FontSize(16).Bold();
                        c.Item().Text($"Desde: {start:dd/MM/yyyy}");
                        c.Item().Text($"Hasta: {end:dd/MM/yyyy}");
                    });
                });

                col.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);
            });
        }

        private void ComposeFinancialContent(IContainer container, List<FinancialTransactionDto> transactions, decimal totalCharges, decimal totalPayments, decimal balance)
        {
            container.PaddingVertical(10).Column(col =>
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80); // Fecha
                        columns.RelativeColumn(2);  // Paciente
                        columns.RelativeColumn(3);  // Detalle
                        columns.RelativeColumn(1);  // Cargo
                        columns.RelativeColumn(1);  // Abono
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCellStyle).Text("Fecha/Hora");
                        header.Cell().Element(HeaderCellStyle).Text("Paciente");
                        header.Cell().Element(HeaderCellStyle).Text("Detalle");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Cargo");
                        header.Cell().Element(HeaderCellStyle).AlignRight().Text("Abono");
                    });

                    foreach (var t in transactions)
                    {
                        table.Cell().Element(BodyCellStyle).Text($"{t.Date:dd/MM HH:mm}");
                        table.Cell().Element(BodyCellStyle).Text(t.PatientName);
                        table.Cell().Element(BodyCellStyle).Text(t.Description);

                        // Lógica visual de "- €"
                        string chargeText = t.ChargeAmount == 0 ? "- €" : $"{t.ChargeAmount:N2} €";
                        string payText = t.PaymentAmount == 0 ? "- €" : $"{t.PaymentAmount:N2} €";

                        // Colores
                        var chargeColor = t.ChargeAmount > 0 ? Colors.Red.Medium : Colors.Grey.Medium;
                        var payColor = t.PaymentAmount > 0 ? Colors.Green.Medium : Colors.Grey.Medium;

                        table.Cell().Element(c => BodyCellStyle(c, true)).Text(chargeText).FontColor(chargeColor);
                        table.Cell().Element(c => BodyCellStyle(c, true)).Text(payText).FontColor(payColor);
                    }
                });

                // Totales al final
                col.Item().PaddingTop(10).AlignRight().Width(300).Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    t.Cell().Element(HeaderCellStyle).Text("Total Cargos (Producción):");
                    t.Cell().Element(c => BodyCellStyle(c, true)).Text($"{totalCharges:C}").Bold();

                    t.Cell().Element(HeaderCellStyle).Text("Total Abonos (Caja):");
                    t.Cell().Element(c => BodyCellStyle(c, true)).Text($"{totalPayments:C}").Bold().FontColor(Colors.Green.Darken1);
                });
            });
        }

        // --- HELPERS DE ESTILO ---
        private IContainer HeaderCellStyle(IContainer container)
        {
            return container.Border(1).BorderColor(ColorTableBorder).Background(ColorTableHeaderBg)
                .PaddingVertical(5).PaddingHorizontal(5).AlignCenter();
        }

        private IContainer BodyCellStyle(IContainer container)
        {
            return container.Border(1).BorderColor(ColorTableBorder)
                .PaddingVertical(5).PaddingHorizontal(5).AlignLeft();
        }

        private IContainer BodyCellStyle(IContainer container, bool alignRight)
        {
            var style = BodyCellStyle(container);
            return alignRight ? style.AlignRight() : style;
        }

        private string MaskDni(string? dniNie)
        {
            if (string.IsNullOrWhiteSpace(dniNie) || dniNie.Length < 5) return dniNie ?? string.Empty;
            int startAsterisk = Math.Max(1, dniNie.Length - 4);
            int countAsterisk = Math.Max(0, dniNie.Length - startAsterisk - 1);
            if (countAsterisk == 0) return dniNie;
            countAsterisk = Math.Min(countAsterisk, 4);
            startAsterisk = dniNie.Length - countAsterisk - 1;
            return dniNie.Substring(0, startAsterisk) + new string('*', countAsterisk) + dniNie.Substring(startAsterisk + countAsterisk);
        }
    }
}
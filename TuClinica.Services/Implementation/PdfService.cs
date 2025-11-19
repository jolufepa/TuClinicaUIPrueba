using iTextSharp.text.pdf;
using iTextSharp.text; // Para BaseFont de iText
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
// using QuestPDF.Drawing; // YA NO ES NECESARIO EN 2024.3+
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

            Directory.CreateDirectory(_baseBudgetsPath);
            Directory.CreateDirectory(_basePrescriptionsPath);
            Directory.CreateDirectory(_baseOdontogramsPath);

            _patientRepository = patientRepository;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private static string GetDataFolderPath()
        {
            string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TuClinicaPD");
            return Path.Combine(appDataFolder, "Data");
        }

        // =================================================================================================
        // 1. GENERACIÓN DE PRESUPUESTO PDF
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
        // 2. GENERACIÓN DE RECETA PDF (OFICIAL)
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
        // 3. GENERACIÓN DE RECETA PDF (BÁSICA)
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

                    string fechaFormato = prescription.IssueDate.ToString("dd/MM/yyyy"); // **CORREGIDO AQUÍ**
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
                    form.SetField("Fecha_af_date", fechaFormato); // **USANDO LA VARIABLE CORRECTA**

                    form.SetField("UnidadesCop", totalUnits.ToString());
                    form.SetField("PautaCop", firstItem.DosagePauta ?? "");
                    form.SetField("FechaCop", fechaFormato);
                    form.SetField("NumEnvCop", numEnvases.ToString());
                    form.SetField("DurTratCop", diasTratamiento.ToString());
                    form.SetField("MedicCop", firstItem.MedicationName);
                    form.SetField("MedicamentoNombreCop", firstItem.MedicationName ?? "");
                    form.SetField("Fecha_Cop_af_date", fechaFormato); // **USANDO LA VARIABLE CORRECTA**

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
        // 4. GENERACIÓN DE ODONTOGRAMA PDF (VECTORIAL PROFESIONAL)
        // =================================================================================================
        public async Task<string> GenerateOdontogramPdfAsync(Patient patient, string odontogramJsonState)
        {
            string yearFolder = Path.Combine(_baseOdontogramsPath, DateTime.Now.Year.ToString());
            Directory.CreateDirectory(yearFolder);
            string fileName = $"Odontograma_{patient.Surname}_{patient.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            string filePath = Path.Combine(yearFolder, fileName);

            List<OdontogramToothState> teeth;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                teeth = JsonSerializer.Deserialize<List<OdontogramToothState>>(odontogramJsonState, options)
                        ?? new List<OdontogramToothState>();
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
                mainCol.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Row(r =>
                    {
                        for (int i = 18; i >= 11; i--) AddGeometricTooth(r, teeth.FirstOrDefault(t => t.ToothNumber == i) ?? new OdontogramToothState { ToothNumber = i }, true);
                        r.ConstantItem(15);
                        for (int i = 21; i <= 28; i++) AddGeometricTooth(r, teeth.FirstOrDefault(t => t.ToothNumber == i) ?? new OdontogramToothState { ToothNumber = i }, true);
                    });
                });

                mainCol.Item().Height(30);

                mainCol.Item().Row(row =>
                {
                    row.RelativeItem().AlignCenter().Row(r =>
                    {
                        for (int i = 48; i >= 41; i--) AddGeometricTooth(r, teeth.FirstOrDefault(t => t.ToothNumber == i) ?? new OdontogramToothState { ToothNumber = i }, false);
                        r.ConstantItem(15);
                        for (int i = 31; i <= 38; i++) AddGeometricTooth(r, teeth.FirstOrDefault(t => t.ToothNumber == i) ?? new OdontogramToothState { ToothNumber = i }, false);
                    });
                });
            });
        }

        private void AddGeometricTooth(RowDescriptor row, OdontogramToothState tooth, bool isUpper)
        {
            float size = 35f;

            row.AutoItem().Padding(2).Column(col =>
            {
                if (isUpper) col.Item().AlignCenter().Text(tooth.ToothNumber.ToString()).FontSize(9).Bold();

                // Generamos el SVG como string
                string svgContent = GenerateToothSvg(size, size, tooth, isUpper);
                col.Item().Width(size).Height(size).Svg(svgContent);

                if (!isUpper) col.Item().AlignCenter().Text(tooth.ToothNumber.ToString()).FontSize(9).Bold();
            });
        }

        // Helper para SVG del Diente
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
                if (r == ToothRestoration.Obturacion) color = SKColor.Parse("#3498DB");
                else if (r == ToothRestoration.Sellador) color = SKColor.Parse("#2ECC71");
                else if (r == ToothRestoration.ProtesisFija) color = SKColor.Parse("#C0392B");
                else if (c == ToothCondition.Caries) color = SKColor.Parse("#E74C3C");
                else if (c == ToothCondition.Fractura) color = SKColor.Parse("#F1C40F");

                return new SKPaint { Color = color, IsStroke = false, IsAntialias = true };
            }

            using var paintCenter = GetPaint(tooth.OclusalCondition, tooth.OclusalRestoration);
            canvas.DrawCircle(cx, cy, radius, paintCenter);

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

            var condTop = isUpper ? tooth.VestibularCondition : tooth.LingualCondition;
            var restTop = isUpper ? tooth.VestibularRestoration : tooth.LingualRestoration;
            var condBot = isUpper ? tooth.LingualCondition : tooth.VestibularCondition;
            var restBot = isUpper ? tooth.LingualRestoration : tooth.VestibularRestoration;

            bool isRightQuadrant = (tooth.ToothNumber >= 11 && tooth.ToothNumber <= 18) || (tooth.ToothNumber >= 41 && tooth.ToothNumber <= 48);
            var condLeft = isRightQuadrant ? tooth.DistalCondition : tooth.MesialCondition;
            var restLeft = isRightQuadrant ? tooth.DistalRestoration : tooth.MesialRestoration;
            var condRight = isRightQuadrant ? tooth.MesialCondition : tooth.DistalCondition;
            var restRight = isRightQuadrant ? tooth.MesialRestoration : tooth.DistalRestoration;

            canvas.DrawPath(pathTop, GetPaint(condTop, restTop)); canvas.DrawPath(pathTop, paintStroke);
            canvas.DrawPath(pathBot, GetPaint(condBot, restBot)); canvas.DrawPath(pathBot, paintStroke);
            canvas.DrawPath(pathLeft, GetPaint(condLeft, restLeft)); canvas.DrawPath(pathLeft, paintStroke);
            canvas.DrawPath(pathRight, GetPaint(condRight, restRight)); canvas.DrawPath(pathRight, paintStroke);
            canvas.DrawCircle(cx, cy, radius, paintStroke);

            if (IsFullRestoration(tooth.FullRestoration))
            {
                using var paintFull = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
                if (tooth.FullRestoration == ToothRestoration.Corona) paintFull.Color = SKColor.Parse("#F1C40F");
                else if (tooth.FullRestoration == ToothRestoration.Endodoncia) paintFull.Color = SKColor.Parse("#9B59B6");
                else paintFull.Color = SKColors.Gray;

                canvas.DrawCircle(cx, cy, w / 2 - 2, paintFull);
                canvas.DrawCircle(cx, cy, w / 2 - 2, paintStroke);
            }

            if (tooth.FullCondition == ToothCondition.ExtraccionIndicada)
            {
                using var paintEx = new SKPaint { Color = SKColors.OrangeRed, IsStroke = true, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawCircle(cx, cy, w / 2 - 2, paintEx);
            }

            if (tooth.FullCondition == ToothCondition.Ausente)
            {
                using var paintAbsent = new SKPaint { Color = SKColors.DarkRed, IsStroke = true, StrokeWidth = 3, IsAntialias = true };
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
                row.Spacing(20);
                void AddLegendItem(string text, string colorHex, bool isX = false)
                {
                    row.AutoItem().Row(r =>
                    {
                        // Generamos SVG para la leyenda
                        string svgContent = GenerateLegendItemSvg(12, 12, colorHex, isX);
                        r.AutoItem().Width(12).Height(12).Svg(svgContent);
                        r.AutoItem().PaddingLeft(5).Text(text).FontSize(9);
                    });
                }

                AddLegendItem("Sano", "#FFFFFF");
                AddLegendItem("Caries", "#E74C3C");
                AddLegendItem("Obturación", "#3498DB");
                AddLegendItem("Corona", "#F1C40F");
                AddLegendItem("Ausente", "#8B0000", true);
                AddLegendItem("Endodoncia", "#9B59B6");
                AddLegendItem("Implante", "#808080");
            });
        }

        // Helper para SVG de Leyenda
        private string GenerateLegendItemSvg(float width, float height, string colorHex, bool isX)
        {
            using var stream = new MemoryStream();
            using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, width, height), stream))
            {
                var paint = new SKPaint { Color = SKColor.Parse(colorHex), IsAntialias = true, Style = SKPaintStyle.Fill };
                if (isX)
                {
                    var paintX = new SKPaint { Color = SKColor.Parse(colorHex), IsStroke = true, StrokeWidth = 2 };
                    canvas.DrawLine(0, 0, width, height, paintX);
                    canvas.DrawLine(width, 0, 0, height, paintX);
                }
                else
                {
                    canvas.DrawRect(0, 0, width, height, paint);
                    var border = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1 };
                    canvas.DrawRect(0, 0, width, height, border);
                }
            }
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // =================================================================================================
        // 5. GENERACIÓN DE HISTORIAL PDF
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
                })
                .GeneratePdf();
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
using iTextSharp.text.pdf;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
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
    // --- CLASES DTO PRIVADAS PARA DESERIALIZAR EL JSON DEL ODONTOGRAMA ---
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
    // -----------------------------------------------------------------

    // --- INICIO DE LA MODIFICACIÓN: Clase Helper para el historial unificado (v2) ---
    /// <summary>
    /// Clase interna para unificar Cargos y Abonos en una sola lista cronológica para el PDF.
    /// </summary>
    internal class PdfHistoryEvent
    {
        public DateTime Timestamp { get; set; }
        public string Doctor { get; set; } = string.Empty; // Columna solo para el Doctor
        public string Concept { get; set; } = string.Empty; // Diagnóstico, Observaciones o "Abono (Método)"
        public decimal Debe { get; set; } // Importe del Cargo
        public decimal Haber { get; set; } // Importe del Abono
    }
    // --- FIN DE LA MODIFICACIÓN ---


    public class PdfService : IPdfService
    {
        private readonly AppSettings _settings;
        private readonly string _baseBudgetsPath;
        private readonly IPatientRepository _patientRepository;
        private readonly string _basePrescriptionsPath;
        private readonly string _baseOdontogramsPath;


        // --- Definición de colores del nuevo template ---
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

        // --- 1. GENERACIÓN DE PRESUPUESTO PDF---
        public async Task<string> GenerateBudgetPdfAsync(Budget budget)
        {
            // --- INICIO DE LA MODIFICACIÓN (Nombre de archivo) ---

            // 1. Asegurarnos de tener los datos del paciente PRIMERO
            var patient = budget.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(budget.PatientId);
                if (loadedPatient == null)
                {
                    throw new InvalidOperationException($"Los datos del paciente (ID: {budget.PatientId}) no estaban cargados al generar el PDF del presupuesto {budget.BudgetNumber}.");
                }
                budget.Patient = loadedPatient; // Asignamos de nuevo al presupuesto
                patient = loadedPatient;
            }

            // 2. Crear la carpeta del año
            string yearFolder = Path.Combine(_baseBudgetsPath, budget.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            // 3. Limpiar los nombres para usarlos en el archivo
            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientNameClean = patient.Name.Replace(' ', '_').Replace(".", "").Replace(",", "");

            // 4. Definir el NUEVO nombre de archivo (Formato: 2025-0001_Apellido_Nombre.pdf)
            string fileName = $"{budget.BudgetNumber}_{patientSurnameClean}_{patientNameClean}.pdf";
            string filePath = Path.Combine(yearFolder, fileName);



            // El resto del método continúa igual...
            string maskedDni = MaskDni(patient.DniNie);

            await Task.Run(() =>
            {
                QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(11).FontFamily(Fonts.Calibri));

                        page.Header().Element(c => ComposeHeader(c, budget, maskedDni));
                        page.Content().Element(c => ComposeContent(c, budget));

                        // --- LLAMADA AL PIE DE PÁGINA (MODIFICADO) ---
                        page.Footer().Element(c => ComposeFooter(c));
                    });
                })
                .GeneratePdf(filePath);
            });

            return filePath;
        }


        // --- 2. GENERACIÓN DE RECETA PDF (OFICIAL) ---
        public async Task<string> GeneratePrescriptionPdfAsync(Prescription prescription)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PlantillaReceta.pdf");
            string yearFolder = Path.Combine(_basePrescriptionsPath, prescription.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            var patient = prescription.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(prescription.PatientId);
                if (loadedPatient == null)
                {
                    throw new InvalidOperationException($"Faltan datos del paciente (ID: {prescription.PatientId}) para generar la receta.");
                }
                prescription.Patient = loadedPatient;
                patient = loadedPatient;
            }
            if (!prescription.Items.Any())
            {
                throw new InvalidOperationException($"La receta (ID: {prescription.Id}) no contiene items (medicamentos).");
            }
            var firstItem = prescription.Items.First();

            string patientNameClean = patient!.Name.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientDniClean = patient.DniNie.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string comprehensiveIdentifier = $"{patientSurnameClean}_{patientNameClean}_{patientDniClean}";
            string fileNameSuffix = prescription.Id > 0 ? prescription.Id.ToString() : prescription.IssueDate.ToString("yyyyMMdd_HHmmss");
            string fileName = $"Receta_{comprehensiveIdentifier}_{fileNameSuffix}.pdf";
            string outputPath = Path.Combine(yearFolder, fileName);

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("No se encontró la plantilla PDF en la ruta esperada.", templatePath);
            }

            // --- LÓGICA REFACTORIZADA ---

            // 1. Usar la nueva propiedad 'DurationInDays'
            int diasTratamiento = firstItem.DurationInDays ?? 1;

            int unidadesPorToma = 1;
            int tomasAlDia = 1;
            int unidadesPorEnvase = 30; // Valor por defecto

            // 2. Extraer 'unidadesPorToma' desde 'Quantity' (Ej: "1 comprimido")
            if (!string.IsNullOrWhiteSpace(firstItem.Quantity))
            {
                var matchUnidades = System.Text.RegularExpressions.Regex.Match(firstItem.Quantity, @"\d+");
                if (matchUnidades.Success) int.TryParse(matchUnidades.Groups[0].Value, out unidadesPorToma);
            }

            // 3. Extraer 'tomasAlDia' desde 'DosagePauta' (Ej: "cada 8 horas")
            if (!string.IsNullOrWhiteSpace(firstItem.DosagePauta))
            {
                var pautaLower = firstItem.DosagePauta.ToLower();
                var matchHoras = System.Text.RegularExpressions.Regex.Match(pautaLower, @"cada\s+(\d+)\s*(horas?|hs?)");
                if (matchHoras.Success)
                {
                    if (int.TryParse(matchHoras.Groups[1].Value, out int horas) && horas > 0 && horas <= 24)
                    {
                        tomasAlDia = 24 / horas;
                    }
                }
            }
            // --- FIN LÓGICA REFACTORIZADA ---


            int unidadesTotales = diasTratamiento * unidadesPorToma * tomasAlDia;
            int numEnvasesCalculado = (unidadesPorEnvase > 0) ? (int)Math.Ceiling((double)unidadesTotales / unidadesPorEnvase) : 1;
            if (numEnvasesCalculado == 0) numEnvasesCalculado = 1;
            string medicFull = firstItem.MedicationName ?? "";


            await Task.Run(() =>
            {
                PdfReader pdfReader = null;
                PdfStamper pdfStamper = null;
                FileStream fs = null;

                try
                {
                    pdfReader = new PdfReader(templatePath);
                    fs = new FileStream(outputPath, FileMode.Create);
                    pdfStamper = new PdfStamper(pdfReader, fs);
                    AcroFields formFields = pdfStamper.AcroFields;

                    // --- INICIO DE LA SOLUCIÓN (TAMAÑO DE LETRA + APLANADO) ---

                    // 1. Cargar una fuente base (ESENCIAL para aplanar con texto)
                    BaseFont helvetica = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);

                    // 2. Asignar la fuente a los campos de medicamento
                    formFields.SetFieldProperty("Medic", "textfont", helvetica, null);
                    formFields.SetFieldProperty("MedicCop", "textfont", helvetica, null);

                    // 3. Asignar el TAMAÑO DE FUENTE (8f)
                    formFields.SetFieldProperty("Medic", "textsize", 8f, null);
                    formFields.SetFieldProperty("MedicCop", "textsize", 8f, null);

                    // --- FIN DE LA SOLUCIÓN ---

                    // Código de la fecha (este ya estaba bien)
                    const int ALIGN_CENTER = 1;
                    formFields.SetFieldProperty("Fecha", "textsize", 8f, null);
                    formFields.SetFieldProperty("FechaCop", "textsize", 8f, null);
                    formFields.SetFieldProperty("Fecha", "alignment", ALIGN_CENTER, null);
                    formFields.SetFieldProperty("FechaCop", "alignment", ALIGN_CENTER, null);

                    string fechaFormato = prescription.IssueDate.ToString("dd/MM/yyyy");
                    string duracionNumero = diasTratamiento.ToString();
                    string nombreCompleto = $"{patient.Name} {patient.Surname}";

                    formFields.SetField("CIF", _settings.ClinicCif ?? "");
                    formFields.SetField("NombrePac", nombreCompleto);
                    formFields.SetField("DNIPac", patient.DniNie);
                    formFields.SetField("NombrePacCop", nombreCompleto);
                    formFields.SetField("DNIPacCop", patient.DniNie);

                    formFields.SetField("Unidades", unidadesTotales.ToString());
                    formFields.SetField("Pauta", firstItem.DosagePauta ?? "");
                    formFields.SetField("Fecha", fechaFormato);
                    formFields.SetField("NumEnv", numEnvasesCalculado.ToString());
                    formFields.SetField("DurTrat", duracionNumero);
                    formFields.SetField("Medic", medicFull);
                    formFields.SetField("MedicamentoNombre", firstItem.MedicationName ?? "");
                    formFields.SetField("Fecha_af_date", fechaFormato);

                    formFields.SetField("UnidadesCop", unidadesTotales.ToString());
                    formFields.SetField("PautaCop", firstItem.DosagePauta ?? "");
                    formFields.SetField("FechaCop", fechaFormato);
                    formFields.SetField("NumEnvCop", numEnvasesCalculado.ToString());
                    formFields.SetField("DurTratCop", duracionNumero);
                    formFields.SetField("MedicCop", medicFull);
                    formFields.SetField("MedicamentoNombreCop", firstItem.MedicationName ?? "");
                    formFields.SetField("Fecha_Cop_af_date", fechaFormato);
                    formFields.SetField("Indicaciones", prescription.Instructions ?? "");

                    formFields.SetField("PrescriptorNombre", prescription.PrescriptorName ?? "");
                    formFields.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");

                    // 4. APLANAR EL FORMULARIO
                    // Esto "quema" el texto con la fuente y tamaño definidos
                    // y ELIMINA el campo de formulario azul.
                    pdfStamper.FormFlattening = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error al rellenar plantilla PDF ({Path.GetFileName(outputPath)}): {ex.Message} (Ruta plantilla: {templatePath})", ex);
                }
                finally
                {
                    pdfStamper?.Close();
                    fs?.Dispose();
                    pdfReader?.Close();
                }
            });

            return outputPath;
        }

        // --- 3. GENERACIÓN DE RECETA PDF (BÁSICA) ---
        public async Task<string> GenerateBasicPrescriptionPdfAsync(Prescription prescription)
        {
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PlantillaRecetaBasica.pdf");

            string yearFolder = Path.Combine(_basePrescriptionsPath, prescription.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            var patient = prescription.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(prescription.PatientId);
                if (loadedPatient == null)
                {
                    throw new InvalidOperationException($"Faltan datos del paciente (ID: {prescription.PatientId}) para generar la receta.");
                }
                prescription.Patient = loadedPatient;
                patient = loadedPatient;
            }
            if (!prescription.Items.Any())
            {
                throw new InvalidOperationException($"La receta (ID: {prescription.Id}) no contiene items (medicamentos).");
            }
            var firstItem = prescription.Items.First();

            string patientNameClean = patient!.Name.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientSurnameClean = patient.Surname.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string patientDniClean = patient.DniNie.Replace(' ', '_').Replace(".", "").Replace(",", "");
            string comprehensiveIdentifier = $"{patientSurnameClean}_{patientNameClean}_{patientDniClean}";
            string fileNameSuffix = prescription.Id > 0 ? prescription.Id.ToString() : prescription.IssueDate.ToString("yyyyMMdd_HHmmss");
            string fileName = $"RecetaBasica_{comprehensiveIdentifier}_{fileNameSuffix}.pdf";
            string outputPath = Path.Combine(yearFolder, fileName);

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("No se encontró la plantilla PDF básica en la ruta esperada.", templatePath);
            }

            // --- LÓGICA REFACTORIZADA ---

            // 1. Usar la nueva propiedad 'DurationInDays'
            int diasTratamiento = firstItem.DurationInDays ?? 1;

            int unidadesPorToma = 1;
            int tomasAlDia = 1;
            int unidadesPorEnvase = 30; // Valor por defecto

            // 2. Extraer 'unidadesPorToma' desde 'Quantity' (Ej: "1 comprimido")
            if (!string.IsNullOrWhiteSpace(firstItem.Quantity))
            {
                var matchUnidades = System.Text.RegularExpressions.Regex.Match(firstItem.Quantity, @"\d+");
                if (matchUnidades.Success) int.TryParse(matchUnidades.Groups[0].Value, out unidadesPorToma);
            }

            // 3. Extraer 'tomasAlDia' desde 'DosagePauta' (Ej: "cada 8 horas")
            if (!string.IsNullOrWhiteSpace(firstItem.DosagePauta))
            {
                var pautaLower = firstItem.DosagePauta.ToLower();
                var matchHoras = System.Text.RegularExpressions.Regex.Match(pautaLower, @"cada\s+(\d+)\s*(horas?|hs?)");
                if (matchHoras.Success)
                {
                    if (int.TryParse(matchHoras.Groups[1].Value, out int horas) && horas > 0 && horas <= 24)
                    {
                        tomasAlDia = 24 / horas;
                    }
                }
            }
            // --- FIN LÓGICA REFACTORIZADA ---


            int unidadesTotales = diasTratamiento * unidadesPorToma * tomasAlDia;
            int numEnvasesCalculado = (unidadesPorEnvase > 0) ? (int)Math.Ceiling((double)unidadesTotales / unidadesPorEnvase) : 1;
            if (numEnvasesCalculado == 0) numEnvasesCalculado = 1;
            string medicFull = firstItem.MedicationName ?? "";


            await Task.Run(() =>
            {
                PdfReader pdfReader = null;
                PdfStamper pdfStamper = null;
                FileStream fs = null;

                try
                {
                    pdfReader = new PdfReader(templatePath);
                    fs = new FileStream(outputPath, FileMode.Create);
                    pdfStamper = new PdfStamper(pdfReader, fs);
                    AcroFields formFields = pdfStamper.AcroFields;

                    const int ALIGN_CENTER = 1;

                    formFields.SetFieldProperty("Fecha", "textsize", 8f, null);
                    formFields.SetFieldProperty("FechaCop", "textsize", 8f, null);
                    formFields.SetFieldProperty("Fecha", "alignment", ALIGN_CENTER, null);
                    formFields.SetFieldProperty("FechaCop", "alignment", ALIGN_CENTER, null);

                    string fechaFormato = prescription.IssueDate.ToString("dd/MM/yyyy");
                    string duracionNumero = diasTratamiento.ToString();
                    string nombreCompleto = $"{patient.Name} {patient.Surname}";

                    formFields.SetField("CIF", _settings.ClinicCif ?? "");
                    formFields.SetField("NombrePac", nombreCompleto);
                    formFields.SetField("DNIPac", patient.DniNie);
                    formFields.SetField("NombrePacCop", nombreCompleto);
                    formFields.SetField("DNIPacCop", patient.DniNie);

                    formFields.SetField("Unidades", unidadesTotales.ToString());
                    formFields.SetField("Pauta", firstItem.DosagePauta ?? "");
                    formFields.SetField("Fecha", fechaFormato);
                    formFields.SetField("NumEnv", numEnvasesCalculado.ToString());
                    formFields.SetField("DurTrat", duracionNumero);
                    formFields.SetField("Medic", medicFull);
                    formFields.SetField("MedicamentoNombre", firstItem.MedicationName ?? "");
                    formFields.SetField("Fecha_af_date", fechaFormato);

                    formFields.SetField("UnidadesCop", unidadesTotales.ToString());
                    formFields.SetField("PautaCop", firstItem.DosagePauta ?? "");
                    formFields.SetField("FechaCop", fechaFormato);
                    formFields.SetField("NumEnvCop", numEnvasesCalculado.ToString());
                    formFields.SetField("DurTratCop", duracionNumero);
                    formFields.SetField("MedicCop", medicFull);
                    formFields.SetField("MedicamentoNombreCop", firstItem.MedicationName ?? "");
                    formFields.SetField("Fecha_Cop_af_date", fechaFormato);
                    formFields.SetField("Indicaciones", prescription.Instructions ?? "");

                    formFields.SetField("PrescriptorNombre", prescription.PrescriptorName ?? "");
                    formFields.SetField("PrescriptorNombreCop", prescription.PrescriptorName ?? "");

                    pdfStamper.FormFlattening = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error al rellenar plantilla PDF ({Path.GetFileName(outputPath)}): {ex.Message} (Ruta plantilla: {templatePath})", ex);
                }
                finally
                {
                    pdfStamper?.Close();
                    fs?.Dispose();
                    pdfReader?.Close();
                }
            });

            return outputPath;
        }


        // --- 4. GENERACIÓN DE ODONTOGRAMA PDF ---
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
            catch (Exception)
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
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));

                        page.Header().Column(col =>
                        {
                            // --- CORRECCIÓN CS1503 (API Moderna) ---
                            col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental").Bold().FontSize(14));
                            col.Item().Text(text => text.Span($"Odontograma de: {patient.PatientDisplayInfo}").FontSize(16).Bold());
                            col.Item().Text(text => text.Span($"Fecha de Emisión: {DateTime.Now:dd/MM/yyyy HH:mm}"));
                            col.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                        });

                        page.Content().AlignCenter().Column(col =>
                        {
                            col.Item().Element(c => ComposeOdontogramGrid(c, teeth));
                            col.Item().PaddingTop(20).Element(c => ComposeOdontogramLegend(c));
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

        // --- MÉTODOS AUXILIARES PARA EL PDF DEL ODONTOGRAMA (Corregidos API Moderna) ---
        private void ComposeOdontogramGrid(IContainer container, List<OdontogramToothState> teeth)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    for (int i = 0; i < 16; i++)
                        columns.ConstantColumn(40, Unit.Point);
                });

                // Fila 1: Cuadrante 1 (18 a 11) y Cuadrante 2 (21 a 28)
                for (int i = 18; i >= 11; i--) AddToothCell(table, teeth.FirstOrDefault(t => t.ToothNumber == i));
                for (int i = 21; i <= 28; i++) AddToothCell(table, teeth.FirstOrDefault(t => t.ToothNumber == i));

                // Fila 2: Cuadrante 4 (48 a 41) y Cuadrante 3 (31 a 38)
                for (int i = 48; i >= 41; i--) AddToothCell(table, teeth.FirstOrDefault(t => t.ToothNumber == i));
                for (int i = 31; i <= 38; i++) AddToothCell(table, teeth.FirstOrDefault(t => t.ToothNumber == i));
            });
        }

        private void AddToothCell(TableDescriptor table, OdontogramToothState? tooth)
        {
            table.Cell().Height(40, Unit.Point).Column(col =>
            {
                if (tooth == null)
                {
                    // --- CORRECCIÓN CS0023 / CS1503 (API Moderna) ---
                    // .Text debe ser la última llamada en el contenedor.
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).AlignCenter().Text(text => text.Span("?"));
                    return;
                }

                // --- CORRECCIÓN CS1503 (API Moderna) ---
                col.Item().AlignCenter().Text(text => text.Span(tooth.ToothNumber.ToString()).FontSize(8));

                col.Item().Extend().Table(g =>
                {
                    g.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    // Fila 0: (empty), VESTIBULAR, (empty)
                    g.Cell();
                    g.Cell().Element(c => DrawSurface(c, tooth.VestibularCondition, tooth.VestibularRestoration));
                    g.Cell();

                    // Fila 1: MESIAL, OCLUSAL, DISTAL
                    g.Cell().Element(c => DrawSurface(c, tooth.MesialCondition, tooth.MesialRestoration));
                    g.Cell().Element(c => DrawSurface(c, tooth.OclusalCondition, tooth.OclusalRestoration));
                    g.Cell().Element(c => DrawSurface(c, tooth.DistalCondition, tooth.DistalRestoration));

                    // Fila 2: (empty), LINGUAL, (empty)
                    g.Cell();
                    g.Cell().Element(c => DrawSurface(c, tooth.LingualCondition, tooth.LingualRestoration));
                    g.Cell();
                });
            });
        }

        private IContainer DrawSurface(IContainer container, ToothCondition condition, ToothRestoration restoration)
        {
            string finalColor;

            if (restoration != ToothRestoration.Ninguna)
            {
                finalColor = GetRestorationColor(restoration);
            }
            else
            {
                finalColor = GetConditionColor(condition);
            }

            return container
                .Background(finalColor)
                .Border(0.5f).BorderColor(Colors.Grey.Medium);
        }

        private string GetConditionColor(ToothCondition condition)
        {
            return condition switch
            {
                ToothCondition.Sano => Colors.White,
                ToothCondition.Caries => Colors.Red.Lighten1,
                ToothCondition.ExtraccionIndicada => Colors.Orange.Medium,
                ToothCondition.Ausente => Colors.Grey.Lighten1,
                ToothCondition.Fractura => Colors.Yellow.Medium,
                _ => Colors.White,
            };
        }

        private string GetRestorationColor(ToothRestoration restoration)
        {
            return restoration switch
            {
                ToothRestoration.Obturacion => Colors.Blue.Lighten1,
                ToothRestoration.Corona => Colors.Yellow.Lighten1,
                ToothRestoration.Implante => Colors.Grey.Darken1,
                ToothRestoration.Endodoncia => Colors.Purple.Lighten1,
                _ => Colors.Transparent,
            };
        }

        private void ComposeOdontogramLegend(IContainer container)
        {
            container.AlignCenter().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.Spacing(15);
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
                    row.AutoItem().Text(text => text.Span("Condición:").Bold());
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.White).Border(1).BorderColor(Colors.Black);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Sano"));
                    });
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Red.Lighten1);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Caries"));
                    });
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Orange.Medium);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Extracción/Fractura"));
                    });
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Grey.Lighten1);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Ausente"));
                    });
                });

                col.Item().PaddingTop(5).Row(row =>
                {
                    row.Spacing(15);
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
                    row.AutoItem().Text(text => text.Span("Restauración:").Bold());
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Blue.Lighten1);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Obturación"));
                    });
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Yellow.Lighten1);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Corona"));
                    });
                    row.AutoItem().Row(r => {
                        r.AutoItem().Width(12).Height(12).Background(Colors.Purple.Lighten1);
                        r.AutoItem().PaddingLeft(5).Text(text => text.Span("Endodoncia"));
                    });
                });
            });
        }


        // --- ¡¡INICIO DE CÓDIGO PARA HISTORIAL!! ---

        // --- 5. GENERACIÓN DE HISTORIAL PDF ---
        public async Task<byte[]> GenerateHistoryPdfAsync(Patient patient, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance)
        {
            // Usamos Task.Run para no bloquear el hilo de UI
            return await Task.Run(() =>
            {
                // Genera el documento en memoria y devuelve los bytes
                return QuestPDF.Fluent.Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4); // Vertical
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));

                        page.Header().Element(c => ComposeHistoryHeader(c, patient));

                        // --- INICIO MODIFICACIÓN: Pasamos las listas originales a ComposeHistoryContent ---
                        page.Content().Element(c => ComposeHistoryContent(c, entries, payments, totalBalance));
                        // --- FIN MODIFICACIÓN ---

                        // --- INICIO MODIFICACIÓN: Llamamos al nuevo pie de página del historial ---
                        page.Footer().Element(c => ComposeHistoryFooter(c));
                        // --- FIN MODIFICACIÓN ---
                    });
                })
                .GeneratePdf(); // <-- Este método devuelve byte[]
            });
        }

        /// <summary>
        /// Crea el encabezado para el PDF del historial.
        /// </summary>
        private void ComposeHistoryHeader(IContainer container, Patient patient)
        {
            container.Column(column =>
            {
                // Título principal con los datos de la clínica
                column.Item().Row(row =>
                {
                    // Columna 1: Logo y Datos Clínica
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
                                // --- CORRECCIÓN CS0023 (API Moderna) ---
                                // Separamos la configuración del contenedor de la llamada a .Image()
                                var imageContainer = col.Item().PaddingBottom(5).MaxHeight(2.5f, Unit.Centimetre);
                                imageContainer.Image(logoPath);
                            }
                            catch { /* Ignorar error de logo */ }
                        }

                        // --- CORRECCIÓN CS1503 (API Moderna) ---
                        col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental").Bold().FontSize(12));
                        col.Item().Text(text => text.Span($"CIF: {_settings.ClinicCif ?? "N/A"}"));
                        col.Item().Text(text => text.Span(_settings.ClinicAddress ?? "Dirección"));
                        col.Item().Text(text => text.Span($"Tel: {_settings.ClinicPhone ?? "N/A"}"));
                    });

                    // Columna 2: Título del Documento y Fecha
                    row.RelativeItem(1).Column(col =>
                    {
                        // --- CORRECCIÓN CS1503 (API Moderna) ---
                        col.Item().AlignRight().Text(text => text.Span("Historial de Cuenta").Bold().FontSize(16));
                        col.Item().AlignRight().Text(text => text.Span($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}"));
                    });
                });

                // Título del Paciente (el que solicitaste)
                column.Item().PaddingTop(20).Column(col =>
                {
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
                    col.Item().Text(text => text.Span("Paciente:").FontSize(12));
                    col.Item().Text(text => text.Span($"{patient.Name} {patient.Surname}").Bold().FontSize(14));
                    col.Item().Text(text => text.Span($"DNI/NIE: {patient.DniNie}"));
                });

                column.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
            });
        }

        // --- INICIO DE LA MODIFICACIÓN: ComposeHistoryContent REESCRITO (v2) ---
        /// <summary>
        /// Crea el contenido principal con la tabla unificada de cargos y abonos.
        /// </summary>
        private void ComposeHistoryContent(IContainer container, List<ClinicalEntry> entries, List<Payment> payments, decimal totalBalance)
        {
            // 1. Unificar las dos listas en una sola lista de 'PdfHistoryEvent'
            var unifiedHistory = new List<PdfHistoryEvent>();

            foreach (var entry in entries)
            {
                unifiedHistory.Add(new PdfHistoryEvent
                {
                    Timestamp = entry.VisitDate,
                    Doctor = entry.DoctorName, // Columna "Doctor"
                    Concept = entry.Diagnosis ?? "N/A", // Columna "Concepto"
                    Debe = entry.TotalCost,
                    Haber = 0
                });
            }

            foreach (var payment in payments)
            {
                // Construir el concepto del pago
                string concept = $"Abono (Método: {payment.Method ?? "N/A"})";
                if (!string.IsNullOrWhiteSpace(payment.Observaciones))
                {
                    concept += $" - {payment.Observaciones}";
                }

                unifiedHistory.Add(new PdfHistoryEvent
                {
                    Timestamp = payment.PaymentDate,
                    Doctor = "", // Columna "Doctor" vacía
                    Concept = concept, // Columna "Concepto"
                    Debe = 0,
                    Haber = payment.Amount
                });
            }

            // 2. Ordenar la lista unificada por fecha
            var sortedHistory = unifiedHistory.OrderBy(e => e.Timestamp).ToList();

            container.PaddingVertical(15).Column(col =>
            {
                col.Spacing(20); // Espacio entre elementos

                // --- TABLA ÚNICA: HISTORIAL CRONOLÓGICO ---
                col.Item().Column(tableCol =>
                {
                    tableCol.Item().PaddingBottom(5).Text(text => text.Span("Historial de Cuenta").Bold().FontSize(14));

                    tableCol.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(65);     // Fecha
                            columns.RelativeColumn(1.5f); // Doctor
                            columns.RelativeColumn(3);    // Concepto/Observaciones
                            columns.RelativeColumn(1);    // Debe
                            columns.RelativeColumn(1);    // Haber
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Fecha"));
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Doctor"));
                            header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Concepto/Observaciones"));
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text(text => text.Span("Debe"));
                            header.Cell().Element(HeaderCellStyle).AlignRight().Text(text => text.Span("Haber"));
                        });

                        // 3. Iterar sobre la lista ordenada y rellenar la tabla
                        foreach (var item in sortedHistory)
                        {
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span($"{item.Timestamp:dd/MM/yy}"));
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span(item.Doctor));
                            table.Cell().Element(BodyCellStyle).Text(text => text.Span(item.Concept));

                            // Columna Debe (Cargo)
                            string debeText = item.Debe > 0 ? $"{item.Debe:C}" : "";
                            table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(debeText).FontColor(Colors.Red.Medium));

                            // Columna Haber (Abono)
                            string haberText = item.Haber > 0 ? $"{item.Haber:C}" : "";
                            table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(haberText).FontColor(Colors.Green.Medium));
                        }
                    });
                });

                // --- RESUMEN DE SALDO TOTAL (Sin cambios, sigue siendo correcto) ---
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

                        row.RelativeItem().Text(text =>
                        {
                            text.Span(label).FontColor(color).Bold().FontSize(14);
                        });

                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span($"{totalBalance:C}").FontColor(color).Bold().FontSize(14);
                        });
                    });
                });

            });
        }
        // --- FIN DE LA MODIFICACIÓN ---


        // --- MÉTODOS AUXILIARES (PRESUPUESTO) ---

        // (Esta es tu sugerencia, ¡correcta!)
        private IContainer BodyCellStyle(IContainer container)
        {
            // Por defecto, alineación a la izquierda y estilo de celda de cuerpo.
            return container.Border(1).BorderColor(ColorTableBorder)
                            .PaddingVertical(5).PaddingHorizontal(5)
                            .AlignLeft();
        }

        // Sobrecarga para alineación derecha
        private IContainer BodyCellStyle(IContainer container, bool alignRight)
        {
            var baseStyle = BodyCellStyle(container); // Llama a la base
            return alignRight ? baseStyle.AlignRight() : baseStyle; // Modifica la alineación
        }

        private static IContainer HeaderCellStyle(IContainer c) =>
            c.Border(1).BorderColor(ColorTableBorder).Background(ColorTableHeaderBg)
             .PaddingVertical(5).PaddingHorizontal(5).AlignCenter();


        private void ComposeHeader(IContainer container, Budget budget, string maskedDni)
        {
            container.Column(column =>
            {
                // --- CORRECCIÓN CS1503 (API Moderna) ---
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
                                // --- CORRECCIÓN CS0023 (API Moderna) ---
                                var imageContainer = col.Item().PaddingBottom(5).MaxHeight(3.5f, Unit.Centimetre);
                                imageContainer.Image(logoPath);
                            }
                            catch (Exception) { /* Ignorar error de logo */ }
                        }

                        // --- CORRECCIÓN CS1503 (API Moderna) ---
                        col.Item().Text(text => text.Span(_settings.ClinicName ?? "Clínica Dental P&D").Bold().FontSize(12));
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
                                // --- CORRECCIÓN CS1503 (API Moderna) ---
                                r.ConstantItem(70).Text(text => text.Span("Paciente:").FontSize(11).Bold());
                                r.RelativeItem().Text(text => text.Span($"{budget.Patient?.Name ?? ""} {budget.Patient?.Surname ?? ""}").FontSize(11));
                            });
                            patientCol.Item().Row(r =>
                            {
                                // --- CORRECCIÓN CS1503 (API Moderna) ---
                                r.ConstantItem(70).Text(text => text.Span("DNI/NIF:").FontSize(11).Bold());
                                r.RelativeItem().Text(text => text.Span(maskedDni).FontSize(11));
                            });
                            patientCol.Item().Row(r =>
                            {
                                // --- CORRECCIÓN CS1503 (API Moderna) ---
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
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
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
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Descripción"));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Cantidad"));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Precio Unit."));
                    header.Cell().Element(HeaderCellStyle).Text(text => text.Span("Total ítem"));
                });

                foreach (var item in budget.Items ?? Enumerable.Empty<BudgetLineItem>())
                {
                    // --- CORRECCIÓN CS1503 (API Moderna) ---
                    table.Cell().Element(c => BodyCellStyle(c)).Text(text => text.Span(item.Description));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span(item.Quantity.ToString()));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span($"{item.UnitPrice:N2} €"));
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(text => text.Span($"{item.LineTotal:N2} €"));
                }
            });
        }

        // --- MÉTODO ComposeTotals (MODIFICADO PARA FINANCIACIÓN) ---
        private void ComposeTotals(IContainer container, Budget budget)
        {
            decimal discountAmount = budget.Subtotal * (budget.DiscountPercent / 100);
            decimal baseImponible = budget.Subtotal - discountAmount;
            decimal vatAmount = baseImponible * (budget.VatPercent / 100);

            // --- INICIO CÓDIGO MODIFICADO ---

            // Definición de estilos de celda (sin cambios)
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

                // --- Filas de Cálculo Base ---
                table.Cell().Element(TotalsLabelCell).Text(text => text.Span("Subtotal:").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{budget.Subtotal:N2} €"));

                // --- INICIO DE LA MODIFICACIÓN (Ocultar Descuento si es 0) ---
                if (discountAmount > 0)
                {
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Descuento ({budget.DiscountPercent}%):").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"-{discountAmount:N2} €"));
                }
                // --- FIN DE LA MODIFICACIÓN ---

                table.Cell().Element(TotalsLabelCell).Text(text => text.Span("Base Imponible:").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{baseImponible:N2} €"));

                table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"IVA ({budget.VatPercent}%):").Bold());
                table.Cell().Element(TotalsValueCell).Text(text => text.Span($"+{vatAmount:N2} €"));

                // --- Total Contado (MODIFICADO) ---
                var totalLabelCell = table.Cell().Element(TotalsLabelCell);
                totalLabelCell.Text(text =>
                {
                    // Texto cambiado de "Total:" a "Total (Contado):"
                    text.Span("Total (Contado):").FontSize(12).Bold();
                });

                var totalValueCell = table.Cell().Element(TotalsValueCell);
                totalValueCell.Text(text =>
                {
                    text.Span($"{budget.TotalAmount:N2} €").FontSize(12).Bold();
                });


                // --- INICIO CÓDIGO AÑADIDO (FINANCIACIÓN EN PDF) ---
                if (budget.NumberOfMonths > 0)
                {
                    // Separador
                    table.Cell().ColumnSpan(2).PaddingTop(5);

                    decimal monthlyPayment = 0;
                    decimal totalFinanced = 0;

                    if (budget.InterestRate == 0)
                    {
                        // Cálculo simple si no hay interés
                        monthlyPayment = budget.TotalAmount / budget.NumberOfMonths;
                        totalFinanced = budget.TotalAmount;
                    }
                    else
                    {
                        // Re-calculamos la cuota para el PDF
                        try
                        {
                            double totalV = (double)budget.TotalAmount;
                            double i = (double)(budget.InterestRate / 100) / 12; // Interés mensual
                            int n = budget.NumberOfMonths;
                            // Fórmula de amortización
                            double monthlyPaymentDouble = (totalV * i) / (1 - Math.Pow(1 + i, -n));
                            monthlyPayment = (decimal)Math.Round(monthlyPaymentDouble, 2, MidpointRounding.AwayFromZero);
                            totalFinanced = monthlyPayment * n;
                        }
                        catch { /* Ignorar error de cálculo en PDF */ }
                    }

                    // Fila: Plazos
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Plazos:").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{budget.NumberOfMonths} meses"));

                    // --- INICIO DE LA MODIFICACIÓN (Eliminar Fila Interés) ---
                    /*
                    // Fila: Interés (ELIMINADA)
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Interés (TIN):").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{budget.InterestRate:N2} %"));
                    */
                    // --- FIN DE LA MODIFICACIÓN ---

                    // Fila: Cuota Mensual
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Cuota Mensual:").FontSize(12).Bold().FontColor(Colors.Blue.Darken2));
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{monthlyPayment:N2} €").FontSize(12).Bold().FontColor(Colors.Blue.Darken2));

                    // Fila: Total Financiado
                    table.Cell().Element(TotalsLabelCell).Text(text => text.Span($"Total Financiado:").Bold());
                    table.Cell().Element(TotalsValueCell).Text(text => text.Span($"{totalFinanced:N2} €"));
                }
                // --- FIN CÓDIGO AÑADIDO ---

            });
            // --- FIN CÓDIGO MODIFICADO ---
        }

        // --- MÉTODO ComposeFooter (MODIFICADO PARA RGPD Y QUITAR NOTA) ---
        private void ComposeFooter(IContainer container)
        {
            // El texto legal que proporcionaste
            string legalText = "De conformidad con lo establecido en el RGPD 2016/679 de 27 de Abril, se le informa que los datos personales facilitados voluntariamente y sin carácter obligatorio por usted han sido incorporados a un fichero o soporte de datos personales cuya titularidad corresponde a P&D DENTAL SCP con domicilio en RAMBLA JUST OLIVERES 56, 2-1 (HOSPITALET LLOB) quien procederá al tratamiento de sus datos personales sobre la base juridica del consentimiento prestado por usted. Por último, se le informa que le asisten los derechos de acceso, rectificación, supresión, limitación, portabilidad y oposición al tratamiento, pudiendo ejercitarlos mediante petición escrita dirigida al titular del.";

            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Column(col =>
            {
                // 1. "Nota: Presupuesto valido por 30 dias" -> ELIMINADA

                // 2. Nuevo Texto Legal (RGPD)
                col.Item().PaddingTop(5).Text(text =>
                {
                    text.Span(legalText)
                        .FontSize(7) // Letra pequeña
                        .FontColor(Colors.Grey.Medium); // Color gris
                });

                // 3. Paginación
                col.Item().PaddingTop(5).AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }
        // --- FIN MODIFICACIÓN ---

        // --- INICIO DE LA MODIFICACIÓN: Nuevo pie de página solo para el historial ---
        /// <summary>
        /// Crea un pie de página simple solo con el número de página (para uso interno).
        /// </summary>
        private void ComposeHistoryFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Column(col =>
            {
                // Paginación
                col.Item().PaddingTop(5).AlignCenter().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }
        // --- FIN DE LA MODIFICACIÓN ---


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
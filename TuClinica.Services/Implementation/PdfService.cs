using iTextSharp.text.pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Repositories;
using TuClinica.Core.Interfaces.Services;
using TuClinica.Core.Models;

namespace TuClinica.Services.Implementation
{
    // Asegúrate de que esta clase sea pública y no parcial
    public class PdfService : IPdfService
    {
        private readonly AppSettings _settings;
        private readonly string _baseBudgetsPath;
        private readonly IPatientRepository _patientRepository;
        private readonly string _basePrescriptionsPath;

        // --- Definición de colores del nuevo template ---
        private static readonly string ColorTableHeaderBg = "#D9E5F6"; // Azul claro cabecera tabla
        private static readonly string ColorTableBorder = "#9BC2E6"; // Borde azul tabla
        private static readonly string ColorTotalsBg = "#F2F2F2";      // Fondo gris claro totales

        // *** CONSTRUCTOR CORREGIDO Y COMPLETO ***
        public PdfService(AppSettings settings, string baseBudgetsPath, string basePrescriptionsPath, IPatientRepository patientRepository)
        {
            _settings = settings;
            _baseBudgetsPath = baseBudgetsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            _basePrescriptionsPath = basePrescriptionsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            Directory.CreateDirectory(_baseBudgetsPath);
            Directory.CreateDirectory(_basePrescriptionsPath);

            _patientRepository = patientRepository; // <-- ASIGNACIÓN VITAL
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // --- 1. GENERACIÓN DE PRESUPUESTO PDF (Lógica principal sin cambios) ---
        public async Task<string> GenerateBudgetPdfAsync(Budget budget)
        {
            string yearFolder = Path.Combine(_baseBudgetsPath, budget.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            string fileName = $"Presupuesto_{budget.BudgetNumber}.pdf";
            string filePath = Path.Combine(yearFolder, fileName);

            var patient = budget.Patient;
            if (patient == null)
            {
                var loadedPatient = await _patientRepository.GetByIdAsync(budget.PatientId);
                if (loadedPatient == null)
                {
                    throw new InvalidOperationException($"Los datos del paciente (ID: {budget.PatientId}) no estaban cargados al generar el PDF del presupuesto {budget.BudgetNumber}.");
                }
                budget.Patient = loadedPatient;
                patient = loadedPatient;
            }

            string maskedDni = MaskDni(patient.DniNie);

            await Task.Run(() =>
            {
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(1.5f, Unit.Centimetre);
                        page.DefaultTextStyle(ts => ts.FontSize(10).FontFamily(Fonts.Calibri));

                        page.Header().Element(c => ComposeHeader(c, budget, maskedDni));
                        page.Content().Element(c => ComposeContent(c, budget));
                        page.Footer().Element(ComposeFooter);
                    });
                })
                .GeneratePdf(filePath);
            });

            return filePath;
        }


        // --- 2. GENERACIÓN DE RECETA PDF (Sin cambios) ---
        public async Task<string> GeneratePrescriptionPdfAsync(Prescription prescription)
        {
            // --- 1. Definir Rutas BASE ---
            string templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", "PlantillaReceta.pdf");
            string yearFolder = Path.Combine(_basePrescriptionsPath, prescription.IssueDate.Year.ToString());
            Directory.CreateDirectory(yearFolder);

            // --- 2. Preparar Datos (Carga Defensiva) ---
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

            // ... (Lógica de nombre de archivo sin cambios) ...
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

            // --- 3. Lógica de Cálculo de Dosis (Sin cambios) ---
            int diasTratamiento = 1;
            int unidadesPorToma = 1;
            int tomasAlDia = 1;
            int unidadesPorEnvase = 30;

            if (!string.IsNullOrWhiteSpace(firstItem.Duration))
            {
                var matchDias = System.Text.RegularExpressions.Regex.Match(firstItem.Duration, @"\d+");
                if (matchDias.Success) int.TryParse(matchDias.Groups[0].Value, out diasTratamiento);
            }
            if (!string.IsNullOrWhiteSpace(firstItem.Quantity))
            {
                var matchUnidades = System.Text.RegularExpressions.Regex.Match(firstItem.Quantity, @"\d+");
                if (matchUnidades.Success) int.TryParse(matchUnidades.Groups[0].Value, out unidadesPorToma);
            }
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

            int unidadesTotales = diasTratamiento * unidadesPorToma * tomasAlDia;
            int numEnvasesCalculado = (unidadesPorEnvase > 0) ? (int)Math.Ceiling((double)unidadesTotales / unidadesPorEnvase) : 1;
            if (numEnvasesCalculado == 0) numEnvasesCalculado = 1;
            string medicFull = firstItem.MedicationName ?? "";

            // --- 4. Rellenar PDF con iTextSharp (Sin cambios) ---
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

        // ---------------------------------------------------------------------
        // --- INICIO: MÉTODOS AUXILIARES (PRESUPUESTO) - VERSIÓN FINAL 2.0 ---
        // ---------------------------------------------------------------------

        // Estilos para la nueva tabla
        private static IContainer HeaderCellStyle(IContainer c) =>
            c.Border(1).BorderColor(ColorTableBorder).Background(ColorTableHeaderBg)
             .PaddingVertical(5).PaddingHorizontal(5).AlignCenter();

        private static IContainer BodyCellStyle(IContainer c, bool alignRight = false)
        {
            var container = c.Border(1).BorderColor(ColorTableBorder)
                             .PaddingVertical(5).PaddingHorizontal(5);

            return alignRight ? container.AlignRight() : container.AlignLeft();
        }

        private void ComposeHeader(IContainer container, Budget budget, string maskedDni)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text("PRESUPUESTO").Bold().FontSize(18).Underline();
                column.Item().PaddingBottom(20);

                string logoPath = string.Empty;
                if (!string.IsNullOrEmpty(_settings.ClinicLogoPath))
                {
                    logoPath = Path.Combine(AppContext.BaseDirectory, _settings.ClinicLogoPath);
                }

                column.Item().Row(row =>
                {
                    // --- Columna Izquierda: Datos Clínica ---
                    row.RelativeItem(1).Column(col =>
                    {
                        if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                        {
                            try
                            {
                                col.Item().MaxHeight(3.5f, Unit.Centimetre).Image(logoPath).FitArea();
                                col.Item().PaddingBottom(5);
                            }
                            catch (Exception) { /* Ignorar error de logo */ }
                        }

                        col.Item().Text(_settings.ClinicName ?? "Clínica Dental P&D").Bold().FontSize(12);
                        col.Item().Text($"CIF: {_settings.ClinicCif ?? "N/A"}");
                        col.Item().Text(_settings.ClinicAddress ?? "Dirección");
                        col.Item().Text($"Tel: {_settings.ClinicPhone ?? "N/A"}");

                        // *** CORRECCIÓN: Usando el email desde appsettings.json ***
                        if (!string.IsNullOrWhiteSpace(_settings.ClinicEmail))
                        {
                            col.Item().Text(_settings.ClinicEmail).FontColor(Colors.Blue.Medium).Underline();
                        }
                    });

                    // --- Columna Derecha: Datos Paciente ---
                    row.RelativeItem(1).Column(col =>
                    {
                        // *** CORRECCIÓN: Alineación vertical. Aumentado el padding. ***
                        // Esto empuja el bloque de paciente hacia abajo.
                        col.Item().PaddingTop(3.8f, Unit.Centimetre).Column(patientCol =>
                        {
                            // *** CORRECCIÓN: Bucle de Filas para alinear Label + Data ***
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text("Paciente:").FontSize(11).Bold();
                                r.RelativeItem().Text($"{budget.Patient?.Name ?? ""} {budget.Patient?.Surname ?? ""}")
                                                 .FontSize(11);
                            });
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text("DNI/NIF:").FontSize(11).Bold();
                                r.RelativeItem().Text(maskedDni).FontSize(11);
                            });
                            patientCol.Item().Row(r =>
                            {
                                r.ConstantItem(70).Text("Teléfono:").FontSize(11).Bold();
                                r.RelativeItem().Text(budget.Patient?.Phone ?? "N/A").FontSize(11);
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
                // --- Fila Número Presupuesto y Fecha ---
                col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingBottom(5).Row(row =>
                {
                    row.ConstantItem(100).Text("PRESUPUESTO:").Bold();
                    row.RelativeItem(2).Text(budget.BudgetNumber);

                    row.ConstantItem(50).Text("Fecha:").Bold();
                    row.RelativeItem(1).Text($"{budget.IssueDate:dd/MM/yyyy}");
                });

                col.Item().PaddingVertical(10);

                // --- Tabla de Items (Sin cambios) ---
                col.Item().Element(c => ComposeTable(c, budget));

                col.Item().PaddingVertical(10);

                // --- Totales (Sin cambios) ---
                col.Item().AlignRight().Element(c => ComposeTotals(c, budget));
            });
        }

        private void ComposeTable(IContainer container, Budget budget)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // Descripción
                    columns.RelativeColumn(1); // Cantidad
                    columns.RelativeColumn(1); // P. Unitario
                    columns.RelativeColumn(1); // Total ítem
                });

                // --- Cabecera de la Tabla ---
                table.Header(header =>
                {
                    header.Cell().Element(HeaderCellStyle).Text("Descripción");
                    header.Cell().Element(HeaderCellStyle).Text("Cantidad");
                    header.Cell().Element(HeaderCellStyle).Text("Precio Unit.");
                    header.Cell().Element(HeaderCellStyle).Text("Total ítem");
                });

                // --- Filas de Items (Dinámicas) ---
                foreach (var item in budget.Items ?? Enumerable.Empty<BudgetLineItem>())
                {
                    table.Cell().Element(c => BodyCellStyle(c)).Text(item.Description);
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text(item.Quantity.ToString());
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text($"{item.UnitPrice:N2} €");
                    table.Cell().Element(c => BodyCellStyle(c, true)).Text($"{item.LineTotal:N2} €");
                }
            });
        }

        private void ComposeTotals(IContainer container, Budget budget)
        {
            // Calculamos los valores igual que en el ViewModel/Diseño anterior
            decimal discountAmount = budget.Subtotal * (budget.DiscountPercent / 100);
            decimal baseImponible = budget.Subtotal - discountAmount;
            decimal vatAmount = baseImponible * (budget.VatPercent / 100);

            // Contenedor de la tabla de totales, alineado a la derecha
            container.Width(250, Unit.Point).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });

                // Estilo para celdas de totales (label y valor)
                static IContainer TotalsLabelCell(IContainer c) =>
                    c.Background(ColorTotalsBg).Border(1).BorderColor(ColorTableBorder)
                     .PaddingHorizontal(5).PaddingVertical(2).AlignRight();

                static IContainer TotalsValueCell(IContainer c) =>
                    c.Background(ColorTotalsBg).Border(1).BorderColor(ColorTableBorder)
                     .PaddingHorizontal(5).PaddingVertical(2).AlignRight();

                // Fila 1: Subtotal
                table.Cell().Element(TotalsLabelCell).Text("Subtotal:").Bold();
                table.Cell().Element(TotalsValueCell).Text($"{budget.Subtotal:N2} €");

                // Fila 2: Descuento
                table.Cell().Element(TotalsLabelCell).Text($"Descuento ({budget.DiscountPercent}%):").Bold();
                table.Cell().Element(TotalsValueCell).Text($"-{discountAmount:N2} €");

                // Fila 3: Base Imponible
                table.Cell().Element(TotalsLabelCell).Text("Base Imponible:").Bold();
                table.Cell().Element(TotalsValueCell).Text($"{baseImponible:N2} €");

                // Fila 4: IVA
                table.Cell().Element(TotalsLabelCell).Text($"IVA ({budget.VatPercent}%):").Bold();
                table.Cell().Element(TotalsValueCell).Text($"+{vatAmount:N2} €");

                // Fila 5: Total
                table.Cell().Element(TotalsLabelCell).Text("Total:").FontSize(12).Bold();
                table.Cell().Element(TotalsValueCell).Text($"{budget.TotalAmount:N2} €").FontSize(12).Bold();
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(10).Column(col =>
            {
                col.Item().Text("Nota: Presupuesto valido por 30 dias");
                col.Item().AlignRight().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        }

        // --- FIN: MÉTODOS AUXILIARES (PRESUPUESTO) ---
        // -------------------------------------------------------------------


        // *** CORRECCIÓN: Función MaskDni añadida de nuevo ***
        private string MaskDni(string? dniNie)
        {
            if (string.IsNullOrWhiteSpace(dniNie) || dniNie.Length < 5) return dniNie ?? string.Empty;
            // Lógica para enmascarar (Ej: 12345678Z -> 1234****Z)
            int startAsterisk = Math.Max(1, dniNie.Length - 4);
            int countAsterisk = Math.Max(0, dniNie.Length - startAsterisk - 1);
            if (countAsterisk == 0) return dniNie;
            // Asegurarnos de no enmascarar más de 4 caracteres
            countAsterisk = Math.Min(countAsterisk, 4);
            startAsterisk = dniNie.Length - countAsterisk - 1;

            return dniNie.Substring(0, startAsterisk) + new string('*', countAsterisk) + dniNie.Substring(startAsterisk + countAsterisk);
        }

        // ---------------------------------------------------------------------
        // --- INICIO: MÉTODOS AUXILIARES (RECETA) - CON CORRECCIONES ---
        // ---------------------------------------------------------------------

        private void ComposePrescriptionHeader(IContainer container, Prescription prescription)
        {
            container.Row(row =>
            {
                // Columna Izquierda: Datos del Prescriptor
                row.RelativeItem(2).Column(col =>
                {
                    col.Item().Text(_settings.ClinicName ?? "Clínica Dental P&D").Bold().FontSize(12).FontColor(Colors.Blue.Medium);
                    col.Item().Text($"Dr(a): {prescription.PrescriptorName ?? "N/A"}").FontSize(10);
                    col.Item().Text($"Especialidad: {prescription.PrescriptorSpecialty ?? "N/A"}").FontSize(10);
                    col.Item().Text($"Col. Num: {prescription.PrescriptorCollegeNum ?? "N/A"}").FontSize(10);
                });

                // Columna Derecha: Datos del Paciente
                row.RelativeItem(3).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Column(col =>
                {
                    col.Item().Text("Datos del Paciente").Bold().FontSize(11);
                    col.Item().Text($"Nombre: {prescription.Patient?.PatientDisplayInfo ?? "N/A"}").FontSize(10);
                    col.Item().Text($"Fecha: {prescription.IssueDate:dd/MM/yyyy}").AlignRight().FontSize(10);
                });
            });
            container.PaddingTop(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
        }

        private void ComposePrescriptionContent(IContainer container, Prescription prescription)
        {
            container.Column(col =>
            {
                col.Spacing(10);

                col.Item().AlignCenter().PaddingVertical(5).Text("PRESCIPCIÓN MÉDICA").Bold().FontSize(14);

                foreach (var item in prescription.Items)
                {
                    col.Item().Background(Colors.Grey.Lighten5).Padding(5).Column(itemCol =>
                    {
                        itemCol.Item().Text(item.MedicationName).Bold().FontSize(12).FontColor(Colors.Black);

                        itemCol.Item().PaddingLeft(5).Text($"Pauta: {item.DosagePauta}");
                        itemCol.Item().PaddingLeft(5).Text($"Cantidad: {item.Quantity} | Duración: {item.Duration}");
                    });
                }

                if (!string.IsNullOrWhiteSpace(prescription.Instructions))
                {
                    col.Item().PaddingTop(10).Text(text =>
                    {
                        text.Span("Instrucciones Adicionales").Bold().Underline();
                    });
                    col.Item().Text(prescription.Instructions).Italic();
                }

                col.Item().PaddingVertical(20).Text(x =>
                {
                    x.Span("Firma del Prescriptor: ").Bold();
                });
            });
        }

        private void ComposePrescriptionFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(5).AlignCenter().Text("Receta válida solo para el paciente indicado y su uso bajo supervisión médica.");
        }

        // --- FIN: MÉTODOS AUXILIARES (RECETA) ---
        // -------------------------------------------------------------------
    }
}
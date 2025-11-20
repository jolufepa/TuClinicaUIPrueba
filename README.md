🦷 TuClínica.UI - Sistema de Gestión Dental

TuClínica.UI es una aplicación de escritorio robusta y segura (WPF, .NET 8) diseñada para modernizar la administración y gestión clínica de pacientes, tratamientos y documentos en clínicas dentales.
Desarrollada bajo los principios de Clean Architecture, priorizando la seguridad, la integridad de los datos y una experiencia de usuario eficiente.

=========================
🚀 CARACTERÍSTICAS PRINCIPALES
=========================

--- GESTIÓN DE PACIENTES ---
* Ficha de Paciente Unificada: Módulo centralizado ("Dashboard") que combina datos personales, alertas médicas, odontograma y contabilidad.
* Sistema de Alertas Médicas (NUEVO): 
    * Registro de condiciones críticas (Alergias, enfermedades).
    * Niveles de severidad visuales (Crítica, Advertencia, Información).
    * Visualización persistente en la ficha del paciente para seguridad clínica.
* Documentos Vinculados (NUEVO):
    * Capacidad para adjuntar y gestionar documentos externos (PDFs, imágenes, radiografías) directamente al perfil del paciente.
    * Clasificación por tipo de documento y notas adicionales.
* Plan de Tratamiento (Tareas): Lista de tareas pendientes ("To-Do") integrada en la ficha, con indicadores visuales (badges) de tareas activas.

--- CLÍNICA Y ODONTOGRAMA ---
* Odontograma Interactivo (FDI):
    * Mapa visual puro del estado dental (Condiciones y Restauraciones).
    * Persistencia independiente: El estado visual se guarda como JSON en la base de datos, desacoplando la representación gráfica de la lógica de facturación.
    * Edición visual mediante diálogos interactivos por superficie o diente completo.
* Módulo de Recetas y Prescripciones:
    * Generación de recetas con pautas de medicación (Dosages) y duración de tratamiento.
    * Exportación a PDF usando plantillas oficiales (iTextSharp).
* Packs de Tratamiento (NUEVO):
    * Funcionalidad para crear y gestionar grupos de tratamientos predefinidos para una inserción rápida.

--- ADMINISTRACIÓN Y FINANZAS ---
* Sistema de Contabilidad (Cargos y Abonos): 
    * Separación estricta entre "Debe" (Cargos/Tratamientos realizados) y "Haber" (Pagos/Abonos).
    * Cálculo de saldo en tiempo real.
* Registro de Cargos Centralizado: Diálogo unificado para registrar tratamientos del catálogo o conceptos libres.
* Gestión de Pagos y Asignación:
    * Registro de abonos (Efectivo, Tarjeta, Transferencia) con campo de observaciones.
    * Sistema de "Allocation": Asignación manual o automática de pagos a cargos específicos para saldar deudas.
* Módulo de Presupuestos: Creación, cálculo de impuestos/descuentos y generación de PDFs profesionales (QuestPDF).
* Auditoría y Seguridad:
    * Registro de Actividad (Activity Logs): Sistema que audita cambios sensibles en la base de datos (quién hizo qué y cuándo).
    * Base de datos cifrada (SQLCipher) y contraseñas hasheadas (BCrypt).

=========================
⚙️ ARQUITECTURA Y TECNOLOGÍAS
=========================

El proyecto implementa una Arquitectura Limpia (Clean Architecture) de N-Capas con patrón MVVM:

Proyecto                Responsabilidad
--------------------    -----------------------------------------------------------------
TuClinica.UI            Capa de Presentación (WPF, XAML, ViewModels, Converters).
TuClinica.Services      Lógica de Aplicación (Auth, PDF, Backup, Validaciones, Licenciamiento).
TuClinica.DataAccess    Persistencia (Entity Framework Core 8, Repositorios, Migraciones).
TuClinica.Core          Dominio (Entidades, Interfaces, Enums, DTOs).
TuClinica.Services.Tests Pruebas Unitarias (MSTest) para asegurar la lógica de negocio.

--- Stack Tecnológico ---
* Framework: .NET 8 (LTS)
* UI: WPF con MahApps.Metro (Estilización moderna).
* MVVM: CommunityToolkit.Mvvm (Comandos Relay, Mensajería Débil, ObservableObject).
* Base de Datos: SQLite con cifrado SQLCipher.
* ORM: Entity Framework Core 8.
* Reportes PDF: QuestPDF (Presupuestos/Odontogramas) y iTextSharp (Recetas).
* Seguridad: BCrypt, AES-CBC + HMAC (Backups), RSA (Licencias), Windows DPAPI.

=========================
🛠️ EJECUCIÓN E INSTALACIÓN
=========================

1. Requisitos: Visual Studio 2022 y .NET 8 SDK.
2. Base de Datos: Al iniciar, la aplicación crea automáticamente `DentalClinic.db` (cifrada) en `%LOCALAPPDATA%/TuClinicaPD/Data`.
3. Usuario Inicial:
   * Usuario: `admin`
   * Contraseña: `admin123`
4. Licenciamiento: El sistema requiere un archivo `license.dat` firmado digitalmente (RSA) para activarse en la primera ejecución.

=========================
💡 NOTAS TÉCNICAS DEL DESARROLLADOR
=========================

* Inyección de Dependencias (DI): Se utiliza `IServiceScopeFactory` en los ViewModels Singleton (`PatientFileViewModel`, `LoginViewModel`) para crear ámbitos (scopes) seguros al acceder a la base de datos, evitando problemas de concurrencia en EF Core.
* Backups por Streaming: El servicio de Backup utiliza flujos (`FileStream`) combinados con criptografía (`CryptoStream`) para procesar archivos grandes sin cargar todo el contenido en la memoria RAM.
* Inicialización de Comandos: En ViewModels críticos que se instancian al inicio, se optó por la inicialización manual de `RelayCommand` en el constructor para evitar condiciones de carrera (Race Conditions) con el Binding de XAML.
* Compatibilidad QuestPDF: El servicio de PDF ha sido actualizado para utilizar la API moderna de QuestPDF (v2024+), reemplazando métodos obsoletos de dibujo de tablas y lienzos.
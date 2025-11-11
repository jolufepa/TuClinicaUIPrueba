```
🦷 TuClínica.UI - Sistema de Gestión Dental

TuClínica.UI es una aplicación de escritorio robusta y segura (WPF, .NET 8) diseñada para modernizar la administración y gestión clínica de pacientes, tratamientos y documentos en clínicas dentales. Desarrollada con un enfoque en la eficiencia, la arquitectura limpia y la integridad de los datos.

=========================
🚀 CARACTERÍSTICAS PRINCIPALES
=========================

* Ficha de Paciente Unificada: Módulo centralizado que combina datos personales, un odontograma visual y un sistema de contabilidad completo con pestañas dedicadas para "Historial Clínico" y "Facturación".

* Odontograma Interactivo (FDI) - ¡Refactorizado!:
    * El odontograma ahora funciona como un "mapa visual puro" del estado dental del paciente (Condiciones y Restauraciones).
    * El estado visual se guarda de forma independiente en la ficha del paciente (como JSON), desacoplando la vista de la facturación.
    * Permite marcar el estado por superficie (Caries, Sano, Fractura, Obturación, Corona, etc.) a través de un diálogo emergente.

* Sistema de Contabilidad (Cargos y Abonos): Gestión financiera profesional que separa "Cargos" (tratamientos, consultas) de "Abonos" (pagos del paciente).

* Registro de Cargos Centralizado - ¡Refactorizado!:
    * Se ha "eliminado" el registro de cargos desde el odontograma.
    * Todo el registro de cargos se centraliza en un único diálogo emergente ("Registrar Cargo/Visita").
    * Este diálogo permite registrar un cargo usando un tratamiento predefinido del catálogo (con autocompletado de precio) o introduciendo un concepto, cantidad y precio unitario manualmente.

* Registro de Pagos: Flujo de trabajo limpio para registrar abonos (efectivo, tarjeta) que quedan como "saldo a favor", ahora centralizado en el panel de "Acciones" de la ficha del paciente.

* Asignación de Pagos: Interfaz dedicada en la pestaña "Facturación" para asignar pagos no asignados a cargos pendientes de pago.

* Gestión de Saldos: Cálculo de saldo total en tiempo real y seguimiento de cargos pendientes.

* Anulación de Cargos: Funcionalidad para eliminar cargos erróneos, que anula automáticamente las asignaciones y devuelve el saldo al paciente.

* Módulo de Presupuestos: Creación de presupuestos con cálculos automáticos (IVA, descuentos) y exportación a PDF (usando QuestPDF).

* Módulo de Recetas: Prescripción de medicamentos, gestión de pautas (dosages) y fármacos, y exportación a PDF (usando plantillas iTextSharp).

* Gestión de Tratamientos: Catálogo de tratamientos con precios predeterminados.

* Gestión de Usuarios: Control de acceso basado en roles (Administrador, Doctor, Recepcionista).

* Seguridad y Auditoría (Nivel Profesional):
    * Base de Datos Cifrada: Almacenamiento local seguro usando SQLite (SQLCipher). La clave se protege con Windows DPAPI.
    * Hashing de Contraseñas: Autenticación robusta con BCrypt.
    * Backups Cifrados (Streaming): Importación/Exportación de copias de seguridad de cualquier tamaño. El sistema utiliza streaming (AES-CBC + HMAC) para evitar errores de memoria (OutOfMemoryException) con archivos grandes.
    * Sistema de Licencias: Activación por hardware (Machine ID) con firmas RSA.
    * Registro de Actividad (Logs): Auditoría automática de creación, modificación y borrado de datos sensibles (pacientes) interceptando `DbContext.SaveChangesAsync`.
    * Visor de Auditoría: Panel de administrador para la revisión y exportación de todos los logs de actividad.

=========================
⚙️ ARQUITECTURA Y TECNOLOGÍAS
=========================

El proyecto sigue una arquitectura limpia de N-Capas y el patrón MVVM (Model-View-ViewModel) para garantizar la separación de responsabilidades y la alta testabilidad.

--- Arquitectura de Capas ---

(Formateado para .txt)
Proyecto                Responsabilidad
--------------------    -----------------------------------------------------------------
TuClinica.UI            Presentación (WPF) y ViewModels. Interfaz con el usuario.
TuClinica.Services      Lógica de Negocio (Auth, Validación, PDF, Licencia, Backup, Auditoría).
TuClinica.DataAccess    Persistencia de Datos (Entity Framework Core y Repositorios).
TuClinica.Core          Contratos de Negocio (Modelos, Interfaces, Enums).
TuClinica.Services.Tests Pruebas Unitarias (MSTest & Moq) para la lógica de negocio.

* Gestión de Dependencias (DI): Sigue las mejores prácticas de DI, inyectando `IServiceScopeFactory` en servicios `Singleton` (como `AuthService` y `PatientFileViewModel`) para crear y gestionar de forma segura el ciclo de vida de los servicios `Scoped` (como `AppDbContext`).

--- Stack Tecnológico ---

(Formateado para .txt)
Componente                  Tecnología/Librería         Propósito
------------------------    -----------------------     -----------------------------------------------
Framework                   .NET 8 (WPF)                Interfaz de usuario de escritorio.
Estilo                      MahApps.Metro               Estilización moderna y controles personalizados.
Patrón                      MVVM (CommunityToolkit.Mvvm) Separación lógica de la UI.
Inyección de Dependencias   Microsoft.Extensions.Hosting Gestión del ciclo de vida de servicios (DI).
Base de Datos               SQLite (SQLCipher)          Almacenamiento local seguro y cifrado.
ORM                         Entity Framework Core 8     Mapeo Objeto-Relacional.
Generación PDF (Ptos)       QuestPDF                    Documentos "Code-First" (Presupuestos).
Generación PDF (Recetas)    iTextSharp (Plantillas)     Relleno de formularios PDF (Recetas).
Cifrado                     BCrypt (Contraseñas)        Seguridad de contraseñas.
Cifrado                     AES-CBC + HMAC              Backups por Streaming.
Cifrado                     RSA (Licencias)             Validación de licencias de software.
Testing                     MSTest & Moq                Pruebas unitarias y Mocks.

=========================
🛠️ EJECUCIÓN
=========================

--- Requisitos Previos ---

  * .NET 8 SDK
  * Visual Studio 2022

--- Primer Arranque ---

1.  Al ejecutar la aplicación por primera vez, se crearán los archivos de base de datos cifrados (`DentalClinic.db` y `db.key`) en la carpeta local de datos (`%LOCALAPPDATA%/TuClinicaPD/Data`).
2.  Se creará un usuario administrador por defecto:
      * Usuario: `admin`
      * Contraseña: `admin123`
3.  La aplicación solicitará la activación. Importa el archivo `license.dat` proporcionado por el administrador.

=========================
⚠️ NOTA IMPORTANTE DE SEGURIDAD PARA GITHUB
=========================

Este repositorio utiliza un sistema de licencias basado en un par de claves Criptográficas RSA (Pública/Privada) para generar activaciones.

  * La Clave Pública (`PublicKey`) está incrustada de forma segura dentro de `TuClinica.Services/Implementation/LicenseService.cs`. Es pública y no representa un riesgo.
  * La Clave Privada (`PrivateKey.xml`) se utiliza en el proyecto del "Generador de Licencias" (que debe mantenerse separado de este repositorio) para *firmar* y crear los archivos `.dat` de licencia.

=========================
💡 NOTAS DEL DESARROLLADOR
=========================

--- [RelayCommand] vs. Implementación Manual de ICommand ---

Durante el desarrollo, se detectó una inconsistencia en la implementación de `ICommand` en los ViewModels:

  * La mayoría de ViewModels (ej. `AdminViewModel`, `BudgetsViewModel`) usan los generadores de código modernos `[RelayCommand]` de CommunityToolkit.Mvvm.
  * El `LoginViewModel` utiliza una implementación manual (Propiedad `ICommand` + inicialización en el constructor).
  * Actualización: El `PatientFileViewModel` (un Singleton) también requiere inicialización manual de comandos por la misma razón.

Esto no es un error, es una decisión de diseño deliberada.

Ciertos ViewModels (`LoginViewModel`, `PatientFileViewModel`) se instancian como Singletons "inmediatamente" al arrancar la aplicación, al mismo tiempo que el `DataContext` se está enlazando (binding). Esto crea una "race condition" (carrera de condiciones) donde el binding del XAML (`Command="{Binding MiComando}"`) se ejecuta *antes* de que el generador `[RelayCommand]` haya tenido tiempo de crear e inicializar la propiedad del comando. El binding falla silenciosamente (el botón "no hace nada").

La solución manual (inicializar el comando *dentro* del constructor) garantiza que la propiedad del comando existe y tiene un valor asignado *antes* de que el `DataContext` se enlace al XAML, asegurando un arranque robusto. Los otros ViewModels no sufren este problema porque se crean más tarde (`Transient`) bajo demanda del usuario.

--- Antipatrón Service Locator (Solucionado) ---

Un problema arquitectónico inicial era el uso del antipatrón "Service Locator". Los servicios Singleton (como `AuthService` y `PatientFileViewModel`) inyectaban `IServiceProvider` para resolver dependencias `Scoped` (como `IUserRepository`).
SOLUCIÓN: Esto se ha refactorizado. Los Singletons ahora inyectan `IServiceScopeFactory`, que es la forma recomendada por Microsoft para crear ámbitos y resolver servicios `Scoped` de forma segura, evitando `ObjectDisposedException` y "dependencias cautivas" (captive dependencies).

--- Streaming de Backups (AES-GCM vs AES-CBC) ---

`BackupService` originalmente cargaba el archivo completo en memoria (`File.ReadAllBytesAsync`), lo que causaba un `OutOfMemoryException` con archivos grandes.
El algoritmo `AesGcm` (usado en `CryptoService`) no es compatible con `CryptoStream`.
SOLUCIÓN: `CryptoService` se ha ampliado para incluir métodos de streaming que usan `AesCbc` con `HMACSHA256` (un patrón Encrypt-then-MAC). `BackupService` ahora usa estos métodos (`EncryptAsync`/`DecryptAsync`) para serializar y encriptar/desencriptar directamente entre `FileStream` y `JsonSerializer`, proporcionando un uso de memoria constante (O(1)) sin importar el tamaño del backup.

--- Error de EF Core "already being tracked" en la Importación de Backups ---

Durante la importación, EF Core lanzaba un error "another instance with the same key value... is already being tracked".
CAUSA: La *exportación* usaba `.AsNoTracking()`. Esto hacía que EF Core creara múltiples instancias de objeto para la misma entidad (ej. un `Patient` en la lista de `Patients` y otro `Patient` idéntico en `Budget.Patient`). El serializador JSON los trataba como objetos separados.
SOLUCIÓN: Se eliminó `.AsNoTracking()` de `ExportBackupAsync`. Esto permite que el `DbContext` resuelva las identidades y el serializador JSON (`ReferenceHandler.Preserve`) cree una sola instancia de objeto y use referencias (`$ref`) para todas las demás, solucionando el error de seguimiento en la importación.

--- Advertencia de Versión de API de QuestPDF (v2022 vs v2024) ---

Durante la resolución de errores, se detectó un conflicto de API crítico en `PdfService.cs`.
El proyecto utiliza la versión moderna de QuestPDF (v2024.3.6), pero el código original para generar el odontograma estaba escrito para una API obsoleta (anterior a 2023).
Esto provocaba una cadena de errores de compilación (como `CS1061 'Container' no contiene 'Expand'` o `CS0122 'ICanvas' no es accesible`) y errores en tiempo de ejecución (`System.NotImplementedException` al usar `.Canvas()`), lo que resultaba en la generación de PDFs de odontograma en blanco.
SOLUCIÓN: El código obsoleto en `PdfService.cs` (específicamente en `ComposeOdontogramGrid` y `AddToothCell`) fue reescrito para usar la API moderna:

  * `.Grid()` fue reemplazado por `.Table()`
  * `.Expand()` fue reemplazado por `.Extend()`
  * `.Canvas()` (que está obsoleto) fue eliminado por completo.
La lógica de dibujo de superficies se reemplazó por un método `DrawSurface` que solo usa `.Background()`.
Cualquier futura actualización de QuestPDF o modificación de `PdfService.cs` debe verificar que la API utilizada para el odontograma sigue siendo compatible con la versión de la librería.
```

# 🦷 TuClínica.UI- Sistema de Gestión Dental

**TuClínica.UI** es una aplicación de escritorio robusta y segura (WPF, .NET 8) diseñada para modernizar la administración y gestión clínica de pacientes, tratamientos y documentos en clínicas dentales. Desarrollada con un enfoque en la eficiencia, la arquitectura limpia y la integridad de los datos.

## 🚀 Características Principales

  * **Ficha de Paciente Unificada:** Módulo centralizado que combina datos personales, un odontograma interactivo (Notación FDI), y un sistema de contabilidad completo.
  * **Odontograma Interactivo (FDI):** Odontograma gráfico en ventana modal que permite registrar tratamientos (cargos) directamente sobre el diente y sus superficies.
  * **Sistema de Contabilidad (Cargos y Abonos):** Gestión financiera profesional que separa "Cargos" (tratamientos, consultas) de "Abonos" (pagos del paciente).
      * **Registro de Cargos:** Creación automática de cargos desde el odontograma o manualmente (para limpiezas, consultas).
      * **Registro de Pagos:** Panel para registrar abonos (efectivo, tarjeta) que quedan como "saldo a favor".
      * **Asignación de Pagos:** Interfaz rápida para asignar pagos no asignados a cargos pendientes de pago.
      * **Gestión de Saldos:** Cálculo de saldo total en tiempo real y seguimiento de cargos pendientes.
      * **Anulación de Cargos:** Funcionalidad para eliminar cargos erróneos, que anula automáticamente las asignaciones y devuelve el saldo al paciente.
  * **Módulo de Presupuestos:** Creación de presupuestos con cálculos automáticos (IVA, descuentos) y exportación a PDF (usando **QuestPDF**).
  * **Módulo de Recetas:** Prescripción de medicamentos, gestión de pautas (dosages) y fármacos, y exportación a PDF (usando plantillas **iTextSharp**).
  * **Gestión de Tratamientos:** Catálogo de tratamientos con precios predeterminados.
  * **Gestión de Usuarios:** Control de acceso basado in roles (Administrador, Doctor, Recepcionista).
  * **Seguridad y Auditoría (Nivel Profesional):**
      * **Base de Datos Cifrada:** Almacenamiento local seguro usando **SQLite (SQLCipher)**. La clave se protege con Windows DPAPI.
      * **Hashing de Contraseñas:** Autenticación robusta con **BCrypt**.
      * **Backups Cifrados:** Importación/Exportación de copias de seguridad cifradas con **AES-GCM**.
      * **Sistema de Licencias:** Activación por hardware (Machine ID) con firmas **RSA**.
      * **Registro de Actividad (Logs):** Auditoría automática de creación, modificación y borrado de datos sensibles (pacientes) interceptando `DbContext.SaveChangesAsync`.
      * **Visor de Auditoría:** Panel de administrador para la revisión y exportación de todos los logs de actividad.

## ⚙️ Arquitectura y Tecnologías

El proyecto sigue una arquitectura limpia de N-Capas y el patrón **MVVM (Model-View-ViewModel)** para garantizar la separación de responsabilidades y la alta testabilidad.

### Arquitectura de Capas

| Proyecto | Responsabilidad |
| :--- | :--- |
| **TuClinica.UI** | Presentación (WPF) y ViewModels. Interfaz con el usuario. |
| **TuClinica.Services** | Lógica de Negocio (Auth, Validación, PDF, Licencia, Backup, Auditoría). |
| **TuClinica.DataAccess** | Persistencia de Datos (Entity Framework Core y Repositorios). |
| **TuClinica.Core** | Contratos de Negocio (Modelos, Interfaces, Enums). |
| **TuClinica.Services.Tests**| Pruebas Unitarias (MSTest & Moq) para la lógica de negocio. |

### Stack Tecnológico

| Componente | Tecnología/Librería | Propósito |
| :--- | :--- | :--- |
| **Framework** | .NET 8 (WPF) | Interfaz de usuario de escritorio. |
| **Estilo** | MahApps.Metro | Estilización moderna y controles personalizados. |
| **Patrón** | MVVM (CommunityToolkit.Mvvm) | Separación lógica de la UI. |
| **Inyección de Dependencias**| Microsoft.Extensions.Hosting | Gestión del ciclo de vida de servicios (DI). |
| **Base de Datos**| SQLite (SQLCipher) | Almacenamiento local seguro y cifrado. |
| **ORM** | Entity Framework Core 8 | Mapeo Objeto-Relacional. |
| **Generación PDF (Ptos)**| QuestPDF | Documentos "Code-First" (Presupuestos). |
| **Generación PDF (Recetas)**| iTextSharp (Plantillas) | Relleno de formularios PDF (Recetas). |
| **Cifrado** | BCrypt, AES-GCM, RSA | Seguridad de contraseñas, backups y licencias. |
| **Testing** | MSTest & Moq | Pruebas unitarias y Mocks. |

## 🛠️ Ejecución

### Requisitos Previos

  * .NET 8 SDK
  * Visual Studio 2022

### Primer Arranque

1.  Al ejecutar la aplicación por primera vez, se crearán los archivos de base de datos cifrados (`DentalClinic.db` y `db.key`) en la carpeta local de datos (`%LOCALAPPDATA%/TuClinicaPD/Data`).
2.  Se creará un usuario administrador por defecto:
      * **Usuario:** `admin`
      * **Contraseña:** `admin123`
3.  La aplicación solicitará la activación. Importa el archivo `license.dat` proporcionado por el administrador.

## ⚠️ Nota Importante de Seguridad para GitHub

Este repositorio utiliza un sistema de licencias basado en un par de claves Criptográficas RSA (Pública/Privada) para generar activaciones.

  * La **Clave Pública** (`PublicKey`) está incrustada de forma segura dentro de `TuClinica.Services/Implementation/LicenseService.cs`. Es pública y no representa un riesgo.
  * La **Clave Privada** (`PrivateKey.xml`) se utiliza en el proyecto del **Generador de Licencias** (que debe mantenerse separado de este repositorio) para *firmar* y crear los archivos `.dat` de licencia.

## 💡 Notas del Desarrollador

### [RelayCommand] vs. Implementación Manual de ICommand

Durante el desarrollo, se detectó una inconsistencia en la implementación de `ICommand` en los ViewModels:

  * La mayoría de ViewModels (ej. `AdminViewModel`, `BudgetsViewModel`) usan los generadores de código modernos `[RelayCommand]` de CommunityToolkit.Mvvm.
  * El `LoginViewModel` utiliza una implementación manual (Propiedad `ICommand` + inicialización en el constructor).
  * **Actualización:** El `PatientFileViewModel` (un Singleton) también requiere inicialización manual de comandos por la misma razón.

**Esto no es un error, es una decisión de diseño deliberada.**

Ciertos ViewModels (`LoginViewModel`, `PatientFileViewModel`) se instancian como Singletons **inmediatamente** al arrancar la aplicación, al mismo tiempo que el `DataContext` se está enlazando (binding). Esto crea una *race condition* (carrera de condiciones) donde el binding del XAML (`Command="{Binding MiComando}"`) se ejecuta *antes* de que el generador `[RelayCommand]` haya tenido tiempo de crear e inicializar la propiedad del comando. El binding falla silenciosamente (el botón "no hace nada").

La **solución manual** (inicializar el comando *dentro* del constructor) garantiza que la propiedad del comando existe y tiene un valor asignado *antes* de que el `DataContext` se enlace al XAML, asegurando un arranque robusto. Los otros ViewModels no sufren este problema porque se crean más tarde (`Transient`) bajo demanda del usuario.

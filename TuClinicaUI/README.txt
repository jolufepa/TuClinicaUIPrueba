# 🦷 TuClínica.UI - Sistema de Gestión para Clínicas Dentales

**TuClínica.UI** es una aplicación de escritorio robusta y segura diseñada para modernizar la administración y gestión clínica de pacientes, presupuestos y recetas en clínicas dentales. Desarrollada con un enfoque en la eficiencia, la integridad de los datos y el cumplimiento normativo.

---

## 🚀 Características Principales

El sistema ofrece una gestión completa de las operaciones clínicas y administrativas:

* **Gestión de Pacientes (CRUD):** Fichas de pacientes detalladas, incluyendo la funcionalidad de archivo (soft-delete) para mantener la historia clínica. Navegación unificada para una experiencia de usuario fluida.
* **Gestión de Usuarios:** Control de acceso basado en roles definidos (Administrador, Doctor, Recepcionista).
* **Gestión de Tratamientos:** Catálogo de tratamientos con precios predeterminados y estado de actividad.
* **Generación de Presupuestos:** Creación de presupuestos detallados con cálculo automático de subtotales, descuentos e IVA.
    * **Documentación Profesional:** Exportación inmediata de presupuestos en formato PDF (usando **QuestPDF**).
* **Gestión de Recetas:** Módulo especializado para la prescripción de medicamentos, incluyendo gestión de pautas y fármacos.
    * **Documentación Específica:** Generación de Recetas en formato PDF utilizando una plantilla base (**iTextSharp**).
* **Seguridad y Cumplimiento (LOPD/RGPD):**
    * **Auditoría de Pacientes:** Registro automático de actividades (Crear, Modificar, Borrar) sobre datos de pacientes interceptando `DbContext.SaveChangesAsync`.
    * **Registro de Acceso:** Log de consultas (Lectura) a fichas de pacientes y listados.
    * **Visor de Auditoría:** Panel de administrador para la revisión de todos los logs de actividad.
    * **Gestión de Logs:**
    * **Cifrado de Base de Datos:** Almacenamiento local seguro usando **SQLite (SQLCipher)**. La clave de cifrado se protege mediante `ProtectedData` (Windows DPAPI).
    * **Hashing de Contraseñas:** Autenticación de usuarios con hashing de contraseñas robusto (**BCrypt**).
    * **Gestión de Licencias:** Sistema de activación basado en ID de hardware (CPU + Placa Base) y licencias firmadas con **RSA**.
* **Mantenimiento de Datos:**
    * Funcionalidades de **Exportación e Importación** de copias de seguridad de la base de datos.
    * Las copias se cifran con criptografía moderna (**AES-GCM**) y claves derivadas con PBKDF2 (`Rfc2898DeriveBytes`).
* **Protocolo de Seguridad:** Incluye procedimientos definidos para la actuación ante brechas de seguridad, cumpliendo con los requisitos de notificación a la AEPD (Agencia Española de Protección de Datos) en menos de 72 horas.

---

## ⚙️ Estructura y Tecnologías

El proyecto se adhiere al patrón de diseño **MVVM (Model-View-ViewModel)** y sigue una arquitectura limpia de N-Capas para garantizar la separación de responsabilidades y la alta testabilidad.

### Arquitectura de Capas

| Proyecto | Responsabilidad |
| :--- | :--- |
| **TuClinica.UI** | Presentación (WPF) y ViewModels. Interfaz con el usuario. |
| **TuClinica.Services** | Lógica de Negocio (Auth, Validación, PDF, Licencia, Backup, Auditoría). |
| **TuClinica.DataAccess** | Persistencia de Datos (Entity Framework Core y Repositorios). |
| **TuClinica.Core** | Contratos de Negocio (Modelos, Interfaces, Enums). |

### Stack Tecnológico

| Componente | Tecnología/Librería | Propósito |
| :--- | :--- | :--- |
| **Frontend** | WPF (.NET 8) | Interfaz de usuario de escritorio. |
| **Estilo** | MahApps.Metro | Estilización moderna y controles personalizados. |
| **Base de Datos**| SQLite (SQLCipher) | Almacenamiento local seguro y cifrado de datos. |
| **ORM** | Entity Framework Core 8 | Mapeo Objeto-Relacional. |
| **Generación PDF (Ptos)**| QuestPDF | Documentos "Code-First" (Presupuestos). |
| **Generación PDF (Recetas)**| iTextSharp (Plantillas) | Relleno de formularios PDF (Recetas). |
| **Patrón** | MVVM (CommunityToolkit.Mvvm) | Separación lógica de la UI. |
| **Dependencias** | Microsoft.Extensions.Hosting | Inyección de Dependencias (DI). |
| **Cifrado** | BCrypt & AES-GCM | Seguridad de contraseñas y Backups. |

---

## 🛠️ Instalación y Configuración

### Requisitos Previos

* .NET 8 SDK
* Visual Studio 2022 (o superior)

### Ejecución

1.  **Clonar el repositorio:** (Asumido)
2.  **Configurar la DB:** En el primer arranque, la aplicación ejecutará las migraciones de Entity Framework Core para crear la base de datos cifrada (`DentalClinic.db`) y generará una clave de cifrado (`db.key`) de forma local.
3.  **Usuario Inicial:** El sistema creará automáticamente un usuario administrador por defecto:
    * **Usuario:** `admin`
    * **Contraseña:** `admin123`
4.  **Activación de Licencia:** La aplicación requerirá la activación. Copie el **Machine ID** que se muestra y solicite un archivo `license.dat` para importarlo.

### Notas del Desarrollador

* El sistema utiliza un `ViewModel` singleton (`PatientFileViewModel`) para la Ficha de Paciente, permitiendo una navegación fluida entre la lista principal y los detalles del paciente sin recargar datos.
* Los presupuestos y recetas se guardan en carpetas separadas (`/presupuestos` y `/recetas`) dentro del directorio local de datos de la aplicación (`%LOCALAPPDATA%/TuClinicaPD/Data/`).
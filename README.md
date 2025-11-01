# 🦷 TuClínica.UI - Sistema de Gestión para Clínicas Dentales

**TuClínica P&D** es una aplicación de escritorio robusta y segura diseñada para modernizar la administración y gestión clínica de pacientes, tratamientos y documentos en clínicas dentales. Desarrollada con un enfoque en la eficiencia y la integridad de los datos.

---

## 🚀 Características Principales

El sistema ofrece una gestión completa de las operaciones clínicas y administrativas:

* **Gestión de Pacientes (CRUD):** Fichas de pacientes detalladas, incluyendo la funcionalidad de archivo (soft-delete) para mantener la historia clínica.
* **Gestión de Usuarios:** Roles de acceso definidos (Administrador, Doctor, Recepcionista) y control de actividad.
* **Gestión de Tratamientos:** Catálogo de tratamientos con precios predeterminados y estado de actividad.
* **Generación de Presupuestos:** Creación de presupuestos detallados con cálculo automático de subtotales, descuentos e IVA.
    * **Documentación Profesional:** Exportación inmediata de presupuestos en formato PDF (usando QuestPDF).
* **Generación de Recetas (Nuevo):** Módulo especializado para la prescripción de medicamentos.
    * **Documentación Específica:** Generación de Recetas en formato PDF utilizando una plantilla base (**iTextSharp/PDF Forms**), asegurando el cumplimiento de los estándares de prescripción.
* **Seguridad y Auditoría:** Autenticación de usuarios con hashing de contraseñas y sistema de gestión de licencias basado en ID de hardware.
* **Mantenimiento de Datos:** Funcionalidades de Exportación e Importación de copias de seguridad de la base de datos (**cifradas** con AES-GCM) para la recuperación ante desastres.

---

## ⚙️ Estructura y Tecnologías

El proyecto se adhiere al patrón de diseño **MVVM (Model-View-ViewModel)** y sigue una arquitectura limpia de N-Capas para garantizar la separación de responsabilidades y la alta testabilidad.

### Arquitectura de Capas

| Proyecto | Responsabilidad |
| :--- | :--- |
| **TuClinica.UI** | Presentación (WPF) y ViewModels. Interfaz con el usuario. |
| **TuClinica.Services** | Lógica de Negocio (Auth, Validación, PDF, Licencia, Backup). |
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
| **Patrón** | MVVM (Manual/Community Toolkit) | Separación lógica de la UI. |
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

* Todos los ViewModels clave han sido refactorizados a la **implementación manual de ICommand** para asegurar la estabilidad del *DataBinding* en WPF y evitar conflictos de reflexión con los generadores de código.
* Los presupuestos y recetas se guardan en carpetas separadas (`/presupuestos` y `/recetas`) dentro del directorio local de datos de la aplicación.# TuClinica
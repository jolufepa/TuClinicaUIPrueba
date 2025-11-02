TuClínica.UI - Sistema de Gestión Dental
========================================

TuClínica.UI es una aplicación de escritorio robusta y segura (WPF, .NET 8) diseñada para modernizar la administración y gestión clínica de pacientes, tratamientos y documentos en clínicas dentales. Desarrollada con un enfoque en la eficiencia, la arquitectura limpia y la integridad de los datos.

--- Características Principales ---

* Gestión de Pacientes (CRUD): Fichas de pacientes detalladas, historial, y funcionalidad de archivo (soft-delete) para mantener la historia clínica.
* Módulo de Presupuestos: Creación de presupuestos con cálculos automáticos (IVA, descuentos) y exportación a PDF (usando QuestPDF).
* Módulo de Recetas: Prescripción de medicamentos, gestión de pautas (dosages) y fármacos, y exportación a PDF (usando plantillas iTextSharp).
* Gestión de Tratamientos: Catálogo de tratamientos con precios predeterminados.
* Gestión de Usuarios: Control de acceso basado in roles (Administrador, Doctor, Recepcionista).
* Seguridad y Auditoría (Nivel Profesional):
    * Base de Datos Cifrada: Almacenamiento local seguro usando SQLite (SQLCipher). La clave se protege con Windows DPAPI.
    * Hashing de Contraseñas: Autenticación robusta con BCrypt.
    * Backups Cifrados: Importación/Exportación de copias de seguridad cifradas con AES-GCM.
    * Sistema de Licencias: Activación por hardware (Machine ID) con firmas RSA.
    * Registro de Actividad (Logs): Auditoría automática de creación, modificación y borrado de datos sensibles (pacientes) interceptando DbContext.SaveChangesAsync.
    * Visor de Auditoría: Panel de administrador para la revisión y exportación de todos los logs de actividad.

--- Arquitectura y Tecnologías ---

El proyecto sigue una arquitectura limpia de N-Capas y el patrón MVVM (Model-View-ViewModel) para garantizar la separación de responsabilidades y la alta testabilidad.

Arquitectura de Capas
-----------------------
Proyecto: TuClinica.UI
Responsabilidad: Presentación (WPF) y ViewModels. Interfaz con el usuario.

Proyecto: TuClinica.Services
Responsabilidad: Lógica de Negocio (Auth, Validación, PDF, Licencia, Backup, Auditoría).

Proyecto: TuClinica.DataAccess
Responsabilidad: Persistencia de Datos (Entity Framework Core y Repositorios).

Proyecto: TuClinica.Core
Responsabilidad: Contratos de Negocio (Modelos, Interfaces, Enums).

Proyecto: TuClinica.Services.Tests
Responsabilidad: Pruebas Unitarias (MSTest & Moq) para la lógica de negocio.

Stack Tecnológico
------------------
Componente: Framework
Tecnología/Librería: .NET 8 (WPF)
Propósito: Interfaz de usuario de escritorio.

Componente: Estilo
Tecnología/Librería: MahApps.Metro
Propósito: Estilización moderna y controles personalizados.

Componente: Patrón
Tecnología/Librería: MVVM (CommunityToolkit.Mvvm)
Propósito: Separación lógica de la UI.

Componente: Inyección de Dependencias
Tecnología/Librería: Microsoft.Extensions.Hosting
Propósito: Gestión del ciclo de vida de servicios (DI).

Componente: Base de Datos
Tecnología/Librería: SQLite (SQLCipher)
Propósito: Almacenamiento local seguro y cifrado.

Componente: ORM
Tecnología/Librería: Entity Framework Core 8
Propósito: Mapeo Objeto-Relacional.

Componente: Generación PDF (Ptos)
Tecnología/Librería: QuestPDF
Propósito: Documentos "Code-First" (Presupuestos).

Componente: Generación PDF (Recetas)
Tecnología/Librería: iTextSharp (Plantillas)
Propósito: Relleno de formularios PDF (Recetas).

Componente: Cifrado
Tecnología/Librería: BCrypt, AES-GCM, RSA
Propósito: Seguridad de contraseñas, backups y licencias.

Componente: Testing
Tecnología/Librería: MSTest & Moq
Propósito: Pruebas unitarias y Mocks.

--- Ejecución ---

Requisitos Previos
* .NET 8 SDK
* Visual Studio 2022

Primer Arranque
1. Al ejecutar la aplicación por primera vez, se crearán los archivos de base de datos cifrados (DentalClinic.db y db.key) en la carpeta local de datos (%LOCALAPPDATA%/TuClinicaPD/Data).
2. Se creará un usuario administrador por defecto:
    * Usuario: admin
    * Contraseña: admin123
3. La aplicación solicitará la activación. Importa el archivo license.dat proporcionado por el administrador.

--- Nota Importante de Seguridad para GitHub ---

Este repositorio utiliza un sistema de licencias basado en un par de claves Criptográficas RSA (Pública/Privada) para generar activaciones.

* La Clave Pública (PublicKey) está incrustada de forma segura dentro de TuClinica.Services/Implementation/LicenseService.cs. Es pública y no representa un riesgo.
* La Clave Privada (PrivateKey.xml) se utiliza en el proyecto del Generador de Licencias (que debe mantenerse separado de este repositorio) para *firmar* y crear los archivos .dat de licencia.



[RelayCommand] vs. Implementación Manual de ICommand
---------------------------------------------------

Durante el desarrollo, se detectó una inconsistencia en la implementación de ICommand en los ViewModels:

* La mayoría de ViewModels (ej. AdminViewModel, BudgetsViewModel) usan los generadores de código modernos [RelayCommand] de CommunityToolkit.Mvvm.
* El LoginViewModel utiliza una implementación manual (Propiedad ICommand + inicialización en el constructor).

Esto no es un error, es una decisión de diseño deliberada.

El LoginViewModel se instancia inmediatamente al arrancar la aplicación, al mismo tiempo que el DataContext de LoginWindow se está enlazando (binding). Esto crea una "race condition" (carrera de condiciones) donde el binding del XAML (Command="{Binding LoginAsyncCommand}") se ejecuta *antes* de que el generador [RelayCommand] haya tenido tiempo de crear e inicializar la propiedad del comando. El binding falla silenciosamente.

La solución manual (inicializar el comando *dentro* del constructor) garantiza que la propiedad LoginAsyncCommand existe y tiene un valor asignado *antes* de que el DataContext se enlace al XAML, asegurando un arranque robusto. Los otros ViewModels no sufren este problema porque se crean más tarde, bajo demanda del usuario.
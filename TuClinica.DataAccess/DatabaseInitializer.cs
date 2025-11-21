using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using TuClinica.Core.Enums;
using TuClinica.Core.Models;

namespace TuClinica.DataAccess
{
    public class DatabaseInitializer
    {
        private readonly AppDbContext _context;

        public DatabaseInitializer(AppDbContext context)
        {
            _context = context;
        }

        public void Initialize()
        {
            // 1. Aplicar Migraciones (Crea tablas si no existen, o actualiza si son viejas)
            // Esto es lo que evita el error "No such table"
            _context.Database.Migrate();

            // 2. Seed inicial (Admin)
            // Esto asegura el "Inicio Seguro" creando el usuario por defecto
            if (!_context.Users.Any())
            {
                _context.Users.Add(new User
                {
                    Username = "admin",
                    // Nota: En un entorno real, idealmente inyectarías el servicio de hash,
                    // pero para el arranque crítico esto es aceptable si la librería BCrypt está disponible.
                    HashedPassword = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = UserRole.Administrador,
                    IsActive = true,
                    Name = "Administrador Sistema",
                    Specialty = "Sistemas"
                });
                _context.SaveChanges();
            }
        }
    }
}
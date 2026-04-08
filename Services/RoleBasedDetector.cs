using System;
using System.Collections.Generic;
using System.Linq;

namespace EmailValidatorPRO.Services
{
    /// <summary>
    /// Detecta emails basados en roles (info@, admin@, support@, etc).
    /// Estos emails no pertenecen a una persona real sino a un departamento.
    /// Son utiles para marketing B2B pero menos valiosos para campañas personalizadas.
    /// </summary>
    public class RoleBasedDetector
    {
        private static readonly Dictionary<string, string> KnownRoles = new(StringComparer.OrdinalIgnoreCase)
        {
            { "info", "Informacion" },
            { "admin", "Administracion" },
            { "support", "Soporte" },
            { "help", "Ayuda" },
            { "contact", "Contacto" },
            { "sales", "Ventas" },
            { "marketing", "Marketing" },
            { "billing", "Facturacion" },
            { "finance", "Finanzas" },
            { "hr", "Recursos Humanos" },
            { "recursos humanos", "Recursos Humanos" },
            { "legal", "Legal" },
            { "abuse", "Abuso" },
            { "postmaster", "Postmaster" },
            { "webmaster", "Webmaster" },
            { "hostmaster", "Hostmaster" },
            { "noreply", "No Responder" },
            { "no-reply", "No Responder" },
            { "donotreply", "No Responder" },
            { "do-not-reply", "No Responder" },
            { "newsletter", "Newsletter" },
            { "news", "Noticias" },
            { "press", "Prensa" },
            { "media", "Medios" },
            { "pr", "Relaciones Publicas" },
            { "jobs", "Empleos" },
            { "careers", "Empleos" },
            { "empleos", "Empleos" },
            { "test", "Testing" },
            { "dev", "Desarrollo" },
            { "developer", "Desarrollo" },
            { "devops", "DevOps" },
            { "it", "TI" },
            { "tech", "Tecnologia" },
            { "ops", "Operaciones" },
            { "office", "Oficina" },
            { "reception", "Recepcion" },
            { "secretaria", "Secretaria" },
            { "directory", "Directorio" },
            { "staff", "Personal" },
            { "team", "Equipo" },
            { "root", "Root/Admin" },
            { "system", "Sistema" },
            { "server", "Servidor" },
            { "backup", "Backup" },
            { "security", "Seguridad" },
            { "spam", "Spam" },
            { "junk", "Junk" },
            { "trash", "Papelera" },
            { "ceo", "CEO" },
            { "cfo", "CFO" },
            { "cto", "CTO" },
            { "coo", "COO" },
            { "cmo", "CMO" },
            { "ciso", "CISO" },
            { "director", "Director" },
            { "manager", "Manager" },
            { "inbox", "Bandeja General" },
            { "hello", "Saludo" },
            { "hi", "Saludo" },
            { "welcome", "Bienvenida" },
            { "feedback", "Feedback" },
            { "complaints", "Quejas" },
            { "queries", "Consultas" },
            { "orders", "Pedidos" },
            { "shipping", "Envios" },
            { "returns", "Devoluciones" },
            { "service", "Servicio al Cliente" },
            { "customer", "Servicio al Cliente" },
            { "tickets", "Tickets" },
            { "subscribe", "Suscripcion" },
            { "unsubscribe", "Desuscripcion" },
            { "verify", "Verificacion" },
            { "confirm", "Confirmacion" },
            { "activate", "Activacion" },
            { "register", "Registro" },
            { "notification", "Notificaciones" },
            { "alert", "Alertas" },
            { "monitor", "Monitoreo" },
            { "log", "Logs" },
            { "ftp", "FTP" },
            { "mail", "Mail General" },
            { "email", "Email General" },
            { "blog", "Blog" },
            { "social", "Redes Sociales" },
            { "comunicacion", "Comunicacion" },
            { "contacto", "Contacto" },
            { "soporte", "Soporte" },
            { "ventas", "Ventas" },
            { "contabilidad", "Contabilidad" },
            { "sistemas", "Sistemas" },
            { "redes", "Redes" }
        };

        /// <summary>
        /// Detecta si el email es role-based.
        /// Devuelve (isRoleBased, roleDescription).
        /// </summary>
        public (bool IsRoleBased, string? RoleType) Detect(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return (false, null);

            var localPart = email.Split('@')[0].ToLowerInvariant();

            // Remover puntos y guiones para comparacion
            var normalized = localPart.Replace(".", "").Replace("-", "").Replace("_", "");

            foreach (var role in KnownRoles)
            {
                if (string.Equals(normalized, role.Key.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
                {
                    return (true, role.Value);
                }
            }

            // Verificar prefijos comunes: info.xxx@, support.xxx@
            foreach (var role in KnownRoles.Keys)
            {
                if (normalized.StartsWith(role) && normalized.Length > role.Length)
                {
                    var suffix = normalized.Substring(role.Length);
                    if (suffix.Length <= 5) // info2, info123, etc
                    {
                        return (true, KnownRoles[role]);
                    }
                }
            }

            return (false, null);
        }

        /// <summary>
        /// Devuelve la lista completa de roles conocidos.
        /// </summary>
        public int GetTotalKnownRoles() => KnownRoles.Count;
    }
}

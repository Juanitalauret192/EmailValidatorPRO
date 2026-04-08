using System;

namespace EmailValidatorPRO.Models
{
    public class EmailResult
    {
        public string Email { get; set; } = string.Empty;
        public EmailStatus Status { get; set; } = EmailStatus.Pending;

        // --- Propiedades derivadas que usa el XAML ---
        public string Domain => Email.Contains('@') ? Email.Split('@')[1] : string.Empty;

        public string? Error
        {
            get
            {
                if (!string.IsNullOrEmpty(SmtpError)) return SmtpError;
                if (!string.IsNullOrEmpty(MxError)) return MxError;
                if (!string.IsNullOrEmpty(FormatError)) return FormatError;
                if (IsSuspicious) return SuspiciousReason;
                if (Status == EmailStatus.Invalid) return "Formato inválido";
                return null;
            }
        }

        public string? Reason
        {
            get
            {
                if (IsCatchAll) return CatchAllReason ?? "Catch-all detectado";
                if (IsSuspicious) return SuspiciousReason ?? "Sospechoso";
                if (IsRoleBased) return $"Role-based: {RoleType}";
                return null;
            }
        }

        public string? MxRecord
        {
            get
            {
                if (MxRecords != null && MxRecords.Length > 0)
                    return string.Join(", ", MxRecords);
                return MxError ?? "Sin registros MX";
            }
        }

        public string Role => RoleType ?? string.Empty;

        // --- Resultados de cada verificacion ---
        public bool IsFormatValid { get; set; }
        public string? FormatError { get; set; }

        public bool IsDisposable { get; set; }
        public string? DisposableReason { get; set; }

        public bool HasMxRecords { get; set; }
        public string? MxError { get; set; }
        public string[]? MxRecords { get; set; }

        public bool SmtpChecked { get; set; }
        public bool SmtpAccepted { get; set; }
        public string? SmtpError { get; set; }
        public string? SmtpResponse { get; set; }

        public bool IsSuspicious { get; set; }
        public string? SuspiciousReason { get; set; }

        // --- Catch-all ---
        public bool IsCatchAll { get; set; }
        public string? CatchAllReason { get; set; }
        public bool CatchAllChecked { get; set; }

        // --- Role-based ---
        public bool IsRoleBased { get; set; }
        public string? RoleType { get; set; }

        // --- Scoring ---
        public int Score { get; set; }
        public string ScoreLabel
        {
            get
            {
                if (Score >= 90) return "Excelente";
                if (Score >= 70) return "Bueno";
                if (Score >= 50) return "Regular";
                if (Score >= 30) return "Bajo";
                return "Malo";
            }
        }

        // --- Metadatos ---
        public TimeSpan? ValidationDuration { get; set; }
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;

        public string GetSummary()
        {
            var parts = new System.Collections.Generic.List<string>();

            parts.Add($"Formato: {(IsFormatValid ? "OK" : FormatError ?? "Invalido")}");
            parts.Add($"Desechable: {(IsDisposable ? DisposableReason ?? "Si" : "No")}");
            parts.Add($"MX: {(HasMxRecords ? "OK" : MxError ?? "No")}");

            if (SmtpChecked)
                parts.Add($"SMTP: {(SmtpAccepted ? "OK" : SmtpError ?? "Rechazado")}");

            if (CatchAllChecked)
                parts.Add($"Catch-all: {(IsCatchAll ? "SI" : "No")}");

            if (IsRoleBased)
                parts.Add($"Role: {RoleType}");

            if (IsSuspicious)
                parts.Add($"Sospechoso: {SuspiciousReason}");

            parts.Add($"Score: {Score}/100 ({ScoreLabel})");

            return string.Join(" | ", parts);
        }
    }
}
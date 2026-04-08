using EmailValidatorPRO.Models;

namespace EmailValidatorPRO.Services
{
    /// <summary>
    /// Calcula un score de calidad para cada email (0-100).
    /// Este score se usa como valor de marketing y para filtrar.
    /// 
    /// Criterios:
    ///   +30  Formato valido
    ///   +20  Tiene DNS MX
    ///   +25  SMTP aceptado
    ///   +10  No es catch-all
    ///   -15  Es role-based
    ///   -30  Es disposable
    ///   -20  Es sospechoso
    ///   -10  TLD sospechoso
    ///   +5   SmtpChecked pero sin greylisting
    ///   -50  SMTP rechazado explicitamente
    /// </summary>
    public class EmailScoringService
    {
        private static readonly string[] SuspiciousTlds =
        {
            ".tk", ".ml", ".ga", ".cf", ".gq", ".xyz", ".top", ".work",
            ".click", ".link", ".info", ".online", ".site", ".club", ".icu"
        };

        public int CalculateScore(EmailResult result)
        {
            int score = 0;

            // --- Positivos ---
            if (result.IsFormatValid)
                score += 30;

            if (result.HasMxRecords)
                score += 20;

            if (result.SmtpAccepted)
                score += 25;

            // Catch-all verificado como NO catch-all
            if (result.CatchAllChecked && !result.IsCatchAll)
                score += 10;

            // SMTP verificado sin problemas
            if (result.SmtpChecked && result.SmtpAccepted && !result.IsCatchAll)
                score += 5;

            // --- Negativos ---
            if (result.IsDisposable)
                score -= 30;

            if (result.IsRoleBased)
                score -= 15;

            if (result.IsSuspicious)
                score -= 20;

            // SMTP rechazado
            if (result.SmtpChecked && !result.SmtpAccepted)
                score -= 50;

            // TLD sospechoso
            if (HasSuspiciousTld(result.Email))
                score -= 10;

            // Greylisting (arriesgado pero no culpable)
            if (result.SmtpError?.Contains("450") == true ||
                result.SmtpError?.Contains("451") == true ||
                result.SmtpError?.Contains("Greylisting") == true)
                score -= 5;

            // Clamp entre 0 y 100
            return System.Math.Clamp(score, 0, 100);
        }

        private bool HasSuspiciousTld(string email)
        {
            var domain = email.Contains('@') ? email.Split('@')[1] : email;
            foreach (var tld in SuspiciousTlds)
            {
                if (domain.EndsWith(tld, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Retorna una descripcion del porque el score bajo.
        /// </summary>
        public string GetScoreExplanation(EmailResult result)
        {
            var reasons = new System.Collections.Generic.List<string>();

            if (!result.IsFormatValid)
                reasons.Add("Formato invalido (-30)");

            if (!result.HasMxRecords)
                reasons.Add("Sin DNS MX (-20)");

            if (result.IsDisposable)
                reasons.Add("Dominio desechable (-30)");

            if (result.IsRoleBased)
                reasons.Add($"Email role-based: {result.RoleType} (-15)");

            if (result.IsSuspicious)
                reasons.Add($"Sospechoso: {result.SuspiciousReason} (-20)");

            if (result.SmtpChecked && !result.SmtpAccepted)
                reasons.Add("SMTP rechazado (-50)");

            if (result.IsCatchAll)
                reasons.Add("Dominio catch-all (-10)");

            if (HasSuspiciousTld(result.Email))
                reasons.Add("TLD sospechoso (-10)");

            if (result.SmtpAccepted)
            {
                reasons.Add("SMTP aceptado (+25)");
                if (!result.IsCatchAll)
                    reasons.Add("No es catch-all (+10)");
            }

            return reasons.Count > 0
                ? string.Join(" | ", reasons)
                : "Email optimo";
        }
    }
}

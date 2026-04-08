using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using EmailValidatorPRO.Models;

namespace EmailValidatorPRO.Services
{
    public class ExportService
    {
        public void ExportToCsv(string filePath, IEnumerable<EmailResult> results, bool validOnly = false)
        {
            var data = validOnly
                ? results.Where(r => r.Status == EmailStatus.Valid)
                : results;

            var sb = new StringBuilder();
            sb.AppendLine("Email,Estado,Formato Valid,Desechable,Tiene MX,SMTP Verificado,SMTP Aceptado,Sospechoso,Duracion(ms),Fecha");

            foreach (var r in data)
            {
                var email = EscapeCsvField(r.Email);
                sb.AppendLine($"{email},{r.Status},{r.IsFormatValid},{r.IsDisposable},{r.HasMxRecords},{r.SmtpChecked},{r.SmtpAccepted},{r.IsSuspicious},{(int?)(r.ValidationDuration?.TotalMilliseconds)},{r.ValidatedAt:yyyy-MM-dd HH:mm:ss}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToTxt(string filePath, IEnumerable<EmailResult> results, bool validOnly = false)
        {
            var data = validOnly
                ? results.Where(r => r.Status == EmailStatus.Valid)
                : results;

            var lines = data.Select(r => r.Email);
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        public void ExportToJson(string filePath, IEnumerable<EmailResult> results, bool validOnly = false)
        {
            var data = validOnly
                ? results.Where(r => r.Status == EmailStatus.Valid)
                : results;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(data.ToList(), options);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        private static string EscapeCsvField(string field)
        {
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}

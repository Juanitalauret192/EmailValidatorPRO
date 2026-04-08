using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using EmailValidatorPRO.Models;

namespace EmailValidatorPRO.Services
{
    public class ExcelExportService
    {
        public void ExportToExcel(string filePath, IEnumerable<EmailResult> results, bool validOnly = false)
        {
            var data = validOnly
                ? results.Where(r => r.Status == EmailStatus.Valid).ToList()
                : results.ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Emails");

            // --- Header style ---
            var headerStyle = ws.Style;
            headerStyle.Font.Bold = true;
            headerStyle.Font.FontColor = XLColor.White;
            headerStyle.Fill.BackgroundColor = XLColor.FromArgb(0x23, 0x86, 0x36);
            headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerStyle.Border.BottomBorder = XLBorderStyleValues.Medium;

            // --- Headers ---
            var headers = new[] { "#", "Email", "Estado", "Score", "Formato OK", "Desechable",
                "Tiene MX", "SMTP OK", "SMTP Error", "Catch-All", "Role-Based",
                "Sospechoso", "Duracion (ms)", "Fecha" };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }

            // Apply header style
            ws.Range(1, 1, 1, headers.Length).Style = headerStyle;

            // --- Data rows ---
            for (int i = 0; i < data.Count; i++)
            {
                var r = data[i];
                var row = i + 2;

                ws.Cell(row, 1).Value = i + 1;
                ws.Cell(row, 2).Value = r.Email;
                ws.Cell(row, 3).Value = r.Status.ToString();
                ws.Cell(row, 4).Value = r.Score;
                ws.Cell(row, 5).Value = r.IsFormatValid ? "Si" : "No";
                ws.Cell(row, 6).Value = r.IsDisposable ? r.DisposableReason ?? "Si" : "No";
                ws.Cell(row, 7).Value = r.HasMxRecords ? "Si" : "No";
                ws.Cell(row, 8).Value = r.SmtpAccepted ? "Si" : (r.SmtpChecked ? "No" : "N/A");
                ws.Cell(row, 9).Value = r.SmtpError ?? "";
                ws.Cell(row, 10).Value = r.IsCatchAll ? "Si" : "No";
                ws.Cell(row, 11).Value = r.IsRoleBased ? r.RoleType ?? "Si" : "No";
                ws.Cell(row, 12).Value = r.IsSuspicious ? r.SuspiciousReason ?? "Si" : "No";
                ws.Cell(row, 13).Value = (int?)(r.ValidationDuration?.TotalMilliseconds);
                ws.Cell(row, 14).Value = r.ValidatedAt.ToString("yyyy-MM-dd HH:mm:ss");

                // Color por status
                var rowRange = ws.Range(row, 1, row, headers.Length);
                switch (r.Status)
                {
                    case EmailStatus.Valid:
                        rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x0D, 0x28, 0x18);
                        break;
                    case EmailStatus.Invalid:
                        rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x30, 0x0A, 0x0A);
                        break;
                    case EmailStatus.Risky:
                        rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x30, 0x25, 0x0A);
                        break;
                    case EmailStatus.Disposable:
                        rowRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1A, 0x14, 0x30);
                        break;
                }

                // Color del score
                var scoreCell = ws.Cell(row, 4);
                if (r.Score >= 90) scoreCell.Style.Font.FontColor = XLColor.FromArgb(0x23, 0x86, 0x36);
                else if (r.Score >= 70) scoreCell.Style.Font.FontColor = XLColor.FromArgb(0x58, 0xA6, 0xFF);
                else if (r.Score >= 50) scoreCell.Style.Font.FontColor = XLColor.FromArgb(0xD2, 0x99, 0x22);
                else if (r.Score >= 30) scoreCell.Style.Font.FontColor = XLColor.FromArgb(0xF0, 0x88, 0x3E);
                else scoreCell.Style.Font.FontColor = XLColor.FromArgb(0xDA, 0x36, 0x33);
            }

            // --- Column widths ---
            ws.Column(1).Width = 5;    // #
            ws.Column(2).Width = 35;   // Email
            ws.Column(3).Width = 12;   // Estado
            ws.Column(4).Width = 8;    // Score
            ws.Column(5).Width = 10;   // Formato
            ws.Column(6).Width = 25;   // Desechable
            ws.Column(7).Width = 10;   // MX
            ws.Column(8).Width = 10;   // SMTP
            ws.Column(9).Width = 30;   // SMTP Error
            ws.Column(10).Width = 10;  // Catch-all
            ws.Column(11).Width = 18;  // Role
            ws.Column(12).Width = 20;  // Sospechoso
            ws.Column(13).Width = 14;  // Duracion
            ws.Column(14).Width = 18;  // Fecha

            // --- Auto filter ---
            ws.RangeUsed().SetAutoFilter();

            // --- Freeze first row ---
            ws.SheetView.FreezeRows(1);

            // --- Summary sheet ---
            if (data.Count > 0)
            {
                var summary = workbook.Worksheets.Add("Resumen");

                summary.Cell("A1").Value = "Email Validator PRO - Resumen";
                summary.Cell("A1").Style.Font.Bold = true;
                summary.Cell("A1").Style.Font.FontSize = 16;

                summary.Cell("A3").Value = "Metrica";
                summary.Cell("B3").Value = "Valor";
                summary.Range("A3:B3").Style.Font.Bold = true;
                summary.Range("A3:B3").Style.Fill.BackgroundColor = XLColor.FromArgb(0x23, 0x86, 0x36);
                summary.Range("A3:B3").Style.Font.FontColor = XLColor.White;

                var total = data.Count;
                var validos = data.Count(r => r.Status == EmailStatus.Valid);
                var invalidos = data.Count(r => r.Status == EmailStatus.Invalid);
                var risky = data.Count(r => r.Status == EmailStatus.Risky);
                var desechables = data.Count(r => r.Status == EmailStatus.Disposable);
                var catchAll = data.Count(r => r.IsCatchAll);
                var roleBased = data.Count(r => r.IsRoleBased);
                var avgScore = data.Average(r => r.Score);

                var stats = new (string, string)[]
                {
                    ("Total emails", total.ToString()),
                    ("Validos", $"{validos} ({validos * 100.0 / total:F1}%)"),
                    ("Invalidos", $"{invalidos} ({invalidos * 100.0 / total:F1}%)"),
                    ("Risky", $"{risky} ({risky * 100.0 / total:F1}%)"),
                    ("Desechables", $"{desechables} ({desechables * 100.0 / total:F1}%)"),
                    ("Catch-all", $"{catchAll} ({catchAll * 100.0 / total:F1}%)"),
                    ("Role-based", $"{roleBased} ({roleBased * 100.0 / total:F1}%)"),
                    ("Score promedio", $"{avgScore:F1}/100"),
                    ("Fecha exportacion", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))
                };

                for (int i = 0; i < stats.Length; i++)
                {
                    summary.Cell(i + 4, 1).Value = stats[i].Item1;
                    summary.Cell(i + 4, 2).Value = stats[i].Item2;
                }

                summary.Column(1).Width = 20;
                summary.Column(2).Width = 30;
            }

            workbook.SaveAs(filePath);
        }
    }
}

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace EmailValidatorPRO.Services
{
    public class LicenseService
    {
        private const string Folder = "EmailValidatorPRO";
        private const string FileName = "license.json";
        private const int TrialLimit = 50;
        private const string ChecksumKey = "EVP2024!SecureKey";

        public bool IsActivated { get; private set; }
        public bool IsTrial { get; private set; } = true;
        public bool IsExpired { get; private set; }
        public int TotalValidated { get; private set; }
        public string? LicenseKey { get; private set; }
        public DateTime? ActivatedDate { get; private set; }
        public DateTime? ExpirationDate { get; private set; }
        public string? MachineId { get; private set; }
        public string LicenseType { get; private set; } = "Trial";

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Folder, FileName);

        public LicenseService() { Load(); }

        private void Load()
        {
            try
            {
                var path = FilePath;
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var data = JsonSerializer.Deserialize<LicenseData>(json);
                if (data == null) return;

                TotalValidated = data.TotalValidated;
                LicenseKey = data.LicenseKey;
                ActivatedDate = data.ActivatedDate;
                ExpirationDate = data.ExpirationDate;
                MachineId = data.MachineId;

                if (string.IsNullOrEmpty(data.LicenseKey)) return;

                if (!ValidateKey(data.LicenseKey))
                {
                    LicenseKey = null;
                    IsActivated = false;
                    IsTrial = true;
                    Save();
                    return;
                }

                if (ExpirationDate.HasValue && DateTime.Now > ExpirationDate.Value)
                {
                    IsActivated = false;
                    IsTrial = true;
                    IsExpired = true;
                    LicenseType = "Expirada";
                    return;
                }

                IsActivated = true;
                IsTrial = false;
                IsExpired = false;
                LicenseType = ExpirationDate.HasValue ? "Mensual" : "Definitiva";
            }
            catch { }
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath)!;
                Directory.CreateDirectory(dir);

                var data = new LicenseData
                {
                    TotalValidated = TotalValidated,
                    LicenseKey = LicenseKey,
                    ActivatedDate = ActivatedDate,
                    ExpirationDate = ExpirationDate,
                    MachineId = MachineId
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public (bool CanProceed, string? Message) CanValidate(int count)
        {
            if (IsActivated && !IsExpired) return (true, null);

            if (IsExpired)
                return (false, $"Licencia mensual expirada el {ExpirationDate:dd/MM/yyyy}. Contactá al vendedor para renovar.");

            if (TotalValidated + count <= TrialLimit) return (true, null);

            var remaining = TrialLimit - TotalValidated;
            return (false, $"Límite de trial alcanzado ({TrialLimit} emails). Quedaban {remaining} disponibles. Activá tu licencia.");
        }

        public void RecordUsage(int count)
        {
            TotalValidated += count;
            Save();
        }

        public (bool Success, string Message) Activate(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return (false, "Ingresá una clave de licencia.");

            key = key.Trim().ToUpper();

            if (!IsValidFormat(key))
                return (false, "Formato inválido. Usá:\nEVP-XXXX-XXXX-XXXX-XXXX (Definitiva)\nEVM-XXXX-XXXX-XXXX-XXXX (Mensual 30 días)");

            if (!ValidateKey(key))
                return (false, "Clave inválida. Contactá al vendedor.");

            LicenseKey = key;
            MachineId = GetMachineId();
            ActivatedDate = DateTime.Now;

            if (key.StartsWith("EVM-"))
            {
                ExpirationDate = DateTime.Now.AddDays(30);
                LicenseType = "Mensual";
            }
            else
            {
                ExpirationDate = null;
                LicenseType = "Definitiva";
            }

            IsActivated = true;
            IsExpired = false;
            IsTrial = false;
            Save();

            var detalle = ExpirationDate.HasValue
                ? $"Tipo: Mensual (30 días)\nExpira: {ExpirationDate:dd/MM/yyyy}"
                : "Tipo: Definitiva (sin vencimiento)";

            return (true, $"¡Licencia activada correctamente!\n{detalle}");
        }

        // ─── Validación ────────────────────────────────────────

        private bool ValidateKey(string key)
        {
            var parts = key.Split('-');
            if (parts.Length != 5) return false;

            var basePart = string.Join('-', parts[0], parts[1], parts[2], parts[3]);
            var checksum = parts[4];
            var expected = ComputeChecksum(basePart);

            return checksum.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsValidFormat(string key)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                key, @"^EV[PM]-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}-[A-Z0-9]{4}$");
        }

        private static string GetMachineId()
        {
            try
            {
                var input = $"{Environment.MachineName}|{Environment.UserName}|{ChecksumKey}";
                using var sha = SHA256.Create();
                var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return Convert.ToHexString(hash).Substring(0, 8).ToLower();
            }
            catch { return "unknown"; }
        }

        // ─── Generación de claves (solo vendedor) ──────────────

        public static string GenerateKey(bool definitive = true)
        {
            var prefix = definitive ? "EVP" : "EVM";
            var rnd = RandomNumberGenerator.GetBytes(12);
            var hex = Convert.ToHexString(rnd).ToUpper();

            var basePart = $"{prefix}-{hex.Substring(0, 4)}-{hex.Substring(4, 4)}-{hex.Substring(8, 4)}";
            var checksum = ComputeChecksum(basePart);

            return $"{basePart}-{checksum}";
        }

        public static string[] GenerateKeys(int count, bool definitive = true)
        {
            var keys = new string[count];
            for (int i = 0; i < count; i++)
                keys[i] = GenerateKey(definitive);
            return keys;
        }

        private static string ComputeChecksum(string basePart)
        {
            var input = $"{basePart}:{ChecksumKey}";
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash).Substring(0, 4).ToUpper();
        }

        // ─── Data ──────────────────────────────────────────────

        private class LicenseData
        {
            public int TotalValidated { get; set; }
            public string? LicenseKey { get; set; }
            public DateTime? ActivatedDate { get; set; }
            public DateTime? ExpirationDate { get; set; }
            public string? MachineId { get; set; }
        }
    }
}
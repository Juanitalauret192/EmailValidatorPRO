namespace EmailValidatorPRO.Models
{
    public class ValidationConfig
    {
        public int ThreadCount { get; set; } = 10;
        public int SmtpTimeoutMs { get; set; } = 10000;
        public int SmtpPort { get; set; } = 25;
        public int DnsTimeoutMs { get; set; } = 5000;
        public int MaxRetries { get; set; } = 2;
        public int RetryDelayMs { get; set; } = 1000;
        public bool VerifySmtp { get; set; } = true;
        public bool CheckDisposable { get; set; } = true;
        public bool CheckDnsMx { get; set; } = true;
        public bool CheckSuspicious { get; set; } = true;
        public string SmtpHeloDomain { get; set; } = "validator.local";
        public string SmtpMailFrom { get; set; } = "validator@example.com";
    }
}

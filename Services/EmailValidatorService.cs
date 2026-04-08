using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EmailValidatorPRO.Models;
using Serilog;

namespace EmailValidatorPRO.Services
{
    public class EmailValidatorService
    {
        private readonly DisposableDomainDetector _disposableDetector;
        private readonly RoleBasedDetector _roleBasedDetector;
        private readonly DnsMxChecker _dnsMxChecker;
        private readonly SmtpVerifier _smtpVerifier;
        private readonly CatchAllDetector _catchAllDetector;
        private readonly EmailScoringService _scoringService;
        private readonly ValidationConfig _config;
        private readonly ConcurrentDictionary<string, bool> _catchAllCache = new();

        private static readonly Regex EmailRegex = new(
            @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?" +
            @"(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly string[] SuspiciousTlds =
        {
            ".tk", ".ml", ".ga", ".cf", ".gq", ".xyz", ".top", ".work", ".click",
            ".link", ".info", ".online", ".site", ".club", ".icu", ".buzz", ".monster"
        };

        public EmailValidatorService(ValidationConfig? config = null)
        {
            _config = config ?? new ValidationConfig();
            _disposableDetector = new DisposableDomainDetector();
            _roleBasedDetector = new RoleBasedDetector();
            _dnsMxChecker = new DnsMxChecker(_config.DnsTimeoutMs);
            _smtpVerifier = new SmtpVerifier(
                _config.SmtpTimeoutMs, _config.SmtpPort,
                _config.SmtpHeloDomain, _config.SmtpMailFrom);
            _catchAllDetector = new CatchAllDetector(
                _config.SmtpTimeoutMs, _config.SmtpPort,
                _config.SmtpHeloDomain, _config.SmtpMailFrom);
            _scoringService = new EmailScoringService();
        }

        public async Task<EmailResult> ValidateSingleAsync(string email,
            CancellationToken cancellationToken = default)
        {
            var result = new EmailResult { Email = email.Trim() };
            var sw = Stopwatch.StartNew();

            try
            {
                if (!ValidateFormat(email, result))
                {
                    result.Status = EmailStatus.Invalid;
                    result.Score = _scoringService.CalculateScore(result);
                    return result;
                }

                var domain = email.Split('@')[1];

                var (isRole, roleType) = _roleBasedDetector.Detect(email);
                result.IsRoleBased = isRole;
                result.RoleType = roleType;

                if (_config.CheckDisposable && _disposableDetector.IsDisposableDomain(domain))
                {
                    result.IsDisposable = true;
                    result.DisposableReason = "Dominio desechable/temporal detectado";
                    result.Status = EmailStatus.Disposable;
                    result.Score = _scoringService.CalculateScore(result);
                    return result;
                }

                if (_config.CheckDnsMx)
                {
                    var (hasMx, records, mxError) = await _dnsMxChecker.CheckMxRecordsAsync(domain, cancellationToken);
                    result.HasMxRecords = hasMx;
                    result.MxRecords = records;
                    result.MxError = mxError;
                    if (!hasMx)
                    {
                        result.Status = EmailStatus.Invalid;
                        result.Score = _scoringService.CalculateScore(result);
                        return result;
                    }
                }

                if (_config.VerifySmtp && result.MxRecords != null)
                {
                    var (smtpAccepted, smtpIsBlocked, smtpResponse, smtpError) =
                        await _smtpVerifier.VerifyAsync(email, result.MxRecords, cancellationToken);

                    result.SmtpChecked = true;
                    result.SmtpAccepted = smtpAccepted;
                    result.SmtpResponse = smtpResponse;
                    result.SmtpError = smtpError;

                    if (smtpAccepted)
                    {
                        result.Status = EmailStatus.Valid;
                    }
                    else if (smtpIsBlocked)
                    {
                        result.Status = EmailStatus.Risky;
                        Log.Warning("SMTP bloqueado para {Email}: {Error}", email, smtpError);
                    }
                    else if (smtpError?.Contains("Greylisting") == true ||
                             smtpError?.Contains("450") == true ||
                             smtpError?.Contains("451") == true)
                    {
                        result.Status = EmailStatus.Risky;
                        Log.Warning("Greylisting: {Email}", email);
                    }
                    else if (IsSmtpRejection(smtpError, smtpResponse))
                    {
                        result.Status = EmailStatus.Invalid;
                    }
                    else
                    {
                        result.Status = EmailStatus.Risky;
                    }
                }
                else
                {
                    result.Status = result.HasMxRecords ? EmailStatus.Valid : EmailStatus.Risky;
                }

                if (result.SmtpAccepted && result.MxRecords != null)
                {
                    var cached = _catchAllCache.TryGetValue(domain, out bool isCatchAll);
                    if (cached)
                    {
                        result.CatchAllChecked = true;
                        result.IsCatchAll = isCatchAll;
                        result.CatchAllReason = isCatchAll ? "Dominio catch-all (cacheado)" : "No catch-all (cacheado)";
                        if (isCatchAll && result.Status == EmailStatus.Valid)
                        {
                            result.Status = EmailStatus.Risky;
                            result.SmtpError = "Dominio catch-all: no se puede confirmar si el email existe";
                        }
                    }
                    else
                    {
                        var (isCatch, catchResponse, catchError) =
                            await _catchAllDetector.DetectAsync(domain, result.MxRecords, cancellationToken);
                        result.CatchAllChecked = true;
                        result.IsCatchAll = isCatch;
                        result.CatchAllReason = isCatch ? catchResponse : null;
                        _catchAllCache.TryAdd(domain, isCatch);
                        if (isCatch && result.Status == EmailStatus.Valid)
                        {
                            result.Status = EmailStatus.Risky;
                            result.SmtpError = "Dominio catch-all: no se puede confirmar si el email existe";
                        }
                    }
                }

                if (_config.CheckSuspicious && result.Status == EmailStatus.Valid)
                    CheckSuspiciousPatterns(email, result);

                result.Score = _scoringService.CalculateScore(result);
            }
            catch (OperationCanceledException)
            {
                result.SmtpError = "Cancelado";
                result.Status = EmailStatus.Risky;
                result.Score = _scoringService.CalculateScore(result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error validando {Email}", email);
                result.SmtpError = $"Error: {ex.Message}";
                result.Status = EmailStatus.Risky;
                result.Score = _scoringService.CalculateScore(result);
            }
            finally
            {
                sw.Stop();
                result.ValidationDuration = sw.Elapsed;
                result.ValidatedAt = DateTime.UtcNow;
            }

            return result;
        }

        private bool IsSmtpRejection(string? error, string? response)
        {
            var text = $"{error} {response}";
            return text.Contains("550") ||
                   text.Contains("553") ||
                   text.Contains("Email rechazado", StringComparison.OrdinalIgnoreCase);
        }

        public async Task ValidateBatchAsync(
            ConcurrentBag<EmailResult> results, string[] emails,
            IProgress<(int Processed, int Total, EmailResult Result)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var total = emails.Length;
            var processed = 0;
            var semaphore = new SemaphoreSlim(_config.ThreadCount, _config.ThreadCount);

            var tasks = emails.Select(async email =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await ValidateSingleWithRetryAsync(email, cancellationToken);
                    results.Add(result);
                    Interlocked.Increment(ref processed);
                    progress?.Report((processed, total, result));
                }
                finally { semaphore.Release(); }
            });

            await Task.WhenAll(tasks);
        }

        private async Task<EmailResult> ValidateSingleWithRetryAsync(string email, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= _config.MaxRetries; attempt++)
            {
                var result = await ValidateSingleAsync(email, ct);
                if (result.Status != EmailStatus.Risky || attempt >= _config.MaxRetries) return result;
                if (result.SmtpError?.Contains("Greylisting") == true ||
                    result.SmtpError?.Contains("450") == true)
                {
                    await Task.Delay(_config.RetryDelayMs * (attempt + 1), ct);
                    continue;
                }
                return result;
            }
            return await ValidateSingleAsync(email, ct);
        }

        private bool ValidateFormat(string email, EmailResult result)
        {
            if (string.IsNullOrWhiteSpace(email)) { result.FormatError = "Email vacio"; return false; }
            email = email.Trim();
            if (!email.Contains('@')) { result.FormatError = "Falta @" ; return false; }
            var parts = email.Split('@');
            if (parts.Length != 2) { result.FormatError = "Multiples @" ; return false; }
            var local = parts[0]; var domain = parts[1];
            if (string.IsNullOrWhiteSpace(local) || local.Length > 64) { result.FormatError = "Local invalida"; return false; }
            if (string.IsNullOrWhiteSpace(domain) || domain.Length > 255) { result.FormatError = "Dominio invalido"; return false; }
            if (!domain.Contains('.')) { result.FormatError = "Sin TLD"; return false; }
            if (!EmailRegex.IsMatch(email)) { result.FormatError = "Formato invalido"; return false; }
            result.IsFormatValid = true;
            return true;
        }

        private void CheckSuspiciousPatterns(string email, EmailResult result)
        {
            var domain = email.Split('@')[1];
            var local = email.Split('@')[0];
            foreach (var tld in SuspiciousTlds)
            {
                if (domain.EndsWith(tld, StringComparison.OrdinalIgnoreCase))
                { result.IsSuspicious = true; result.SuspiciousReason = $"TLD sospechoso: {tld}"; result.Status = EmailStatus.Risky; return; }
            }
            var digitCount = local.Count(char.IsDigit);
            if (local.Count(char.IsLetter) > 0 && digitCount / (double)local.Length > 0.7)
            { result.IsSuspicious = true; result.SuspiciousReason = "Exceso de numeros"; result.Status = EmailStatus.Risky; }
            if (domain.Count(c => c == '-') > 3)
            { result.IsSuspicious = true; result.SuspiciousReason = "Dominio inusual"; result.Status = EmailStatus.Risky; }
        }
    }
}
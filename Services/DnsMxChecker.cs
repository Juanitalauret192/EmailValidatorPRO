using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace EmailValidatorPRO.Services
{
    public class DnsMxChecker
    {
        private readonly LookupClient _dnsClient;
        private readonly int _timeoutMs;

        public DnsMxChecker(int timeoutMs = 5000)
        {
            _timeoutMs = timeoutMs;
            _dnsClient = new LookupClient(new LookupClientOptions
            {
                Timeout = TimeSpan.FromMilliseconds(timeoutMs),
                Retries = 2,
                UseCache = true,
                EnableAuditTrail = false
            });
        }

        public async Task<(bool HasMx, string[]? Records, string? Error)> CheckMxRecordsAsync(
            string domain, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _dnsClient.QueryAsync(domain, QueryType.MX, cancellationToken: cancellationToken);

                if (result.HasError)
                {
                    // Si no hay MX, intentar con registro A como fallback
                    return await CheckARecordFallbackAsync(domain, cancellationToken);
                }

                var mxRecords = result.Answers
                    .OfType<MxRecord>()
                    .Select(mx => mx.Exchange.Value)
                    .ToArray();

                if (mxRecords.Length > 0)
                {
                    return (true, mxRecords, null);
                }

                // Sin registros MX, intentar fallback A
                return await CheckARecordFallbackAsync(domain, cancellationToken);
            }
            catch (DnsResponseException ex)
            {
                return (false, null, $"DNS Error: {ex.Code} - {ex.Message}");
            }
            catch (OperationCanceledException)
            {
                return (false, null, "Cancelado por timeout");
            }
            catch (Exception ex)
            {
                // Fallback a verificacion A record
                return await CheckARecordFallbackAsync(domain, cancellationToken);
            }
        }

        private async Task<(bool HasMx, string[]? Records, string? Error)> CheckARecordFallbackAsync(
            string domain, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _dnsClient.QueryAsync(domain, QueryType.A, cancellationToken: cancellationToken);

                var aRecords = result.Answers
                    .OfType<ARecord>()
                    .Select(a => a.Address.ToString())
                    .ToArray();

                if (aRecords.Length > 0)
                {
                    return (true, new[] { $"A:{domain}" }, "Solo registro A (sin MX)");
                }

                return (false, null, "No se encontraron registros MX ni A");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error DNS: {ex.Message}");
            }
        }
    }
}

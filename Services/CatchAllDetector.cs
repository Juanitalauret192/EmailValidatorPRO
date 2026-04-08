using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EmailValidatorPRO.Services
{
    /// <summary>
    /// Detecta si un dominio tiene configurado catch-all.
    /// Un dominio catch-all acepta CUALQUIER email, incluso los que no existen.
    /// Se verifica enviando RCPT TO a un email inexistente aleatorio.
    /// Si el servidor lo acepta, el dominio es catch-all y no podemos
    /// confiar en la verificacion SMTP individual.
    /// </summary>
    public class CatchAllDetector
    {
        private readonly int _timeoutMs;
        private readonly int _port;
        private readonly string _heloDomain;
        private readonly string _mailFrom;

        public CatchAllDetector(int timeoutMs = 10000, int port = 25,
            string heloDomain = "validator.local", string mailFrom = "validator@example.com")
        {
            _timeoutMs = timeoutMs;
            _port = port;
            _heloDomain = heloDomain;
            _mailFrom = mailFrom;
        }

        /// <summary>
        /// Verifica si el dominio es catch-all.
        /// Envia RCPT TO a un email aleatorio que seguramente no existe.
        /// Si el servidor acepta, el dominio es catch-all.
        /// </summary>
        public async Task<(bool IsCatchAll, string? Response, string? Error)> DetectAsync(
            string domain, string[] mxRecords, CancellationToken cancellationToken = default)
        {
            if (mxRecords == null || mxRecords.Length == 0)
                return (false, null, "Sin registros MX");

            // Generar un email que NO puede existir en el dominio
            var randomPart = GenerateRandomNonexistentUser();
            var fakeEmail = $"{randomPart}@{domain}";

            Log.Debug("Verificando catch-all para {Domain} con email {Fake}", domain, fakeEmail);

            foreach (var mx in mxRecords)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var mxHost = mx.StartsWith("A:") ? domain : mx;
                    var result = await PerformCatchAllCheckAsync(fakeEmail, mxHost, cancellationToken);

                    if (result.IsCatchAll || result.Response?.StartsWith("250") == true)
                    {
                        Log.Information("Dominio catch-all detectado: {Domain}", domain);
                        return (true, result.Response, null);
                    }

                    // Si el servidor rechaza el email falso, NO es catch-all
                    if (result.Response?.StartsWith("550") == true ||
                        result.Response?.StartsWith("551") == true ||
                        result.Response?.StartsWith("553") == true)
                    {
                        return (false, result.Response, null);
                    }
                }
                catch (OperationCanceledException)
                {
                    return (false, null, "Cancelado");
                }
                catch (Exception ex)
                {
                    Log.Debug("Catch-all check fallo para {MxHost}: {Error}", mx, ex.Message);
                    continue;
                }
            }

            return (false, null, "No se pudo determinar (todos los MX fallaron)");
        }

        private async Task<(bool IsCatchAll, string? Response, string? Error)> PerformCatchAllCheckAsync(
            string fakeEmail, string mxHost, CancellationToken cancellationToken)
        {
            TcpClient? tcpClient = null;
            NetworkStream? stream = null;
            StreamReader? reader = null;
            StreamWriter? writer = null;

            try
            {
                tcpClient = new TcpClient();
                tcpClient.NoDelay = true;

                var connectTask = tcpClient.ConnectAsync(mxHost, _port);
                var timeoutTask = Task.Delay(_timeoutMs, cancellationToken);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask == timeoutTask)
                    return (false, null, $"Timeout conectando a {mxHost}:{_port}");

                await connectTask;

                stream = tcpClient.GetStream();
                reader = new StreamReader(stream, Encoding.ASCII);
                writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                // Leer banner
                var banner = await ReadResponseAsync(reader, cancellationToken);
                if (!banner.StartsWith("220"))
                    return (false, banner, $"Banner invalido: {banner}");

                // HELO
                await writer.WriteLineAsync($"HELO {_heloDomain}");
                var heloResponse = await ReadResponseAsync(reader, cancellationToken);
                if (!heloResponse.StartsWith("250"))
                    return (false, heloResponse, $"HELO rechazado: {heloResponse}");

                // MAIL FROM
                await writer.WriteLineAsync($"MAIL FROM:<{_mailFrom}>");
                var mailFromResponse = await ReadResponseAsync(reader, cancellationToken);
                if (!mailFromResponse.StartsWith("250"))
                    return (false, mailFromResponse, $"MAIL FROM rechazado: {mailFromResponse}");

                // RCPT TO - Email inexistente
                await writer.WriteLineAsync($"RCPT TO:<{fakeEmail}>");
                var rcptResponse = await ReadResponseAsync(reader, cancellationToken);

                // QUIT
                await writer.WriteLineAsync("QUIT");
                try { await ReadResponseAsync(reader, cancellationToken); } catch { }

                // Si el servidor acepta un email que no existe = CATCH-ALL
                if (rcptResponse.StartsWith("250") || rcptResponse.StartsWith("251"))
                {
                    return (true, rcptResponse, $"Catch-all: el dominio acepta emails inexistentes ({fakeEmail})");
                }

                // Si lo rechaza = NO es catch-all
                if (rcptResponse.StartsWith("550") || rcptResponse.StartsWith("553"))
                {
                    return (false, rcptResponse, null);
                }

                // Greylisting u otra respuesta
                return (false, rcptResponse, $"Respuesta inesperada: {rcptResponse}");
            }
            catch (SocketException ex)
            {
                return (false, null, $"Error de conexion: {ex.Message}");
            }
            catch (IOException ex)
            {
                return (false, null, $"Error I/O: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, null, $"Error: {ex.Message}");
            }
            finally
            {
                writer?.Dispose();
                reader?.Dispose();
                stream?.Dispose();
                tcpClient?.Close();
            }
        }

        /// <summary>
        /// Genera un usuario aleatorio que no puede existir.
        /// Usa prefijo + timestamp + random hex para garantizar unicidad.
        /// </summary>
        private string GenerateRandomNonexistentUser()
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var random = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"xtest_{timestamp}_{random}";
        }

        private async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            var sb = new StringBuilder();
            var line = await reader.ReadLineAsync();

            if (line == null) return string.Empty;

            sb.AppendLine(line);

            while (line != null && line.Length >= 4 && line[3] == '-')
            {
                cancellationToken.ThrowIfCancellationRequested();
                line = await reader.ReadLineAsync();
                if (line != null)
                    sb.AppendLine(line);
            }

            return sb.ToString().TrimEnd();
        }
    }
}

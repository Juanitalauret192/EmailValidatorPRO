using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace EmailValidatorPRO.Services
{
    public class SmtpVerifier
    {
        private readonly int _timeoutMs;
        private readonly int _port;
        private readonly string _heloDomain;
        private readonly string _mailFrom;

        public SmtpVerifier(int timeoutMs = 10000, int port = 25,
            string heloDomain = "validator.local", string mailFrom = "validator@example.com")
        {
            _timeoutMs = timeoutMs;
            _port = port;
            _heloDomain = heloDomain;
            _mailFrom = mailFrom;
        }

        public async Task<(bool Accepted, bool IsBlocked, string? Response, string? Error)> VerifyAsync(
            string email, string[]? mxRecords = null, CancellationToken cancellationToken = default)
        {
            if (mxRecords == null || mxRecords.Length == 0)
                return (false, false, null, "No hay registros MX para verificar SMTP");

            var domain = email.Split('@')[1];
            int connectionErrors = 0;
            string? lastError = null;

            foreach (var mx in mxRecords)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var mxHost = mx.StartsWith("A:") ? domain : mx;
                    var result = await PerformSmtpHandshakeAsync(email, mxHost, cancellationToken);

                    if (result.Accepted)
                        return result;

                    if (result.Response?.Contains("550") == true ||
                        result.Response?.Contains("551") == true ||
                        result.Response?.Contains("552") == true ||
                        result.Response?.Contains("553") == true)
                    {
                        return result;
                    }

                    if (result.Response?.Contains("450") == true ||
                        result.Response?.Contains("451") == true ||
                        result.Response?.Contains("452") == true)
                    {
                        lastError = result.Error;
                        continue;
                    }

                    lastError = result.Error;
                }
                catch (OperationCanceledException)
                {
                    return (false, false, null, "Cancelado");
                }
                catch (Exception ex)
                {
                    connectionErrors++;
                    lastError = ex.Message;
                    Log.Debug("SMTP verification fallida para {MxHost}: {Error}", mx, ex.Message);
                    continue;
                }
            }

            if (connectionErrors == mxRecords.Length)
            {
                return (false, true, null, $"SMTP bloqueado: tu ISP o firewall impide conexion al puerto 25. No se pudo verificar {domain}");
            }

            return (false, false, null, lastError ?? "Todos los servidores MX fallaron o agotaron timeout");
        }

        private async Task<(bool Accepted, bool IsBlocked, string? Response, string? Error)> PerformSmtpHandshakeAsync(
            string email, string mxHost, CancellationToken cancellationToken)
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
                {
                    return (false, true, null, $"Timeout: {mxHost}:{_port} no responde (posible firewall o bloqueo)");
                }

                try
                {
                    await connectTask;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
                {
                    return (false, true, null, $"Puerto 25 bloqueado: {mxHost} rechaza conexiones SMTP directas");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                    return (false, true, null, $"Timeout: {mxHost} no responde (posible firewall)");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostUnreachable)
                {
                    return (false, true, null, $"Host inalcanzable: {mxHost} no es accesible");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.NetworkUnreachable)
                {
                    return (false, true, null, $"Red inalcanzable: posible problema de DNS o firewall");
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AccessDenied)
                {
                    return (false, true, null, $"Acceso denegado: tu ISP o firewall bloquea el puerto 25 (muy comun en conexiones residenciales)");
                }

                stream = tcpClient.GetStream();
                reader = new StreamReader(stream, Encoding.ASCII);
                writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

                var banner = await ReadResponseAsync(reader, cancellationToken);
                if (!banner.StartsWith("220"))
                {
                    if (banner.Contains("421"))
                    {
                        return (false, true, null, $"Servidor rechaza conexiones: {mxHost} no acepta SMTP desde tu IP");
                    }
                    return (false, false, banner, $"Banner invalido: {banner}");
                }

                await writer.WriteLineAsync($"HELO {_heloDomain}");
                var heloResponse = await ReadResponseAsync(reader, cancellationToken);
                if (!heloResponse.StartsWith("250"))
                {
                    if (heloResponse.Contains("550") || heloResponse.Contains("554"))
                    {
                        return (false, true, null, $"HELO rechazado: {mxHost} no confia en tu IP o dominio");
                    }
                    return (false, false, heloResponse, $"HELO rechazado: {heloResponse}");
                }

                await writer.WriteLineAsync($"MAIL FROM:<{_mailFrom}>");
                var mailFromResponse = await ReadResponseAsync(reader, cancellationToken);
                if (!mailFromResponse.StartsWith("250"))
                {
                    return (false, false, mailFromResponse, $"MAIL FROM rechazado: {mailFromResponse}");
                }

                await writer.WriteLineAsync($"RCPT TO:<{email}>");
                var rcptResponse = await ReadResponseAsync(reader, cancellationToken);

                await writer.WriteLineAsync("QUIT");
                try { await ReadResponseAsync(reader, cancellationToken); } catch { }

                if (rcptResponse.StartsWith("250") || rcptResponse.StartsWith("251"))
                {
                    return (true, false, rcptResponse, null);
                }

                if (rcptResponse.StartsWith("450") || rcptResponse.StartsWith("451") || rcptResponse.StartsWith("452"))
                {
                    return (false, false, rcptResponse, $"Greylisting: {rcptResponse}");
                }

                if (rcptResponse.StartsWith("55"))
                {
                    return (false, false, rcptResponse, $"Email rechazado: {rcptResponse}");
                }

                return (false, false, rcptResponse, $"Respuesta inesperada: {rcptResponse}");
            }
            catch (SocketException ex)
            {
                return (false, true, null, $"Error de conexion con {mxHost}: {ex.Message} (Codigo: {ex.SocketErrorCode})");
            }
            catch (IOException ex)
            {
                return (false, true, null, $"Error I/O con {mxHost}: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (false, false, null, $"Error SMTP: {ex.Message}");
            }
            finally
            {
                writer?.Dispose();
                reader?.Dispose();
                stream?.Dispose();
                tcpClient?.Close();
            }
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
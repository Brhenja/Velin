using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public class ServerStatus
    {
        public bool Online { get; set; }
        public string Motd { get; set; } = "";
        public string Version { get; set; } = "";
        public int Players { get; set; }
        public int MaxPlayers { get; set; }
        public long LatencyMs { get; set; }
        public string Error { get; set; } = "";
    }

    // "Server List Ping" de Minecraft: pregunta por el estado de un servidor sin entrar al juego.
    public static class ServerPinger
    {
        public static (string host, int port) ParseAddress(string address)
        {
            address = address.Trim();
            int cut = address.LastIndexOf(':');
            if (cut > 0 && cut < address.Length - 1 && int.TryParse(address.Substring(cut + 1), out var port) && port is > 0 and < 65536 && !address.StartsWith("["))
                return (address.Substring(0, cut), port);
            return (address, 25565);
        }

        public static async Task<ServerStatus> PingAsync(string address, int timeoutMs = 4000, CancellationToken ct = default)
        {
            var status = new ServerStatus();
            try
            {
                var (host, port) = ParseAddress(address);
                if (host.Length == 0) { status.Error = "Dirección vacía."; return status; }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeoutMs);
                using var client = new TcpClient();
                await client.ConnectAsync(host, port, cts.Token);
                var stream = client.GetStream();

                // Handshake (estado = 1) y pedido de estado.
                var hs = new MemoryStream();
                WriteVarInt(hs, 0x00);
                WriteVarInt(hs, 767);
                var hostBytes = Encoding.UTF8.GetBytes(host);
                WriteVarInt(hs, hostBytes.Length); hs.Write(hostBytes);
                hs.WriteByte((byte)(port >> 8)); hs.WriteByte((byte)(port & 0xFF));
                WriteVarInt(hs, 1);
                await SendPacketAsync(stream, hs.ToArray(), cts.Token);
                await SendPacketAsync(stream, new byte[] { 0x00 }, cts.Token);

                // Respuesta: largo, id (0), texto JSON.
                int length = await ReadVarIntAsync(stream, cts.Token);
                int id = await ReadVarIntAsync(stream, cts.Token);
                if (id != 0x00 || length <= 0 || length > 1_000_000) { status.Error = "Respuesta inválida."; return status; }
                int jsonLen = await ReadVarIntAsync(stream, cts.Token);
                var json = new byte[jsonLen];
                await ReadExactAsync(stream, json, cts.Token);
                Parse(Encoding.UTF8.GetString(json), status);

                // Ping para medir la latencia.
                var sw = Stopwatch.StartNew();
                var ping = new MemoryStream();
                WriteVarInt(ping, 0x01);
                ping.Write(BitConverter.GetBytes(DateTime.UtcNow.Ticks));
                await SendPacketAsync(stream, ping.ToArray(), cts.Token);
                int pongLen = await ReadVarIntAsync(stream, cts.Token);
                var pong = new byte[Math.Clamp(pongLen, 0, 64)];
                await ReadExactAsync(stream, pong, cts.Token);
                status.LatencyMs = sw.ElapsedMilliseconds;

                status.Online = true;
            }
            catch (OperationCanceledException) { status.Error = ct.IsCancellationRequested ? "Cancelado." : "Sin respuesta (tiempo agotado)."; }
            catch (Exception ex) { status.Error = ex.Message; }
            return status;
        }

        private static void Parse(string json, ServerStatus status)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("version", out var v) && v.TryGetProperty("name", out var vn)) status.Version = vn.GetString() ?? "";
            if (root.TryGetProperty("players", out var p))
            {
                if (p.TryGetProperty("online", out var on) && on.ValueKind == JsonValueKind.Number) status.Players = on.GetInt32();
                if (p.TryGetProperty("max", out var mx) && mx.ValueKind == JsonValueKind.Number) status.MaxPlayers = mx.GetInt32();
            }
            if (root.TryGetProperty("description", out var d))
                status.Motd = Regex.Replace(FlattenText(d), "§.", "").Replace("\n", " ").Trim();
        }

        // La descripción puede ser un texto, o un objeto con "text" y "extra" (partes con color).
        private static string FlattenText(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.String: return e.GetString() ?? "";
                case JsonValueKind.Object:
                {
                    var sb = new StringBuilder();
                    if (e.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String) sb.Append(t.GetString());
                    if (e.TryGetProperty("extra", out var ex) && ex.ValueKind == JsonValueKind.Array)
                        foreach (var part in ex.EnumerateArray()) sb.Append(FlattenText(part));
                    return sb.ToString();
                }
                case JsonValueKind.Array:
                {
                    var sb = new StringBuilder();
                    foreach (var part in e.EnumerateArray()) sb.Append(FlattenText(part));
                    return sb.ToString();
                }
                default: return "";
            }
        }

        private static void WriteVarInt(Stream s, int value)
        {
            uint v = (uint)value;
            while (true)
            {
                if ((v & ~0x7Fu) == 0) { s.WriteByte((byte)v); return; }
                s.WriteByte((byte)((v & 0x7F) | 0x80));
                v >>= 7;
            }
        }

        private static async Task SendPacketAsync(NetworkStream stream, byte[] payload, CancellationToken ct)
        {
            var framed = new MemoryStream();
            WriteVarInt(framed, payload.Length);
            framed.Write(payload);
            await stream.WriteAsync(framed.ToArray(), ct);
            await stream.FlushAsync(ct);
        }

        private static async Task<int> ReadVarIntAsync(NetworkStream stream, CancellationToken ct)
        {
            int result = 0, shift = 0;
            var one = new byte[1];
            while (true)
            {
                if (await stream.ReadAsync(one.AsMemory(0, 1), ct) == 0) throw new EndOfStreamException("El servidor cerró la conexión.");
                result |= (one[0] & 0x7F) << shift;
                if ((one[0] & 0x80) == 0) return result;
                shift += 7;
                if (shift > 35) throw new InvalidDataException("VarInt demasiado largo.");
            }
        }

        private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer.AsMemory(read), ct);
                if (n == 0) throw new EndOfStreamException("El servidor cerró la conexión.");
                read += n;
            }
        }
    }
}

using System.Net.Sockets;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AykutOnPC.Web.Infrastructure;

/// <summary>
/// Lightweight Redis liveness probe that opens a raw TCP connection to host:port and
/// (optionally) sends an AUTH + PING. We deliberately avoid taking a hard dependency on
/// StackExchange.Redis here — Redis is currently used only for caching by the prod stack
/// and the app must remain runnable in dev where Redis is not present.
/// </summary>
public sealed class RedisHealthCheck(IConfiguration configuration, ILogger<RedisHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connStr = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(connStr))
        {
            // Redis not configured (dev or simple compose) — treat as healthy/skipped so /health stays green.
            return HealthCheckResult.Healthy("Redis not configured (skipped).");
        }

        var (host, port, password) = ParseConnectionString(connStr);

        try
        {
            using var client = new TcpClient { ReceiveTimeout = 2000, SendTimeout = 2000 };
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(host, port, connectCts.Token);
            using var stream = client.GetStream();
            stream.ReadTimeout  = 2000;
            stream.WriteTimeout = 2000;

            // Build a single RESP3 pipeline: AUTH (if password) + PING + QUIT
            var builder = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(password))
            {
                builder.Append("*2\r\n$4\r\nAUTH\r\n$").Append(password.Length).Append("\r\n").Append(password).Append("\r\n");
            }
            builder.Append("*1\r\n$4\r\nPING\r\n");
            builder.Append("*1\r\n$4\r\nQUIT\r\n");

            var payload = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            await stream.WriteAsync(payload, cancellationToken);

            var buffer = new byte[256];
            var read   = await stream.ReadAsync(buffer, cancellationToken);
            var reply  = System.Text.Encoding.UTF8.GetString(buffer, 0, read);

            if (reply.Contains("PONG"))
                return HealthCheckResult.Healthy($"Redis OK ({host}:{port}).");

            // PONG missing usually means AUTH failed (-NOAUTH or -WRONGPASS) — still treat as unhealthy
            // but don't echo the raw reply (could leak partial creds in logs).
            logger.LogWarning("Redis health probe completed without PONG. Host={Host}", host);
            return HealthCheckResult.Degraded($"Redis reachable on {host}:{port} but PING did not return PONG.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Redis probe timed out connecting to {host}:{port}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Redis unreachable ({host}:{port}).", ex);
        }
    }

    private static (string Host, int Port, string Password) ParseConnectionString(string connStr)
    {
        // Supported formats:
        //   redis:6379,password=xxx,ssl=False,abortConnect=False  (StackExchange.Redis style)
        //   redis:6379
        //   localhost:6379
        var parts    = connStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var endpoint = parts[0];
        var hostPort = endpoint.Split(':', 2);
        var host     = hostPort[0];
        var port     = hostPort.Length > 1 && int.TryParse(hostPort[1], out var p) ? p : 6379;

        var password = string.Empty;
        foreach (var kv in parts.Skip(1))
        {
            var eq = kv.IndexOf('=');
            if (eq <= 0) continue;
            var key = kv[..eq].Trim();
            var val = kv[(eq + 1)..].Trim();
            if (key.Equals("password", StringComparison.OrdinalIgnoreCase))
                password = val;
        }

        return (host, port, password);
    }
}

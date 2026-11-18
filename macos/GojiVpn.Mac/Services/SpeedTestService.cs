using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;

namespace GodjiVpn.Services;

/// <summary>
/// Замер скорости через поднятый туннель: весь трафик замера идёт через локальный SOCKS
/// xray — ровно тот же путь, что у пользовательских приложений (VLESS+Reality+узел),
/// поэтому цифры отражают реальную пропускную способность туннеля, а не канала мимо VPN.
/// Конечные точки speed.cloudflare.com — без авторизации; запросы уходят через выбранный
/// узел. Замер осмыслен только при подключённом VPN — вызывающий это проверяет и держит
/// кнопку выключенной. Каждая фаза ограничена по времени, а не по объёму: на медленном
/// канале не зависает, на быстром не успевает упереться в лимит файла.
/// </summary>
public static class SpeedTestService
{
    private const string DownUrl = "https://speed.cloudflare.com/__down?bytes=100000000";
    private const string UpUrl = "https://speed.cloudflare.com/__up";
    private static readonly TimeSpan PhaseLimit = TimeSpan.FromSeconds(10);

    public static async Task<(double DownMbps, double UpMbps)> RunAsync(int socksPort, CancellationToken ct = default)
    {
        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"socks5://127.0.0.1:{socksPort}"),
            UseProxy = true,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };

        var down = await MeasureDownloadAsync(http, ct).ConfigureAwait(false);
        var up = await MeasureUploadAsync(http, ct).ConfigureAwait(false);
        return (down, up);
    }

    private static async Task<double> MeasureDownloadAsync(HttpClient http, CancellationToken ct)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(ct);
        phase.CancelAfter(PhaseLimit);
        using var response = await http.GetAsync(DownUrl, HttpCompletionOption.ResponseHeadersRead, phase.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = response.Content.ReadAsStream(phase.Token);

        var buffer = new byte[64 * 1024];
        long total = 0;
        var watch = Stopwatch.StartNew();
        while (true)
        {
            var read = await stream.ReadAsync(buffer, phase.Token).ConfigureAwait(false);
            if (read <= 0) break;
            total += read;
            if (watch.Elapsed >= PhaseLimit) break;
        }
        watch.Stop();
        if (total == 0) throw new HttpRequestException("тестовая загрузка не принесла ни байта — туннель не пропускает трафик");
        return total * 8.0 / watch.Elapsed.TotalSeconds / 1_000_000.0;
    }

    private static async Task<double> MeasureUploadAsync(HttpClient http, CancellationToken ct)
    {
        using var phase = CancellationTokenSource.CreateLinkedTokenSource(ct);
        phase.CancelAfter(PhaseLimit);
        // Один и тот же буфер на все POST: содержимое для замера не важно, скорость канала
        // не зависит от энтропии, а генерить гигабайты случайных байт — лишняя работа CPU.
        var chunk = new byte[256 * 1024];
        Encoding.ASCII.GetBytes("0", chunk);

        long total = 0;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < PhaseLimit)
        {
            using var content = new ByteArrayContent(chunk);
            using var response = await http.PostAsync(UpUrl, content, phase.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            total += chunk.Length;
        }
        watch.Stop();
        if (total == 0) throw new HttpRequestException("тестовая отдача не отправила ни байта — туннель не пропускает трафик");
        return total * 8.0 / watch.Elapsed.TotalSeconds / 1_000_000.0;
    }
}

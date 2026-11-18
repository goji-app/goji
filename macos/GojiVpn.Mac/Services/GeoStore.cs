using System.IO;
using System.Net.Http;

namespace GodjiVpn.Services;

/// <summary>
/// Гео-базы xray (geoip.dat/geosite.dat): правила подписки и наши (geosite:category-ads-all
/// при «Блокировать рекламу») читаются их из каталога XRAY_LOCATION_ASSET. В бандле лежит
/// версия на момент сборки, поэтому свежие копии держим в пользовательском каталоге и
/// подставляем ядру его каталог (см. запуск xray в движке) — пока обновления не было,
/// каталог пуст и ядро берёт бандл, как раньше. Источник — свой сайт загрузки
/// (get.gojihub.xyz/geo, правила runetfreedom под нашу реальность, обновление там — раз
/// в 6 часов; см. publish.py проекта сайта). Обновление раз в неделю само и по кнопке
/// (Настройки → Подключение).
/// </summary>
public static class GeoStore
{
    public const string GeoIpUrl = "https://get.gojihub.xyz/geo/geoip.dat";
    public const string GeoSiteUrl = "https://get.gojihub.xyz/geo/geosite.dat";
    // Живые файлы — мегабайты (geosite ~85 МБ, geoip — несколько МБ); publishing на сайте
    // отбраковывает меньше 50 КБ. Меньше 200 КБ — точно мусор/заглушка прокладки, не применять.
    private const long MinBytes = 200_000;

    public static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GodjiVpn", "geo");
    private static string IpPath => Path.Combine(Dir, "geoip.dat");
    private static string SitePath => Path.Combine(Dir, "geosite.dat");

    /// <summary>Есть ли пользовательская пара гео-баз — её и отдаём ядру. Пара, а не по одному:
    /// разные поколения geoip/geosite между собой не конфликтуют, но рассинхрон проверок
    /// (ip_cidr против гео-категорий разных месяцев) даст труднодиагностируемое поведение.</summary>
    public static bool HasUserGeo() => File.Exists(IpPath) && File.Exists(SitePath);

    /// <summary>Скачивает обе базы во временные файлы и только после двух успешных загрузок
    /// подменяет основные: половинное обновление (новый geosite со старым geoip — или хуже,
    /// обрезанный файл) хуже старых рабочих баз. false — ничего не изменилось, старые остались.</summary>
    public static async Task<bool> UpdateAsync(Action<string>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Dir);
        try
        {
            var ipTmp = IpPath + ".new";
            var siteTmp = SitePath + ".new";
            try
            {
                await DownloadAsync(GeoIpUrl, ipTmp, ct).ConfigureAwait(false);
                progress?.Invoke("geoip.dat");
                await DownloadAsync(GeoSiteUrl, siteTmp, ct).ConfigureAwait(false);
                progress?.Invoke("geosite.dat");
                File.Move(ipTmp, IpPath, true);
                File.Move(siteTmp, SitePath, true);
                return true;
            }
            finally
            {
                TryDelete(ipTmp);
                TryDelete(siteTmp);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }
    }

    private static async Task DownloadAsync(string url, string targetPath, CancellationToken ct)
    {
        using var http = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromMinutes(6) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var content = response.Content.ReadAsStream(ct))
        await using (var file = new FileStream(targetPath + ".part", FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(file, ct).ConfigureAwait(false);
        }
        // Файл от прокладки/заглушки: HTML-страница вместо данных. Плюс проверка размера —
        // короткий ответ CDN при ошибке origin не должен стать гео-базой.
        await using var check = new FileStream(targetPath + ".part", FileMode.Open, FileAccess.Read, FileShare.Read);
        if (check.Length < MinBytes) throw new InvalidDataException($"подозрительно маленький файл ({check.Length} Б)");
        var head = new byte[16];
        var read = await check.ReadAsync(head, ct).ConfigureAwait(false);
        // HTML-заглушка прокладки начинается с '<' или '!': гео-база — бинарный protobuf.
        for (var i = 0; i < read; i++)
            if (head[i] is (byte)'<' or (byte)'!')
                throw new InvalidDataException("похоже на HTML-заглушку, а не на гео-базу");
        File.Move(targetPath + ".part", targetPath, true);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}

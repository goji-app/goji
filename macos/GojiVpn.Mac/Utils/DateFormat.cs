using System.Globalization;

namespace GodjiVpn.Utils;

public static class DateFormat
{
    private static CultureInfo Culture => CultureInfo.GetCultureInfo(GodjiVpn.Services.Loc.Instance.Lang switch { "en" => "en-GB", "zh" => "zh-CN", _ => "ru-RU" });

    /// <summary>"2026-09-05T16:11:51Z" → "5 сентября". Возвращает исходную строку, если
    /// распарсить не удалось. Портировано 1:1 из util/DateFormat.kt.</summary>
    public static string FormatDate(string isoDateTime)
    {
        if (!DateTimeOffset.TryParse(isoDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return isoDateTime;
        return parsed.ToLocalTime().ToString(GodjiVpn.Services.Loc.Instance.Lang == "zh" ? "M月d日" : "d MMMM", Culture);
    }

    /// <summary>"2026-09-05T16:11:51Z" → "5 сентября в 16:11" — для чата поддержки, где важно
    /// не только число, но и время сообщения (в отличие от FormatDate выше).</summary>
    public static string FormatDateTime(string isoDateTime)
    {
        if (!DateTimeOffset.TryParse(isoDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return isoDateTime;
        return parsed.ToLocalTime().ToString(GodjiVpn.Services.Loc.Instance.Lang switch { "en" => "d MMMM 'at' HH:mm", "zh" => "M月d日 HH:mm", _ => "d MMMM 'в' HH:mm" }, Culture);
    }
}

using System.Text;
using System.Text.Json.Nodes;
using GodjiVpn.Models;

namespace GodjiVpn.Services;

/// <summary>
/// Импорт узла по ссылке — стандартный пользовательский путь v2ray-клиентов: vless://,
/// vmess:// (base64-JSON), trojan://, ss:// (SIP002). Собирает из ссылки полный
/// клиентский профиль xray (тот же формат, что отдаёт подписка и что ждёт
/// CustomNodeStore/VpnEngine — с freedom-outbound "direct", чтобы работали наши правила
/// .ru и «Сайты мимо VPN»). Поля общего вида: scheme://userinfo@host:port?query#name.
/// </summary>
public static class NodeLinkParser
{
    public static (VlessNode? Node, string? Error) Parse(string input)
    {
        var raw = input.Trim();
        if (raw.Length == 0) return (null, null); // пусто — просто закрыли диалог
        if (raw.StartsWith("{")) return (null, "json"); // вызывающий отдаёт JSON в CustomNodeStore.Add как есть

        var schemeEnd = raw.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0) return (null, Loc.T("Не похоже на ссылку узла — поддерживаются vless://, vmess://, trojan:// и ss://"));
        var scheme = raw[..schemeEnd].ToLowerInvariant();
        var rest = raw[(schemeEnd + 3)..];

        // Имя узла — фрагмент после #, URL-декодированный.
        string? name = null;
        var hash = rest.IndexOf('#');
        if (hash >= 0)
        {
            name = Uri.UnescapeDataString(rest[(hash + 1)..]).Trim();
            rest = rest[..hash];
        }

        JsonObject outbound;
        string host;
        int port;
        string? uuid = null;

        try
        {
            switch (scheme)
            {
                case "vless":
                case "trojan":
                    (outbound, host, port, uuid) = ParseVlessLike(scheme, rest);
                    break;
                case "vmess":
                    (outbound, host, port, uuid) = ParseVmess(rest);
                    break;
                case "ss":
                    (outbound, host, port) = ParseShadowsocks(rest);
                    break;
                default:
                    return (null, Loc.T("Неизвестный протокол — поддерживаются vless://, vmess://, trojan:// и ss://"));
            }
        }
        catch
        {
            return (null, Loc.T("Не удалось разобрать ссылку — проверьте, что она скопирована целиком"));
        }

        if (string.IsNullOrWhiteSpace(host)) return (null, Loc.T("В ссылке нет адреса сервера"));
        if (string.IsNullOrWhiteSpace(name)) name = $"{host}:{port}";

        var profile = new JsonObject
        {
            ["remarks"] = name,
            ["log"] = new JsonObject { ["loglevel"] = "warning" },
            ["inbounds"] = new JsonArray(),
            ["outbounds"] = new JsonArray(
                outbound,
                new JsonObject { ["tag"] = "direct", ["protocol"] = "freedom", ["settings"] = new JsonObject() }),
            ["routing"] = new JsonObject { ["domainStrategy"] = "AsIs", ["rules"] = new JsonArray() }
        };

        return (new VlessNode
        {
            Id = "custom-" + Guid.NewGuid().ToString("N")[..8],
            Name = name,
            Host = host,
            Port = port,
            ConnectPayloadJson = profile.ToJsonString(),
            Uuid = uuid
        }, null);
    }

    /// <summary>vless://uuid@host:port?… и trojan://password@host:port?… — отличаются полем
    /// учётки (id+flow vs password) и тем, что у trojan streamSettings бывает без security.</summary>
    private static (JsonObject Outbound, string Host, int Port, string? Uuid) ParseVlessLike(string scheme, string rest)
    {
        var at = rest.LastIndexOf('@');
        if (at < 0) throw new FormatException("нет @");
        var secret = Uri.UnescapeDataString(rest[..at]);
        var hostPortQuery = rest[(at + 1)..];
        var q = hostPortQuery.IndexOf('?');
        var hostPort = q >= 0 ? hostPortQuery[..q] : hostPortQuery;
        var query = ParseQuery(q >= 0 ? hostPortQuery[q..] : "");

        var colon = hostPort.LastIndexOf(':');
        if (colon < 0) throw new FormatException("нет порта");
        var host = colon > 0 && hostPort.StartsWith('[')
            ? hostPort[1..(hostPort.IndexOf(']'))] // IPv6 [::1]:443
            : hostPort[..colon];
        var port = int.TryParse(hostPort[(colon + 1)..], out var p) ? p : 443;

        string? sni = query.TryGetValue("sni", out var s) ? s : null;
        string? fp = query.TryGetValue("fp", out var f) ? f : null;
        var network = query.TryGetValue("type", out var t) && t.Length > 0 ? t : "tcp";
        var security = query.TryGetValue("security", out var sec) && sec.Length > 0 ? sec
            : query.ContainsKey("pbk") ? "reality" : "none";

        var streamSettings = new JsonObject();
        if (network == "ws")
        {
            var ws = new JsonObject();
            if (query.TryGetValue("path", out var path) && path.Length > 0) ws["path"] = path;
            if (query.TryGetValue("host", out var wsHost) && wsHost.Length > 0)
                ws["headers"] = new JsonObject { ["Host"] = wsHost };
            streamSettings["wsSettings"] = ws;
        }
        else if (network == "grpc")
        {
            var grpc = new JsonObject();
            if (query.TryGetValue("serviceName", out var svc) && svc.Length > 0) grpc["serviceName"] = svc;
            streamSettings["grpcSettings"] = grpc;
        }
        else if (network == "xhttp")
        {
            var xhttp = new JsonObject();
            if (query.TryGetValue("path", out var xp) && xp.Length > 0) xhttp["path"] = xp;
            if (query.TryGetValue("host", out var xh) && xh.Length > 0) xhttp["host"] = xh;
            streamSettings["xhttpSettings"] = xhttp;
        }
        else if (network == "http")
        {
            var http = new JsonObject();
            if (query.TryGetValue("path", out var hp2) && hp2.Length > 0) http["path"] = hp2;
            if (query.TryGetValue("host", out var hh) && hh.Length > 0)
                http["host"] = new JsonArray(hh);
            streamSettings["httpSettings"] = http;
        }

        if (security == "reality")
        {
            var reality = new JsonObject();
            if (sni != null) reality["serverName"] = sni;
            reality["fingerprint"] = string.IsNullOrEmpty(fp) ? "chrome" : fp;
            if (query.TryGetValue("pbk", out var pbk)) reality["publicKey"] = pbk;
            if (query.TryGetValue("sid", out var sid)) reality["shortId"] = sid;
            reality["spiderX"] = query.TryGetValue("spx", out var spx) ? spx : "/";
            streamSettings["security"] = "reality";
            streamSettings["realitySettings"] = reality;
        }
        else if (security == "tls")
        {
            var tls = new JsonObject();
            if (sni != null) tls["serverName"] = sni;
            if (!string.IsNullOrEmpty(fp)) tls["fingerprint"] = fp;
            if (query.TryGetValue("alpn", out var alpn) && alpn.Length > 0)
                tls["alpn"] = new JsonArray(alpn.Split(',').Select(a => a.Trim()).Where(a => a.Length > 0)
                    .Select(a => (JsonNode)JsonValue.Create(a)!).ToArray());
            streamSettings["security"] = "tls";
            streamSettings["tlsSettings"] = tls;
        }

        JsonObject outbound;
        string? uuid = null;
        if (scheme == "vless")
        {
            uuid = secret;
            var user = new JsonObject { ["id"] = secret, ["encryption"] = "none" };
            if (query.TryGetValue("flow", out var flow) && flow.Length > 0) user["flow"] = flow;
            outbound = new JsonObject
            {
                ["protocol"] = "vless",
                ["settings"] = new JsonObject
                {
                    ["vnext"] = new JsonArray(new JsonObject
                    {
                        ["address"] = host,
                        ["port"] = port,
                        ["users"] = new JsonArray(user)
                    })
                }
            };
        }
        else
        {
            outbound = new JsonObject
            {
                ["protocol"] = "trojan",
                ["settings"] = new JsonObject
                {
                    ["servers"] = new JsonArray(new JsonObject
                    {
                        ["address"] = host,
                        ["port"] = port,
                        ["password"] = secret
                    })
                }
            };
        }
        outbound["streamSettings"] = streamSettings;
        return (outbound, host, port, uuid);
    }

    /// <summary>vmess://BASE64({"v":"2","ps":имя,"add":адрес,"port":"443","id":uuid,"aid":"0",
    /// "net":"ws","path":…,"host":…,"tls":"tls","sni":…,"fp":…}) — формат v2rayN.</summary>
    private static (JsonObject Outbound, string Host, int Port, string Uuid) ParseVmess(string rest)
    {
        var json = DecodeBase64(rest);
        if (json == null) throw new FormatException("не base64");
        var o = JsonNode.Parse(json)?.AsObject() ?? throw new FormatException("не JSON");

        var host = Text(o["add"]) ?? throw new FormatException("нет адреса");
        var port = int.TryParse(Text(o["port"]), out var p) ? p : 443;
        var id = Text(o["id"]) ?? throw new FormatException("нет id");
        var network = Text(o["net"]) is { Length: > 0 } n ? n : "tcp";
        var security = Text(o["tls"]) == "tls" ? "tls" : "none";
        var sni = Text(o["sni"]);
        if (string.IsNullOrEmpty(sni)) sni = Text(o["host"]);
        var fp = Text(o["fp"]);

        var streamSettings = new JsonObject();
        if (network == "ws")
        {
            var ws = new JsonObject();
            var path = Text(o["path"]);
            if (!string.IsNullOrEmpty(path)) ws["path"] = path;
            var wsHost = Text(o["host"]);
            if (!string.IsNullOrEmpty(wsHost)) ws["headers"] = new JsonObject { ["Host"] = wsHost };
            streamSettings["wsSettings"] = ws;
        }
        else if (network == "grpc")
        {
            var grpc = new JsonObject();
            var svc = Text(o["path"]);
            if (!string.IsNullOrEmpty(svc)) grpc["serviceName"] = svc;
            streamSettings["grpcSettings"] = grpc;
        }
        if (security == "tls")
        {
            var tls = new JsonObject();
            if (!string.IsNullOrEmpty(sni)) tls["serverName"] = sni;
            if (!string.IsNullOrEmpty(fp)) tls["fingerprint"] = fp;
            streamSettings["security"] = "tls";
            streamSettings["tlsSettings"] = tls;
        }

        var aid = int.TryParse(Text(o["aid"]) ?? "0", out var a) ? a : 0;
        var outbound = new JsonObject
        {
            ["protocol"] = "vmess",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray(new JsonObject
                {
                    ["address"] = host,
                    ["port"] = port,
                    ["users"] = new JsonArray(new JsonObject
                    {
                        ["id"] = id,
                        ["alterId"] = aid,
                        ["security"] = "auto"
                    })
                })
            },
            ["streamSettings"] = streamSettings
        };
        return (outbound, host, port, id);
    }

    /// <summary>ss:// — два варианта SIP002: ss://base64(method:pass)@host:port#name и
    /// старый ss://base64(method:pass@host:port)#name.</summary>
    private static (JsonObject Outbound, string Host, int Port) ParseShadowsocks(string rest)
    {
        string method, password, hostPort;
        var at = rest.LastIndexOf('@');
        if (at >= 0)
        {
            var decoded = DecodeBase64(rest[..at]) ?? Uri.UnescapeDataString(rest[..at]);
            var sep = decoded.IndexOf(':');
            if (sep < 0) throw new FormatException("нет метода шифрования");
            method = decoded[..sep];
            password = decoded[(sep + 1)..];
            hostPort = rest[(at + 1)..];
        }
        else
        {
            var hash = rest.IndexOf("#", StringComparison.Ordinal);
            if (hash >= 0) rest = rest[..hash];
            var slash = rest.IndexOf('/');
            if (slash >= 0) rest = rest[..slash];
            var decoded = DecodeBase64(rest) ?? throw new FormatException("не base64");
            var sep = decoded.IndexOf('@');
            if (sep < 0) throw new FormatException("нет @");
            var cred = decoded[..sep];
            hostPort = decoded[(sep + 1)..];
            var csep = cred.IndexOf(':');
            if (csep < 0) throw new FormatException("нет метода шифрования");
            method = cred[..csep];
            password = cred[(csep + 1)..];
        }

        var colon = hostPort.LastIndexOf(':');
        if (colon < 0) throw new FormatException("нет порта");
        var host = hostPort[..colon].Trim('[', ']');
        var port = int.TryParse(hostPort[(colon + 1)..], out var p) ? p : 443;

        var outbound = new JsonObject
        {
            ["protocol"] = "shadowsocks",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray(new JsonObject
                {
                    ["address"] = host,
                    ["port"] = port,
                    ["method"] = method,
                    ["password"] = password
                })
            }
        };
        return (outbound, host, port);
    }

    /// <summary>Строковое поле vmess-JSON может прийти и числом — GetValue<string> на числе
    /// бросает исключение, поэтому читаем аккуратно: строка как есть, остальное — JSON-текст.</summary>
    private static string? Text(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) ? s
        : node?.ToJsonString().Trim('"');

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (query.StartsWith('?')) query = query[1..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = eq >= 0 ? pair[..eq] : pair;
            var value = eq >= 0 ? Uri.UnescapeDataString(pair[(eq + 1)..]) : "";
            result[key] = value;
        }
        return result;
    }

    private static string? DecodeBase64(string text)
    {
        var clean = text.Trim().Replace('-', '+').Replace('_', '/').TrimEnd('=');
        // Длина кратна 4 — дописываем стандартный паддинг; base64url без него падал бы.
        clean += (4 - clean.Length % 4) % 4 == 0 ? "" : new string('=', 4 - clean.Length % 4);
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(clean)); }
        catch { return null; }
    }
}

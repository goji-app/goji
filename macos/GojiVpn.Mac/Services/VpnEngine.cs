// Заглушка. Настоящий движок VPN (запуск xray и sing-box, root-обёртка туннеля, сборка
// конфигурации, kill switch) в репозитории не хранится — как и GodjiVpnService.kt на Android.
// Он лежит в «Приложения Goji\macOS\Приватное\VpnEngine.cs» и подключается к сборке через
// build\GojiPrivate.targets; в CI приходит из секрета GOJI_PRIVATE_MACOS.
// Этот файл в сборку не входит; не заменять его настоящим.

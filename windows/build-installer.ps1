# Полная пересборка Godji VPN: dotnet publish (self-contained win-x64) + Obfuscar +
# сборка собственного WPF-установщика (installer\Setup). Результат — единственный файл
# dist\GodjiVpn-Setup-<версия>.exe; промежуточные папки после сборки удаляются.
#
# Установщик — отдельное приложение GodjiSetup (WPF, фирменный дизайн «Афиша×Прибор»):
#   1) publish приложения → publish_stage (как раньше);
#   2) publish_stage упаковывается в payload.bin (LZMA-контейнер, installer/PayloadTool);
#   3) payload.bin встраивается в GodjiSetup.exe как EmbeddedResource → dist\GodjiVpn-Setup-<v>.exe.
# Отдельного деинсталлятора нет: unins000.exe в папке установки — это копия самого установщика
# (InstallerEngine кладёт её после распаковки), он же работает с /uninstall и /quiet.
# Inno Setup больше не нужен. Payload живёт внутри сборки, поэтому при первом запуске
# .NET-хост распаковывает сборку в %TEMP%\.net\GodjiSetup\ — это цена одиночного файла.
#
# Требования: .NET 8 SDK. Установщик собирается с -p:EmbedPayload=true / false
#   (встраивать payload.bin) и -p:Version=<версия из csproj>.
# Запуск: powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1
# Время: ~35 мин (LZMA-кодирование payload) + ~10 мин (publish/обфускация/публикация).

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$proj = Join-Path $root "GodjiVpn\GodjiVpn.csproj"
$setupProj = Join-Path $root "installer\Setup\Setup.csproj"
$publishDir = Join-Path $root "publish_stage"
$uninsDir = Join-Path $root "setup_stage_unins"
$fullDir = Join-Path $root "setup_stage_full"
$payloadBin = Join-Path $root "installer\Setup\payload.bin"

Write-Host "== Проверка ядра (Runtime\SHA256SUMS) ==" -ForegroundColor Cyan
# xray.exe и sing-box.exe в git не хранятся — сверяем, что в установщик уйдут именно те версии.
$runtimeDir = Join-Path $root "GodjiVpn\Runtime"
foreach ($line in Get-Content (Join-Path $runtimeDir "SHA256SUMS")) {
    $expected, $name = $line -split '\s+', 2
    $actual = (Get-FileHash (Join-Path $runtimeDir $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw "Runtime\$name не совпадает с Runtime\SHA256SUMS: $actual" }
}

Write-Host "== Очистка старых артефактов сборки ==" -ForegroundColor Cyan
Remove-Item -Recurse -Force (Join-Path $root "GodjiVpn\bin"), (Join-Path $root "GodjiVpn\obj"),
    $publishDir, $uninsDir, $fullDir, $payloadBin -ErrorAction SilentlyContinue

Write-Host "== dotnet publish (Release, self-contained, win-x64) ==" -ForegroundColor Cyan
dotnet publish $proj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:PublishTrimmed=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с ошибкой" }

Write-Host "== Удаление лишнего из publish (pdb/xml/локали) ==" -ForegroundColor Cyan
Remove-Item -Force (Join-Path $publishDir "GodjiVpn.pdb"), `
    (Join-Path $publishDir "Microsoft.Web.WebView2.Core.xml"), `
    (Join-Path $publishDir "Microsoft.Web.WebView2.WinForms.xml"), `
    (Join-Path $publishDir "Microsoft.Web.WebView2.Wpf.xml") -ErrorAction SilentlyContinue
Remove-Item -Force (Join-Path $publishDir "Runtime\README.txt"), (Join-Path $publishDir "Runtime\SHA256SUMS") -ErrorAction SilentlyContinue
$satelliteLocales = "cs","de","es","fr","it","ja","ko","pl","pt-BR","ru","tr","zh-Hans","zh-Hant"
foreach ($loc in $satelliteLocales) {
    Remove-Item -Recurse -Force (Join-Path $publishDir $loc) -ErrorAction SilentlyContinue
}

Write-Host "== Обфускация GodjiVpn.dll (Obfuscar, см. obfuscar.xml) ==" -ForegroundColor Cyan
# Публикация идёт без single-file/trimming, поэтому GodjiVpn.dll — читаемый в ILSpy IL-код
# с именами приватных классов и строками (URL, сообщения). Obfuscar переименовывает
# приватные/внутренние члены и шифрует строковые литералы; публичный API остаётся как есть —
# это безопасный для WPF профиль (XAML/биндинги резолвятся по публичным именам). Конфиг и
# обоснование — obfuscar.xml рядом со скриптом; версия инструмента зафиксирована в
# .config/dotnet-tools.json (dotnet tool restore ставит её в %USERPROFILE%\.dotnet\tools).
Push-Location $root
try {
    # Манифест ищется от текущей папки (windows\.config\dotnet-tools.json);
    # команда запускается через dotnet tool run — не зависит от PATH (%USERPROFILE%\.dotnet\tools).
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore завершился с ошибкой" }
    dotnet tool run obfuscar.console -- obfuscar.xml
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar завершился с ошибкой" }
    # Obfuscar пишет результат в publish_stage_obf — забираем из него только сборку приложения.
    Copy-Item (Join-Path $root "publish_stage_obf\GodjiVpn.dll") $publishDir -Force
} finally {
    Pop-Location
}
Remove-Item -Recurse -Force (Join-Path $root "publish_stage_obf") -ErrorAction SilentlyContinue

# Версия — из GodjiVpn.csproj, чтобы установщик не отставал от приложения.
$version = ([xml](Get-Content $proj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Не нашёл <Version> в $proj" }

Write-Host "== Упаковка payload (publish_stage -> payload.bin, LZMA d64 fb273) ==" -ForegroundColor Cyan
# Потоковый контейнер PayloadTool: header (кол-во файлов/байты) открытым текстом + LZMA-поток
# записей (словарь 64 МБ, fastbytes 273 — профиль прежнего Inno lzma2/max; декодер — тот же
# пакет LZMA-SDK в Setup). unins000.exe в publish_stage НЕ кладём: деинсталлятором становится
# сам установщик (копия в папке установки), иначе ~60 МБ уже сжатого кода ложатся в payload
# почти без сжатия и раздували установщик с ~148 до ~200 МБ.
dotnet run --project (Join-Path $root "installer\PayloadTool\PayloadTool.csproj") -c Release -- $publishDir $payloadBin
if ($LASTEXITCODE -ne 0) { throw "PayloadTool завершился с ошибкой" }

Write-Host "== Установщик (GodjiSetup с payload) -> dist\GodjiVpn-Setup-$version.exe ==" -ForegroundColor Cyan
dotnet publish $setupProj -c Release -r win-x64 -p:EmbedPayload=true -p:Version=$version -o $fullDir
if ($LASTEXITCODE -ne 0) { throw "publish установщика завершился с ошибкой" }
Remove-Item -Force (Join-Path $fullDir "*.pdb") -ErrorAction SilentlyContinue

$outExe = Join-Path $root "dist\GodjiVpn-Setup-$version.exe"
New-Item -ItemType Directory -Force (Join-Path $root "dist") | Out-Null
Move-Item -Force (Join-Path $fullDir "GodjiSetup.exe") $outExe

Write-Host "== Уборка промежуточных папок ==" -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir, $uninsDir, $fullDir, $payloadBin, `
    (Join-Path $root "installer\Setup\bin"), (Join-Path $root "installer\Setup\obj"), `
    (Join-Path $root "installer\PayloadTool\bin"), (Join-Path $root "installer\PayloadTool\obj"), `
    (Join-Path $root "GodjiVpn\bin"), (Join-Path $root "GodjiVpn\obj") -ErrorAction SilentlyContinue

$hash = (Get-FileHash $outExe -Algorithm SHA256).Hash.ToLowerInvariant()
$size = (Get-Item $outExe).Length
Write-Host "Готово: $outExe ($size байт)" -ForegroundColor Green
Write-Host "SHA-256: $hash" -ForegroundColor Green

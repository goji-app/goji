# Goji VPN для macOS

Клиент Goji VPN для Mac: Avalonia 11 / .NET 8, Xray-core + sing-box (TUN).
Интерфейс и возможности такие же, как у Windows- и Android-версий: дизайн «Goji Expressive», глобус,
серверы с пингом и избранным, подписка, поддержка прямо в приложении, «Сайты мимо VPN» и
«Приложения мимо VPN», kill switch, проверка утечек, значок в строке меню с состоянием подключения,
отчёт о сбое одним нажатием, экраны знакомства при первом запуске.

Работает на **macOS 10.15 Catalina и новее** (Mac с Intel) и **macOS 11 Big Sur и новее**
(Mac с Apple Silicon). На macOS 13+ работает ядро Xray 26.10.21, на 10.15–12 — 26.3.27
(новое ядро собрано Go 1.27, а тот поддерживает только macOS 13+; см. `Runtime/README.md`).

## Установка

1. Скачайте установщик с сайта загрузки (раздел macOS):
   `GojiVPN-<версия>-macOS.dmg` — один для любого Mac (Intel и Apple Silicon).
2. Откройте `.dmg` и перетащите **Goji VPN** в «Программы».
3. Приложение не заверено Apple, поэтому при первом запуске macOS его остановит. Откройте
   «Системные настройки → Конфиденциальность и безопасность» и нажмите «Всё равно открыть».
   Другой способ — выполнить в Терминале:
   ```
   xattr -dr com.apple.quarantine "/Applications/Goji VPN.app"
   ```
4. При первом подключении macOS попросит пароль администратора один раз: он нужен, чтобы
   установить службу, создающую VPN-интерфейс для всего трафика Mac. Дальше пароль не нужен
   (снова спросит только после обновления, изменившего службу). Если служба недоступна,
   приложение вернётся к запросу пароля при каждом подключении. Удалить службу:
   ```
   sudo launchctl bootout system/xyz.goji.vpn.tun; sudo rm -rf "/Library/Application Support/GojiVPN" /Library/LaunchDaemons/xyz.goji.vpn.tun.plist
   ```

## Сборка

Основной путь — GitHub Actions (`.github/workflows/macos.yml`, запуск вручную): сборка на Mac,
подпись `codesign`, универсальный установщик `.dmg`, zip для автообновления и пробный запуск со снимками экрана.

Локально (на Windows или macOS):

Нужны .NET 8 SDK, Python 3 с Pillow и `tools/rcodesign` (ad-hoc подпись без Mac).
Ядра под обе архитектуры кладутся в `Runtime/arm64` и `Runtime/x64` — см. `Runtime/README.md`.

```
python build-macos.py            # обе архитектуры
python build-macos.py arm64      # только Apple Silicon
```

Результат — `dist/GojiVPN-<версия>-macOS-<arch>.zip`.

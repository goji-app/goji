#!/bin/bash
# Проверка заявки к root-демону туннеля: шапка goji-tund.sh (DaemonHeader из VpnEngine.cs) должна
# принять корректную заявку, отбросить небезопасные имена приложений и выйти на неверном номере сессии.
# Без root и без sing-box: вместо тела обёртки выводим аргументы. Запуск: GOJI_PRIVATE_DIR=... bash daemon-header-test.sh
set -u
work=$(mktemp -d); status=0
sed -n '/const string DaemonHeader = """/,/""";/p' "${GOJI_PRIVATE_DIR:?нет приватного кода туннеля}/VpnEngine.cs" \
  | tr -d '\r' | sed '1d;$d' | sed 's/^            //' \
  | sed "s/@APPNAME@/GojiVpn/g;s/@DNS@/1.1.1.1/g;s/@TUNDNS@/1.1.1.1/g;s/@ADAPTER@/172.19.0.1/g;s/@SOCKS@/10808/g" \
  | sed "s#^D=.*#D=$work/d#;s#^export LC_ALL=C PATH=.*#export LC_ALL=C#" > "$work/h.sh"
echo 'echo "ARGS: $*"; cat "$RUN/sing-box-config.json"' >> "$work/h.sh"
mkdir -p "$work/d/req"
sid=0123456789abcdef0123456789abcdef

printf '%s\n' $sid 4242 Telegram 'bad"name' 'Good App' 'x;rm -rf' > "$work/d/req/start"
out=$(bash "$work/h.sh")
echo "$out" | grep -q "ARGS: .*sing-box .*4242 .*stop.$sid .*tun.$sid.pid" || { echo "FAIL: аргументы обёртки"; status=1; }
echo "$out" | grep -q '"process_name":\["GojiVpn","Telegram","Good App"\]' || { echo "FAIL: имена приложений"; status=1; }
[ -e "$work/d/req/start" ] && { echo "FAIL: заявка не удалена"; status=1; }

printf '%s\n' 'bad sid' 4242 > "$work/d/req/start"
[ -z "$(bash "$work/h.sh")" ] || { echo "FAIL: неверный номер сессии принят"; status=1; }
printf '%s\n' $sid '42;42' > "$work/d/req/start"
[ -z "$(bash "$work/h.sh")" ] || { echo "FAIL: неверный pid принят"; status=1; }
touch "$work/d/req/stop.$sid"; bash "$work/h.sh" >/dev/null
ls "$work/d/req"/stop.* >/dev/null 2>&1 && { echo "FAIL: хвост stop не убран"; status=1; }

[ $status = 0 ] && echo "OK: заявка демона проверяется правильно"
rm -rf "$work"; exit $status

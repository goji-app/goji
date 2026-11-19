#!/bin/bash
# Проверка заявки к root-демону туннеля: шапка goji-tund.sh (DaemonHeader из VpnEngine.cs) должна
# принять корректную заявку, отбросить небезопасные имена приложений и выйти на неверном номере сессии.
# Собранный конфиг sing-box после каждого режима проверяется парсером JSON — битый конфиг
# (например, правило без фигурных скобок) роняет sing-box на старте: FATAL decode config.
# Без root и без sing-box: вместо тела обёртки выводим аргументы. Запуск: GOJI_PRIVATE_DIR=... bash daemon-header-test.sh
set -u
work=$(mktemp -d); status=0
PY=python; python3 -c "pass" 2>/dev/null && PY=python3
sed -n '/const string DaemonHeader = """/,/""";/p' "${GOJI_PRIVATE_DIR:?нет приватного кода туннеля}/VpnEngine.cs" \
  | tr -d '\r' | sed '1d;$d' | sed 's/^            //' \
  | sed "s/@APPNAME@/GojiVpn/g;s/@DNS@/1.1.1.1/g;s/@TUNDNS@/1.1.1.1/g;s/@ADAPTER@/172.19.0.1/g;s/@SOCKS@/10808/g" \
  | sed "s#^D=.*#D=$work/d#;s#^export LC_ALL=C PATH=.*#export LC_ALL=C#" > "$work/h.sh"
echo 'echo "ARGS: $*"; tee "$D/../cfg.json" < "$RUN/sing-box-config.json"' >> "$work/h.sh"
mkdir -p "$work/d/req"

# Заявка + проверки шапки + конфиг обязан быть валидным JSON.
run_req() {
  out=$(bash "$work/h.sh")
  "$PY" -m json.tool "$work/cfg.json" >/dev/null 2>&1 || { echo "FAIL: конфиг не валидный JSON ($1)"; status=1; cat "$work/cfg.json"; }
}
sid=0123456789abcdef0123456789abcdef

printf '%s\n' $sid 4242 Telegram 'bad"name' 'Good App' 'x;rm -rf' > "$work/d/req/start"
run_req "обход по умолчанию"
echo "$out" | grep -q "ARGS: .*sing-box .*4242 .*stop.$sid .*tun.$sid.pid" || { echo "FAIL: аргументы обёртки"; status=1; }
echo "$out" | grep -q '"process_name":\["GojiVpn","Telegram","Good App"\]' || { echo "FAIL: имена приложений"; status=1; }
echo "$out" | grep -q '"final":"socks-out"' || { echo "FAIL: обход по умолчанию (заявка без @MODE — старое приложение)"; status=1; }
[ -e "$work/d/req/start" ] && { echo "FAIL: заявка не удалена"; status=1; }

# Per-app Proxy: режим ALLOW — только выбранные через SOCKS (и их DNS), остальное напрямую.
printf '%s\n' $sid 4242 '@MODE ALLOW' Telegram 'bad"name' 'Good App' > "$work/d/req/start"
run_req "ALLOW"
echo "$out" | grep -q '"protocol":"dns","process_name":\["Telegram","Good App"\],"action":"hijack-dns"' || { echo "FAIL: ALLOW DNS"; status=1; }
echo "$out" | grep -q '{"process_name":\["Telegram","Good App"\],"outbound":"socks-out"' || { echo "FAIL: ALLOW socks"; status=1; }
echo "$out" | grep -q '"final":"direct"' || { echo "FAIL: ALLOW final"; status=1; }

# ALLOW с пустым списком: никаких правил по приложениям, всё напрямую.
printf '%s\n' $sid 4242 '@MODE ALLOW' > "$work/d/req/start"
run_req "ALLOW без списка"
echo "$out" | grep -q '"process_name":\["Telegram"' && { echo "FAIL: ALLOW без списка дал правила"; status=1; }
echo "$out" | grep -q '"final":"direct"' || { echo "FAIL: ALLOW без списка, final"; status=1; }

# OFF: список программ не действует (заявка его присылает, но конфиг обязан его отбросить).
printf '%s\n' $sid 4242 '@MODE OFF' Telegram 'Good App' > "$work/d/req/start"
run_req "OFF"
echo "$out" | grep -q '"process_name":\["GojiVpn"\],"outbound":"direct"' || { echo "FAIL: OFF: само приложение не напрямую"; status=1; }
echo "$out" | grep -q 'Telegram' && { echo "FAIL: OFF учёл список программ"; status=1; }
echo "$out" | grep -q '"final":"socks-out"' || { echo "FAIL: OFF final"; status=1; }

# Неизвестный режим — как обход (защита от опечатки в будущих версиях).
printf '%s\n' $sid 4242 '@MODE WHAT' Telegram > "$work/d/req/start"
run_req "неизвестный режим"
echo "$out" | grep -q '"final":"socks-out"' || { echo "FAIL: неизвестный режим"; status=1; }

printf '%s\n' 'bad sid' 4242 > "$work/d/req/start"
[ -z "$(bash "$work/h.sh")" ] || { echo "FAIL: неверный номер сессии принят"; status=1; }
printf '%s\n' $sid '42;42' > "$work/d/req/start"
[ -z "$(bash "$work/h.sh")" ] || { echo "FAIL: неверный pid принят"; status=1; }
touch "$work/d/req/stop.$sid"; bash "$work/h.sh" >/dev/null
ls "$work/d/req"/stop.* >/dev/null 2>&1 && { echo "FAIL: хвост stop не убран"; status=1; }

[ $status = 0 ] && echo "OK: заявка демона проверяется правильно"
rm -rf "$work"; exit $status

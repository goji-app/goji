import Foundation

/// Локализованная строка из Localizable.strings (ru — основной язык, en — второй).
/// Язык выбирает система (Настройки iOS → Goji VPN → Язык), отдельного переключателя в
/// приложении нет — так рекомендует Apple для приложений с несколькими локализациями.
func L(_ key: String) -> String {
    NSLocalizedString(key, bundle: AppLanguage.bundle, comment: "")
}

/// Строка с подстановкой аргументов (%@, %d, %lld в тексте перевода).
func L(_ key: String, _ args: CVarArg...) -> String {
    String(format: NSLocalizedString(key, bundle: AppLanguage.bundle, comment: ""), locale: Locale.current, arguments: args)
}

enum AppLanguage {
    /// Язык, выбранный на экране входа или в настройках («ru»/«en»); nil — как в системе.
    static var override: String? {
        get { UserDefaults.standard.string(forKey: "app.lang") }
        set { UserDefaults.standard.set(newValue, forKey: "app.lang") }
    }
    /// Язык интерфейса: выбор пользователя, иначе тот, что iOS выбрала для приложения.
    static var code: String { override ?? Bundle.main.preferredLocalizations.first ?? "ru" }
    /// Пакет строк выбранного языка (для L()).
    static var bundle: Bundle {
        guard let o = override, let path = Bundle.main.path(forResource: o, ofType: "lproj"), let b = Bundle(path: path) else { return .main }
        return b
    }
    static var isRussian: Bool { code.hasPrefix("ru") }
}

enum Plural {
    /// Русские формы «1 день / 2 дня / 5 дней»; для английского — one/other.
    /// Ключи в Localizable.strings: "<base>.one", "<base>.few", "<base>.many".
    static func form(_ n: Int, _ base: String) -> String {
        let isRussian = AppLanguage.isRussian
        let suffix: String
        if isRussian {
            let mod10 = n % 10, mod100 = n % 100
            if mod10 == 1 && mod100 != 11 { suffix = "one" }
            else if (2...4).contains(mod10) && !(12...14).contains(mod100) { suffix = "few" }
            else { suffix = "many" }
        } else {
            suffix = n == 1 ? "one" : "many"
        }
        return L("\(base).\(suffix)", n)
    }
}

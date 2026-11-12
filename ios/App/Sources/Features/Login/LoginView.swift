import SwiftUI

struct LoginView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var settings: AppSettings
    @Environment(\.openURL) private var openURL
    @State private var email = ""
    @State private var sending = false
    @State private var error: String?
    @State private var codeSentTo = ""
    @State private var showVerify = false
    @State private var showScanner = false
    @FocusState private var emailFocused: Bool

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                // Макет 9a «Вход»: заголовок, подсказка, поле только с нижней линией, плоская кнопка, QR-вход текстом.
                HStack(spacing: 14) {
                    ForEach([("ru", "РУССКИЙ"), ("en", "ENGLISH")], id: \.0) { code, title in
                        Button { settings.languageCode = code } label: {
                            Text(title).font(GojiFont.mono(12))
                                .fontWeight(settings.languageCode == code ? .bold : .regular)
                                .foregroundStyle(settings.languageCode == code ? GojiColor.textPrimary : GojiColor.textSecondary)
                                .padding(.vertical, 8)
                        }
                    }
                }
                .padding(.top, 16)
                Text(AppLanguage.isRussian ? "Войти по почте" : "Sign in by email")
                    .font(GojiFont.title())
                    .foregroundStyle(GojiColor.textPrimary)
                    .padding(.top, 8)
                    .accessibilityAddTraits(.isHeader)
                Text(AppLanguage.isRussian
                     ? "Пришлём код из шести цифр. Пароль не нужен. Первые 3 дня бесплатно."
                     : "We'll send a six-digit code. No password. First 3 days are free.")
                    .font(GojiFont.manrope(15))
                    .foregroundStyle(GojiColor.textSecondary)
                Text(AppLanguage.isRussian ? "ПОЧТА" : "EMAIL")
                    .font(GojiFont.mono(11))
                    .foregroundStyle(GojiColor.textSecondary)
                    .padding(.top, 16)
                VStack(spacing: 10) {
                    TextField("name@example.com", text: $email)
                        .textContentType(.emailAddress)
                        .keyboardType(.emailAddress)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .submitLabel(.send)
                        .focused($emailFocused)
                        .onSubmit { Task { await send() } }
                        .font(GojiFont.manrope(18))
                        .foregroundStyle(GojiColor.textPrimary)
                        .accessibilityLabel(L("login.email.label"))
                    Rectangle().fill(GojiColor.textPrimary).frame(height: 1)
                }
                if let error {
                    Text(error)
                        .font(GojiFont.caption())
                        .foregroundStyle(GojiColor.danger)
                }
                Button {
                    Task { await send() }
                } label: {
                    Group {
                        if sending {
                            ProgressView().tint(GojiColor.inkButtonText)
                        } else {
                            Text(L("login.get_code").uppercased())
                                .font(GojiFont.mono(14, .bold))
                                .tracking(0.8)
                        }
                    }
                    .foregroundStyle(GojiColor.inkButtonText)
                    .frame(maxWidth: .infinity)
                    .frame(height: 56)
                    .background(RoundedRectangle(cornerRadius: 2).fill(GojiColor.inkButton))
                }
                .buttonStyle(.plain)
                .disabled(sending)
                .padding(.top, 14)
                Text(L("login.no_account"))
                    .font(GojiFont.caption())
                    .foregroundStyle(GojiColor.textSecondary)

                Button {
                    showScanner = true
                } label: {
                    Text(AppLanguage.isRussian ? "ВОЙТИ ПО QR С ДРУГОГО УСТРОЙСТВА" : "SIGN IN WITH QR FROM ANOTHER DEVICE")
                        .font(GojiFont.mono(12))
                        .foregroundStyle(GojiColor.textSecondary)
                        .frame(maxWidth: .infinity, minHeight: 44)
                }
                .buttonStyle(.plain)

                consentText
                    .padding(.bottom, 24)
            }
            .padding(.horizontal, GojiMetrics.screenPadding)
        }
        .scrollDismissesKeyboard(.interactively)
        .navigationDestination(isPresented: $showVerify) {
            VerifyCodeView(email: codeSentTo)
        }
        .onAppear { takeTransferEmail() }
        .onChange(of: model.transferEmail) { _ in takeTransferEmail() }
        .sheet(isPresented: $showScanner) {
            QRScannerSheet { code in
                showScanner = false
                if AuthService.isTransferURI(code) {
                    model.pendingTransfer = code
                } else {
                    model.toast = L("transfer.error.invalid")
                }
            }
        }
    }

    /// Почта из QR-кода переноса (AppModel.confirmTransfer): подставить и сразу отправить код.
    private func takeTransferEmail() {
        guard let transfer = model.transferEmail else { return }
        model.transferEmail = nil
        email = transfer
        Task { await send() }
    }

    /// Ссылки на документы до регистрации (аккаунт создаётся при первом входе по коду).
    private var consentText: some View {
        let terms = "[\(L("login.consent.terms"))](\(AppConstants.termsURL.absoluteString))"
        let privacy = "[\(L("login.consent.privacy"))](\(AppConstants.privacyPolicyURL.absoluteString))"
        let markdown = L("login.consent", terms, privacy)
        return Text((try? AttributedString(markdown: markdown)) ?? AttributedString(markdown))
            .font(GojiFont.caption())
            .foregroundStyle(GojiColor.textSecondary)
            .tint(GojiColor.tealDeep)
    }

    private func send() async {
        let trimmed = email.trimmingCharacters(in: .whitespacesAndNewlines)
        guard trimmed.range(of: "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", options: .regularExpression) != nil else {
            error = L("login.error.invalid_email")
            return
        }
        error = nil
        sending = true
        defer { sending = false }
        do {
            try await AuthService.shared.sendCode(email: trimmed)
            emailFocused = false
            codeSentTo = trimmed
            showVerify = true
        } catch {
            self.error = error.localizedDescription
        }
    }
}

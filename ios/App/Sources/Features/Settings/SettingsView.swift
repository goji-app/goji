import SwiftUI

struct SettingsView: View {
    @EnvironmentObject private var model: AppModel
    @EnvironmentObject private var settings: AppSettings
    @EnvironmentObject private var vpn: VPNController
    @Environment(\.openURL) private var openURL
    @State private var confirmLogout = false

    var body: some View {
        ScreenScaffold(title: L("settings.title")) {
            SectionHeader(title: L("settings.connection"))
            card {
                NavigationLink { JournalView() } label: {
                    RowLabel(icon: "chart.bar.xaxis", title: L("journal.title"), subtitle: L("journal.link_desc"))
                }
                divider
                Toggle(isOn: Binding(get: { settings.killSwitch }, set: { value in
                    settings.killSwitch = value
                    Task { await vpn.applySettings(reloadCore: false) }
                })) {
                    RowLabel(icon: "lock.shield", title: L("settings.killswitch"), subtitle: L("settings.killswitch.desc"), showsChevron: false)
                }
                divider
                NavigationLink { AutoConnectView() } label: {
                    RowLabel(icon: "wifi", title: L("settings.autoconnect"), value: settings.autoConnect.title)
                }
                divider
                NavigationLink { BypassDomainsView() } label: {
                    RowLabel(icon: "arrow.triangle.branch", title: L("bypass.title"), subtitle: L("bypass.subtitle"),
                             value: settings.bypassDomains.isEmpty ? nil : "\(settings.bypassDomains.count)")
                }
                divider
                Toggle(isOn: $settings.autoSwitchOnJam) {
                    RowLabel(icon: "antenna.radiowaves.left.and.right", title: L("settings.autoswitch"), subtitle: L("settings.autoswitch.desc"), showsChevron: false)
                }
            }

            SectionHeader(title: L("settings.servers_check"))
            card {
                NavigationLink { PingSettingsView() } label: {
                    RowLabel(icon: "speedometer", title: L("ping.title"), value: settings.pingMethod.title)
                }
                divider
                NavigationLink { LeakCheckView() } label: {
                    RowLabel(icon: "checkmark.shield", title: L("leak.title"), subtitle: L("leak.subtitle"))
                }
            }

            SectionHeader(title: L("settings.appearance"))
            card {
                VStack(alignment: .leading, spacing: 8) {
                    Text(L("settings.theme"))
                        .font(GojiFont.caption())
                        .foregroundStyle(GojiColor.textSecondary)
                    PillSegmented(options: ThemeMode.allCases, selection: $settings.themeMode) { $0.title }
                    Text(AppLanguage.isRussian ? "Палитра" : "Palette")
                        .font(GojiFont.caption())
                        .foregroundStyle(GojiColor.textSecondary)
                        .padding(.top, 6)
                    PaletteGrid(selected: $settings.paletteId)
                }
                divider
                Button {
                    if let url = URL(string: UIApplication.openSettingsURLString) { openURL(url) }
                } label: {
                    RowLabel(icon: "globe", title: L("settings.language"), subtitle: L("settings.language.desc"), value: Locale.current.localizedString(forLanguageCode: AppLanguage.code)?.capitalized)
                }
                .buttonStyle(.plain)
                divider
                Toggle(isOn: $settings.haptics) {
                    RowLabel(icon: "hand.tap", title: L("settings.haptics"), showsChevron: false)
                }
            }

            SectionHeader(title: L("support.title"))
            card {
                NavigationLink { SupportListView() } label: {
                    RowLabel(icon: "bubble.left.and.bubble.right", title: L("support.write"), subtitle: L("support.write.desc"), tint: GojiColor.terracotta)
                }
                divider
                NavigationLink { FaqView() } label: {
                    RowLabel(icon: "questionmark.circle", title: L("faq.title"))
                }
                divider
                NavigationLink { LogsView() } label: {
                    RowLabel(icon: "doc.text.magnifyingglass", title: L("logs.title"), value: settings.logLevel == .none ? L("log.level.none") : nil)
                }
            }

            SectionHeader(title: L("settings.about"))
            card {
                NavigationLink { AboutView() } label: {
                    RowLabel(icon: "info.circle", title: L("about.title"), value: "\(AppConstants.appVersion) (\(AppConstants.buildNumber))")
                }
                divider
                Button { openURL(AppConstants.privacyPolicyURL) } label: {
                    RowLabel(icon: "hand.raised", title: L("legal.privacy"))
                }
                .buttonStyle(.plain)
                divider
                Button { openURL(AppConstants.termsURL) } label: {
                    RowLabel(icon: "doc.text", title: L("legal.terms"))
                }
                .buttonStyle(.plain)
            }

            SectionHeader(title: L("settings.account"))
            card {
                Button { confirmLogout = true } label: {
                    RowLabel(icon: "rectangle.portrait.and.arrow.right", title: L("settings.logout"), tint: GojiColor.danger, showsChevron: false)
                }
                .buttonStyle(.plain)
                divider
                NavigationLink { DeleteAccountView() } label: {
                    RowLabel(icon: "person.crop.circle.badge.xmark", title: L("account.delete.title"), tint: GojiColor.danger)
                }
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .toggleStyle(SwitchToggleStyle(tint: GojiColor.teal))
        .confirmationDialog(L("settings.logout.confirm"), isPresented: $confirmLogout, titleVisibility: .visible) {
            Button(L("settings.logout"), role: .destructive) { Task { await model.logout() } }
            Button(L("common.cancel"), role: .cancel) {}
        }
    }

    private var divider: some View {
        Divider().overlay(GojiColor.hair).padding(.vertical, 6)
    }

    private func card<Content: View>(@ViewBuilder _ content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 4) { content() }
            .buttonStyle(.plain)
            .padding(14)
            .background(GlassSurface())
    }
}

/// Простой экран-подложка для подэкранов настроек: фон + заголовок + содержимое.
struct SubScreen<Content: View>: View {
    let title: String
    @ViewBuilder var content: Content

    var body: some View {
        ZStack {
            GlassBackdrop()
            ScrollView {
                VStack(alignment: .leading, spacing: 12) { content }
                    .padding(.horizontal, GojiMetrics.screenPadding)
                    .padding(.bottom, 24)
            }
            .scrollIndicators(.hidden)
        }
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
        .toolbarBackground(.hidden, for: .navigationBar)
    }
}


/// Сетка образцов палитр: фон · акцент · тёплый акцент; выбранная — рамка.
struct PaletteGrid: View {
    @Binding var selected: String
    @Environment(\.colorScheme) private var scheme

    var body: some View {
        LazyVGrid(columns: [GridItem(.adaptive(minimum: 72), spacing: 8)], alignment: .leading, spacing: 8) {
            ForEach(Palettes.all) { palette in
                let tones = Palettes.tones(palette, dark: scheme == .dark)
                Button {
                    selected = palette.id
                    Haptics.light()
                } label: {
                    VStack(spacing: 4) {
                        HStack(spacing: 0) {
                            Rectangle().fill(Color(UIColor(argb: tones.surface)))
                            Rectangle().fill(Color(UIColor(argb: tones.primary)))
                            Rectangle().fill(Color(UIColor(argb: tones.tertiary)))
                        }
                        .frame(height: 38)
                        .padding(3)
                        .overlay(Rectangle().stroke(palette.id == selected ? GojiColor.textPrimary : GojiColor.outlineVariant, lineWidth: palette.id == selected ? 2 : 1))
                        Text(palette.name)
                            .font(GojiFont.manrope(11, .regular, relativeTo: .caption))
                            .foregroundStyle(GojiColor.textSecondary)
                            .lineLimit(1)
                    }
                }
                .buttonStyle(.plain)
                .accessibilityLabel(palette.name)
                .accessibilityAddTraits(palette.id == selected ? .isSelected : [])
            }
        }
    }
}

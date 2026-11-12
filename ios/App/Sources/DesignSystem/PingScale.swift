import SwiftUI

/// Шкала пинга макета 9a: три равных сегмента (<500 · <1000 · ≥1000 мс) с зазором и метка цвета ink.
/// Цвет не единственный носитель смысла — число пинга всегда рядом.
struct PingScale: View {
    let ms: Int?
    var height: CGFloat = 6
    var marker: Color = GojiColor.textPrimary

    private static let good = Color(UIColor(argb: 0xFF6E8B79))
    private static let ok = Color(UIColor(argb: 0xFFB39A62))
    private static let bad = Color(UIColor(argb: 0xFFA5655E))

    var body: some View {
        GeometryReader { geo in
            let seg = max(0, (geo.size.width - 4) / 3)
            ZStack(alignment: .leading) {
                HStack(spacing: 2) {
                    Rectangle().fill(Self.good).frame(width: seg, height: height)
                    Rectangle().fill(Self.ok).frame(width: seg, height: height)
                    Rectangle().fill(Self.bad).frame(width: seg, height: height)
                }
                .frame(maxHeight: .infinity)
                if let ms, ms >= 0 {
                    Rectangle()
                        .fill(marker)
                        .frame(width: 3, height: height + 8)
                        .offset(x: min(0.97, Double(ms) / 1000 * 0.97) * geo.size.width)
                }
            }
        }
        .frame(height: height + 8)
        .accessibilityHidden(true)
    }
}

extension VPNNode {
    /// Код для крупной подписи: ISO у узла со страной, иначе заглавный суффикс названия («EU», «LTE»).
    var codeLabel: String {
        if let code = geo?.code { return code }
        let tail = displayName.split(separator: " ").last.map(String.init) ?? ""
        let isCode = (2...3).contains(tail.count) && tail == tail.uppercased() && tail.allSatisfy { $0.isLetter }
        return isCode ? tail : "—"
    }
}

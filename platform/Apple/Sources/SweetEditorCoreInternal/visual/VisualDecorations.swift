import Foundation

struct Cursor: Codable {
    let text_position: TextPositionData
    let position: PointData
    let height: Float
    let visible: Bool
    let show_dragger: Bool
}

struct SelectionRect: Codable {
    let origin: PointData
    let width: Float
    let height: Float
}

struct SelectionHandle: Codable {
    let position: PointData
    let height: Float
    let visible: Bool
}

struct CompositionDecoration: Codable {
    let active: Bool
    let origin: PointData
    let width: Float
    let height: Float
}

struct DiagnosticDecoration: Codable {
    let origin: PointData
    let width: Float
    let height: Float
    let severity: Int32
    let color: Int32
}

struct LinkedEditingRect: Codable {
    let origin: PointData
    let width: Float
    let height: Float
    let is_active: Bool
}

struct BracketHighlightRect: Codable {
    let origin: PointData
    let width: Float
    let height: Float
}

struct ScrollbarRect: Codable {
    let origin: PointData
    let width: Float
    let height: Float
}

struct ScrollbarModel: Codable {
    let visible: Bool
    let alpha: Float
    let thumb_active: Bool
    let track: ScrollbarRect
    let thumb: ScrollbarRect
}

enum GuideDirection: String, Codable {
    case HORIZONTAL
    case VERTICAL
}

enum GuideType: String, Codable {
    case INDENT
    case BRACKET
    case FLOW
    case SEPARATOR
}

enum GuideStyle: String, Codable {
    case SOLID
    case DASHED
    case DOUBLE
}

struct GuideSegment: Codable {
    let direction: GuideDirection
    let type: GuideType
    let style: GuideStyle
    let start: PointData
    let end: PointData
    let arrow_end: Bool
}

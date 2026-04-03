// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SweetEditorApple",
    platforms: [
        .iOS(.v13),
        .macOS(.v11),
    ],
    products: [
        .library(name: "SweetEditoriOS", targets: ["SweetEditoriOS"]),
        .library(name: "SweetEditorMacOS", targets: ["SweetEditorMacOS"]),
    ],
    targets: [
        .binaryTarget(
            name: "SweetEditorCoreIOS",
            path: "binaries/SweetEditorCoreIOS.xcframework"
        ),
        .binaryTarget(
            name: "SweetEditorCoreOSX",
            path: "binaries/SweetEditorCoreOSX.xcframework"
        ),
        .target(
            name: "SweetEditorBridge",
            publicHeadersPath: "include"
        ),
        .target(
            name: "SweetEditorCoreInternal",
            dependencies: ["SweetEditorBridge"]
        ),
        .target(
            name: "SweetEditoriOS",
            dependencies: ["SweetEditorCoreInternal", "SweetEditorCoreIOS"],
            swiftSettings: [
                .unsafeFlags(["-Xfrontend", "-disable-access-control"]),
            ]
        ),
        .target(
            name: "SweetEditorMacOS",
            dependencies: ["SweetEditorCoreInternal", "SweetEditorCoreOSX"],
            swiftSettings: [
                .unsafeFlags(["-Xfrontend", "-disable-access-control"]),
            ]
        ),
        .testTarget(
            name: "SweetEditoriOSTests",
            dependencies: ["SweetEditoriOS"],
            path: "Tests/SweetEditoriOSTests"
        ),
        .testTarget(
            name: "SweetEditorMacOSTests",
            dependencies: ["SweetEditorMacOS"],
            path: "Tests/SweetEditorMacOSTests"
        ),
    ],
    swiftLanguageVersions: [.v5]
)

#!/bin/bash
# Arguments:
# -o, --output          Output directory
# -s, --src             SweetEditor project source directory
# -p, --platform        Target platform (all/android/windows/ohos/wasm; all means build everything)
# --android-ndk         Android NDK path
# --ohos-toolchain      OHOS toolchain CMake file path

# Parse arguments
PROJECT_DIR="../"
OUTPUT_DIR="../build"
ANDROID_NDK="${ANDROID_NDK:-}"
OHOS_TOOLCHAIN="${OHOS_TOOLCHAIN:-}"
PLATFORM="all"
POSITIONAL_ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    -o|--output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    -s|--src)
      PROJECT_DIR="$2"
      shift 2
      ;;
    -p|--platform)
      PLATFORM="$2"
      shift 2
      ;;
    --android-ndk)
      ANDROID_NDK="$2"
      shift 2
      ;;
    --ohos-toolchain)
      OHOS_TOOLCHAIN="$2"
      shift 2
      ;;
    -*|--*)
      echo "Unknown option: $1"
      exit 1
      ;;
    *)
      POSITIONAL_ARGS+=("$1")
      shift
      ;;
  esac
done
set -- "${POSITIONAL_ARGS[@]}"

echo "============================= Start building: $PLATFORM ============================="

function resolve_android_strip_tool() {
  local host_tags=("windows-x86_64" "linux-x86_64" "darwin-x86_64" "darwin-arm64")
  local tag
  for tag in "${host_tags[@]}"; do
    local bin_dir="$ANDROID_NDK/toolchains/llvm/prebuilt/$tag/bin"
    if [ -x "$bin_dir/llvm-strip" ]; then
      echo "$bin_dir/llvm-strip"
      return 0
    fi
    if [ -x "$bin_dir/llvm-strip.exe" ]; then
      echo "$bin_dir/llvm-strip.exe"
      return 0
    fi
  done
  return 1
}

function strip_android_outputs() {
  local target_dir="$1"
  local strip_tool="$2"
  [ -d "$target_dir" ] || return 0
  [ -n "$strip_tool" ] || return 0
  while IFS= read -r -d '' so_file; do
    "$strip_tool" --strip-unneeded "$so_file"
  done < <(find "$target_dir" -type f -name "*.so" -print0)
}

function build_windows_msvc() {
  echo "============================= Windows X64 ============================="
  WINDOWS_OUTPUT=$OUTPUT_DIR/windows
  cmake $PROJECT_DIR \
    -B $WINDOWS_OUTPUT \
    -G "Visual Studio 17 2022" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_CXX_STANDARD=17 \
    -DCMAKE_CXX_STANDARD_REQUIRED=ON \
    -DCMAKE_CXX_FLAGS="/std:c++17 /EHsc /utf-8" \
    -DCMAKE_INSTALL_PREFIX=$WINDOWS_OUTPUT \
    -DBUILD_STATIC_LIB=OFF \
    -DBUILD_TESTING=OFF
  cmake --build $WINDOWS_OUTPUT -j 24 --config Release
  cmake --install $WINDOWS_OUTPUT
}

function build_osx() {
  OSX_ARCH=$1
  echo "============================= MacOSX $OSX_ARCH ============================="
  OSX_OUTPUT=$OUTPUT_DIR/osx/$OSX_ARCH
  cmake $PROJECT_DIR \
    -B $OSX_OUTPUT \
    -G "Ninja" \
    -DCMAKE_CXX_FLAGS="-std=c++17" \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_OSX_ARCHITECTURES="$OSX_ARCH"
  cmake --build $OSX_OUTPUT -j 12
  cmake --install $OSX_OUTPUT
}

function build_linux() {
    LINUX_ARCH=$1
    LINUX_OUTPUT=$OUTPUT_DIR/linux/$LINUX_ARCH
    cmake $PROJECT_DIR \
      -B $LINUX_OUTPUT \
      -G "Ninja" \
      -DCMAKE_CXX_FLAGS="-std=c++17 -fPIC" \
      -DCMAKE_BUILD_TYPE=Release
    cmake --build $LINUX_OUTPUT -j 12
    cmake --install $LINUX_OUTPUT
}

function build_emscripten() {
  echo "============================= WebAssembly ============================="
  WASM_OUTPUT=$OUTPUT_DIR/emscripten
  emcmake.bat cmake $PROJECT_DIR\
    -B $WASM_OUTPUT \
    -G "Ninja" \
    -DCMAKE_CXX_FLAGS="-std=c++17" \
    -DCMAKE_BUILD_TYPE=Release
  cmake --build $WASM_OUTPUT -j 24
  cmake --install $WASM_OUTPUT
}

function build_android() {
  ANDROID_ARCH=$1
  if [ -z "$ANDROID_NDK" ]; then
    echo "ANDROID_NDK is not set. Use --android-ndk or export ANDROID_NDK."
    exit 1
  fi
  echo "============================= Android $ANDROID_ARCH ============================="
  echo "============================= NDK: $ANDROID_NDK ============================="
  ANDROID_OUTPUT=$OUTPUT_DIR/android/$ANDROID_ARCH
  cmake $PROJECT_DIR \
    -B $ANDROID_OUTPUT \
    -G "Ninja" \
    -DANDROID_ABI=$ANDROID_ARCH \
    -DCMAKE_ANDROID_ARCH_ABI=$ANDROID_ARCH \
    -DANDROID_NDK=$ANDROID_NDK \
    -DCMAKE_ANDROID_NDK=$ANDROID_NDK \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=$ANDROID_NDK/build/cmake/android.toolchain.cmake \
    -DANDROID_PLATFORM=android-21 \
    -DCMAKE_CXX_FLAGS="-std=c++17"
  cmake --build $ANDROID_OUTPUT -j 24
  cmake --install $ANDROID_OUTPUT

  local strip_tool
  strip_tool=$(resolve_android_strip_tool)
  if [ -n "$strip_tool" ]; then
    echo "============================= Stripping Android .so ($ANDROID_ARCH) ============================="
    echo "============================= Strip Tool: $strip_tool ============================="
    strip_android_outputs "$ANDROID_OUTPUT" "$strip_tool"
  else
    echo "Warning: llvm-strip not found under ANDROID_NDK=$ANDROID_NDK, skip stripping."
  fi
}

function build_ohos() {
  OHOS_ARCH=$1
  if [ -z "$OHOS_TOOLCHAIN" ]; then
    echo "OHOS_TOOLCHAIN is not set. Use --ohos-toolchain or export OHOS_TOOLCHAIN."
    exit 1
  fi
  echo "============================= OHOS $OHOS_ARCH ============================="
  echo "============================= Toolchain: $OHOS_TOOLCHAIN ============================="
  OHOS_OUTPUT=$OUTPUT_DIR/ohos/OHOS_ARCH
  cmake $PROJECT_DIR \
    -B $OHOS_OUTPUT \
    -G "Ninja" \
    -DOHOS_PLATFORM=OHOS \
    -DOHOS_ARCH=$OHOS_ARCH \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_TOOLCHAIN_FILE=$OHOS_TOOLCHAIN \
    -DCMAKE_CXX_FLAGS="-std=c++17"
  cmake --build $OHOS_OUTPUT -j 24
  cmake --install $OHOS_OUTPUT
}

if [ $PLATFORM = "all" ]; then
  build_windows_msvc
  build_linux aarch64
  build_android arm64-v8a
  build_android x86_64
  build_ohos arm64-v8a
  build_ohos x86_64
elif [ $PLATFORM = "wasm" ]; then
  build_emscripten
elif [ $PLATFORM = "windows" ]; then
  build_windows_msvc
elif [ $PLATFORM = "osx" ]; then
  build_osx arm64
  build_osx x86_64
elif [ $PLATFORM = "linux" ]; then
  build_linux aarch64
  build_linux x86_64
elif [ $PLATFORM = "android" ]; then
  build_android arm64-v8a
  build_android x86_64
elif [ $PLATFORM = "ohos" ]; then
  build_ohos arm64-v8a
  build_ohos x86_64
fi

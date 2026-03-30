#!/bin/bash

# Avalonia Demo 编译验证脚本
# 用于在安装了.NET SDK的环境中执行完整的编译验证流程

set -e  # 遇到错误立即退出

echo "========================================="
echo "Avalonia Demo 编译验证流程"
echo "========================================="
echo ""

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# 项目路径
PROJECT_ROOT="$(cd "$(dirname "$0")/../../.." && pwd)"
AVALONIA_DIR="$PROJECT_ROOT/platform/Avalonia"
SOLUTION_FILE="$AVALONIA_DIR/Avalonia.sln"

echo "项目根目录: $PROJECT_ROOT"
echo "Avalonia目录: $AVALONIA_DIR"
echo "解决方案文件: $SOLUTION_FILE"
echo ""

# 检查.NET SDK
echo "步骤1: 检查.NET SDK"
echo "-----------------------------------"
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}错误: 未找到.NET SDK${NC}"
    echo "请从 https://dotnet.microsoft.com/download 安装.NET 9 SDK"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo -e "${GREEN}✓${NC} .NET SDK 版本: $DOTNET_VERSION"

# 检查版本是否符合要求
if [[ ! "$DOTNET_VERSION" =~ ^9\. ]]; then
    echo -e "${YELLOW}警告: .NET SDK版本不是9.x，可能存在兼容性问题${NC}"
fi
echo ""

# 检查项目文件
echo "步骤2: 检查项目文件"
echo "-----------------------------------"
PROJECT_FILES=(
    "$AVALONIA_DIR/SweetEditor/SweetEditor.csproj"
    "$AVALONIA_DIR/Demo/Demo.csproj"
    "$AVALONIA_DIR/Tests/Tests.csproj"
)

for file in "${PROJECT_FILES[@]}"; do
    if [ -f "$file" ]; then
        echo -e "${GREEN}✓${NC} $file"
    else
        echo -e "${RED}✗${NC} $file (未找到)"
        exit 1
    fi
done
echo ""

# 检查原生库
echo "步骤3: 检查原生库"
echo "-----------------------------------"
NATIVE_LIB_DIR="$PROJECT_ROOT/cmake-build-release-visual-studio/bin"
NATIVE_LIBS=(
    "sweeteditor.dll"
    "libsweeteditor.so"
    "libsweeteditor.dylib"
)

if [ -d "$NATIVE_LIB_DIR" ]; then
    for lib in "${NATIVE_LIBS[@]}"; do
        if [ -f "$NATIVE_LIB_DIR/$lib" ]; then
            echo -e "${GREEN}✓${NC} $NATIVE_LIB_DIR/$lib"
        else
            echo -e "${YELLOW}⚠${NC} $NATIVE_LIB_DIR/$lib (未找到，将在编译时警告)"
        fi
    done
else
    echo -e "${YELLOW}⚠${NC} 原生库目录不存在: $NATIVE_LIB_DIR"
    echo "编译时可能会出现警告，但不影响功能验证"
fi
echo ""

# 清理旧的构建产物
echo "步骤4: 清理旧的构建产物"
echo "-----------------------------------"
dotnet clean "$SOLUTION_FILE" -c Release || {
    echo -e "${YELLOW}⚠${NC} 清理失败，可能没有旧的构建产物"
}
echo -e "${GREEN}✓${NC} 清理完成"
echo ""

# 恢复NuGet包
echo "步骤5: 恢复NuGet包"
echo "-----------------------------------"
dotnet restore "$SOLUTION_FILE" || {
    echo -e "${RED}错误: NuGet包恢复失败${NC}"
    exit 1
}
echo -e "${GREEN}✓${NC} NuGet包恢复完成"
echo ""

# 编译解决方案
echo "步骤6: 编译解决方案"
echo "-----------------------------------"
dotnet build "$SOLUTION_FILE" -c Release --no-restore || {
    echo -e "${RED}错误: 编译失败${NC}"
    echo "请检查编译错误信息并修复"
    exit 1
}
echo -e "${GREEN}✓${NC} 编译成功"
echo ""

# 验证构建产物
echo "步骤7: 验证构建产物"
echo "-----------------------------------"
OUTPUT_DIRS=(
    "$AVALONIA_DIR/SweetEditor/bin/Release/net8.0"
    "$AVALONIA_DIR/Demo/bin/Release/net8.0"
    "$AVALONIA_DIR/Tests/bin/Release/net8.0"
)

for dir in "${OUTPUT_DIRS[@]}"; do
    if [ -d "$dir" ]; then
        echo -e "${GREEN}✓${NC} $dir"
        # 列出生成的文件
        find "$dir" -name "*.dll" -o -name "*.exe" -o -name "*.so" -o -name "*.dylib" | head -10
    else
        echo -e "${RED}✗${NC} $dir (未找到)"
        exit 1
    fi
done
echo ""

# 运行单元测试
echo "步骤8: 运行单元测试"
echo "-----------------------------------"
dotnet test "$AVALONIA_DIR/Tests/Tests.csproj" -c Release --no-build --verbosity normal || {
    echo -e "${RED}错误: 单元测试失败${NC}"
    echo "请检查测试失败信息并修复"
    exit 1
}
echo -e "${GREEN}✓${NC} 所有单元测试通过"
echo ""

# 检查Demo应用
echo "步骤9: 检查Demo应用"
echo "-----------------------------------"
DEMO_DIR="$AVALONIA_DIR/Demo/bin/Release/net8.0"
if [ -f "$DEMO_DIR/Demo.exe" ] || [ -f "$DEMO_DIR/Demo" ]; then
    echo -e "${GREEN}✓${NC} Demo应用可执行文件已生成"
    echo "位置: $DEMO_DIR"
else
    echo -e "${RED}✗${NC} Demo应用可执行文件未找到"
    exit 1
fi
echo ""

# 检查NuGet包
echo "步骤10: 检查NuGet包"
echo "-----------------------------------"
PACKAGE_DIR="$AVALONIA_DIR/SweetEditor/bin/Release"
if [ -f "$PACKAGE_DIR/SweetEditor.Avalonia.*.nupkg" ]; then
    echo -e "${GREEN}✓${NC} NuGet包已生成"
    ls -lh "$PACKAGE_DIR"/SweetEditor.Avalonia.*.nupkg
else
    echo -e "${YELLOW}⚠${NC} NuGet包未生成 (需要设置GeneratePackageOnBuild=true)"
fi
echo ""

# 生成验证报告
echo "步骤11: 生成验证报告"
echo "-----------------------------------"
REPORT_FILE="$AVALONIA_DIR/VERIFICATION_REPORT.txt"
cat > "$REPORT_FILE" << EOF
Avalonia Demo 编译验证报告
========================

验证时间: $(date)
.NET SDK版本: $DOTNET_VERSION
项目路径: $AVALONIA_DIR

验证结果:
- 项目结构检查: 通过
- 依赖项配置: 通过
- 代码语法检查: 通过
- 编译状态: 成功
- 单元测试: 通过
- 构建产物: 验证完成

构建产物:
EOF

for dir in "${OUTPUT_DIRS[@]}"; do
    echo "- $dir:" >> "$REPORT_FILE"
    ls -lh "$dir" | grep -E '\.(dll|exe|so|dylib)$' >> "$REPORT_FILE"
    echo "" >> "$REPORT_FILE"
done

echo -e "${GREEN}✓${NC} 验证报告已生成: $REPORT_FILE"
echo ""

# 显示总结
echo "========================================="
echo "编译验证总结"
echo "========================================="
echo -e "${GREEN}✓${NC} 所有检查通过"
echo -e "${GREEN}✓${NC} 编译成功"
echo -e "${GREEN}✓${NC} 单元测试通过"
echo -e "${GREEN}✓${NC} 构建产物验证完成"
echo ""
echo "下一步操作:"
echo "1. 运行Demo应用: cd $AVALONIA_DIR/Demo && dotnet run -c Release"
echo "2. 查看验证报告: cat $REPORT_FILE"
echo "3. 发布NuGet包: dotnet pack $AVALONIA_DIR/SweetEditor/SweetEditor.csproj -c Release"
echo ""
echo "验证完成！"
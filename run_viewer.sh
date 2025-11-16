#!/bin/bash

# UsdzSharpie Viewer 実行スクリプト

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VIEWER_DIR="$SCRIPT_DIR/UsdzSharpie.Viewer"
EXAMPLES_DIR="$SCRIPT_DIR/Examples"

if [ $# -eq 0 ]; then
    echo "使用方法: $0 <USDZファイル名 または パス>"
    echo ""
    echo "利用可能なサンプルファイル:"
    ls -1 "$EXAMPLES_DIR"/*.usdz 2>/dev/null | xargs -n 1 basename
    echo ""
    echo "例:"
    echo "  $0 chair_swan.usdz"
    echo "  $0 /path/to/your/model.usdz"
    exit 1
fi

USDZ_FILE="$1"

# ファイルが存在しない場合、Examplesフォルダ内を探す
if [ ! -f "$USDZ_FILE" ]; then
    if [ -f "$EXAMPLES_DIR/$USDZ_FILE" ]; then
        USDZ_FILE="$EXAMPLES_DIR/$USDZ_FILE"
    else
        echo "エラー: ファイルが見つかりません: $1"
        exit 1
    fi
fi

echo "USDZファイルを読み込み中: $USDZ_FILE"
echo "ビューワーを起動します..."
echo ""

cd "$VIEWER_DIR"
dotnet run -- "$USDZ_FILE"

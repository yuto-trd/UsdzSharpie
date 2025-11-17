# UsdzSharpie.Server

USDZ ファイルをオフスクリーンレンダリングして画像を返す Web サーバーです。

## 概要

このサーバーは ASP.NET Core を使用して構築されており、USDZファイルと視点情報（カメラの位置、ターゲット、FOV）を受け取って、レンダリングされた画像を返します。

### 主な機能

- **オフスクリーンレンダリング**: OSMesa を使用した完全なオフスクリーンOpenGLレンダリング
- **画像エンコード**: SkiaSharp を使用してPNG、JPEG、WebP形式の画像を生成
- **複数視点対応**: 複数のカメラ視点情報を受け取ることができます（現在は最初の視点のみ使用）
- **WebUI**: ブラウザから簡単にテストできるWeb UIを提供
- **ヘッドレス対応**: X11やウィンドウシステムなしで動作可能（OSMesa使用）

## 前提条件

### OSMesa のインストール

このサーバーはOSMesa（Off-Screen Mesa）を使用してヘッドレス環境でOpenGLレンダリングを行います。

#### Ubuntu/Debian

```bash
sudo apt-get update
sudo apt-get install libosmesa6 libosmesa6-dev mesa-common-dev
```

#### CentOS/RHEL

```bash
sudo yum install mesa-libOSMesa mesa-libOSMesa-devel
```

#### Fedora

```bash
sudo dnf install mesa-libOSMesa mesa-libOSMesa-devel
```

#### macOS (Homebrewを使用)

```bash
brew install mesa
```

注: macOSでは、OSMesaライブラリのパスを環境変数で指定する必要がある場合があります:
```bash
export DYLD_LIBRARY_PATH=/usr/local/lib:$DYLD_LIBRARY_PATH
```

### インストールの確認

OSMesaが正しくインストールされているか確認:

```bash
# Linuxの場合
ldconfig -p | grep OSMesa

# macOSの場合
ls /usr/local/lib/libOSMesa*
```

## 使用方法

### サーバーの起動

```bash
cd UsdzSharpie.Server
dotnet run
```

サーバーは `http://localhost:5000` で起動します。

### Web UIからの使用

1. ブラウザで `http://localhost:5000` にアクセス
2. USDZファイルを選択
3. 画像サイズとフォーマットを設定
4. 視点情報（JSON形式）を編集（オプション）
5. 「Render」ボタンをクリック

### APIの使用

#### エンドポイント

```
POST /render
```

#### パラメータ（Form Data）

- `usdzFile` (file, required): USDZファイル
- `viewpointsJson` (string, required): カメラ視点情報のJSON配列
- `width` (int, optional): 画像の幅（デフォルト: 800）
- `height` (int, optional): 画像の高さ（デフォルト: 600）
- `format` (string, optional): 画像フォーマット（png, jpeg, webp）（デフォルト: png）

#### 視点情報のJSON形式

```json
[
  {
    "positionX": 3.0,
    "positionY": 3.0,
    "positionZ": 3.0,
    "targetX": 0.0,
    "targetY": 0.0,
    "targetZ": 0.0,
    "fov": 45.0
  }
]
```

#### cURLでの例

```bash
curl -X POST http://localhost:5000/render \
  -F "usdzFile=@path/to/model.usdz" \
  -F "width=1024" \
  -F "height=768" \
  -F "format=png" \
  -F 'viewpointsJson=[{"positionX":3.0,"positionY":3.0,"positionZ":3.0,"targetX":0.0,"targetY":0.0,"targetZ":0.0,"fov":45.0}]' \
  -o output.png
```

#### Pythonでの例

```python
import requests

url = "http://localhost:5000/render"
files = {
    'usdzFile': open('model.usdz', 'rb')
}
data = {
    'width': 1024,
    'height': 768,
    'format': 'png',
    'viewpointsJson': '[{"positionX":3.0,"positionY":3.0,"positionZ":3.0,"targetX":0.0,"targetY":0.0,"targetZ":0.0,"fov":45.0}]'
}

response = requests.post(url, files=files, data=data)
if response.status_code == 200:
    with open('output.png', 'wb') as f:
        f.write(response.content)
```

## 技術スタック

- **ASP.NET Core 10.0**: Webフレームワーク
- **OpenTK 4.9.4**: OpenGL バインディング
- **OSMesa**: オフスクリーンOpenGLレンダリング
- **SkiaSharp**: 画像エンコード
- **StbImageSharp**: テクスチャ読み込み
- **UsdzSharpie**: USDZファイル読み込み

## アーキテクチャ

### 主要コンポーネント

1. **OpenGLContext**: シングルトンでOSMesaコンテキストを管理
   - OSMesaCreateContextExtでRGBAフォーマット、24ビット深度バッファを作成
   - P/Invokeを使用してOSMesaネイティブライブラリを呼び出し
2. **OSMesa**: OSMesaネイティブライブラリへのP/Invokeバインディング
3. **RendererService**: レンダリングのエントリーポイント、スレッドセーフな実装
4. **OffscreenRenderer**: フレームバッファを使用したオフスクリーンレンダリング
5. **MeshRenderer**: USDZメッシュのOpenGLレンダリング
6. **Shader**: 頂点シェーダーとフラグメントシェーダーの管理
7. **Camera**: カメラの視点とプロジェクション行列の計算

### レンダリングフロー

1. クライアントからUSDZファイルと視点情報を受信
2. 一時ファイルとして保存
3. UsdzReaderでUSDZファイルを解析
4. 各メッシュのレンダラーを作成
5. フレームバッファにレンダリング
6. ピクセルデータを読み取り
7. SkiaSharpで指定フォーマットにエンコード
8. クライアントに返却

## 制限事項

- 現在は最初の視点のみレンダリングします（複数視点対応は今後の実装予定）
- OSMesaはソフトウェアレンダリングのため、GPUアクセラレーションは使用されません
  - 大規模なモデルや高解像度レンダリングは時間がかかる場合があります
- OpenGL 2.1相当の機能に制限される場合があります（OSMesaのバージョンによる）

## トラブルシューティング

### OSMesaライブラリが見つからない

**エラー**: `DllNotFoundException: Unable to load shared library 'OSMesa'`

**解決方法**:
1. OSMesaがインストールされているか確認:
   ```bash
   # Linux
   ldconfig -p | grep OSMesa

   # macOS
   brew list mesa
   ```

2. ライブラリのパスを環境変数で指定:
   ```bash
   # Linux
   export LD_LIBRARY_PATH=/usr/lib/x86_64-linux-gnu:$LD_LIBRARY_PATH

   # macOS
   export DYLD_LIBRARY_PATH=/usr/local/lib:$DYLD_LIBRARY_PATH
   ```

3. Linuxでライブラリ名が異なる場合、シンボリックリンクを作成:
   ```bash
   sudo ln -s /usr/lib/x86_64-linux-gnu/libOSMesa.so.8 /usr/lib/x86_64-linux-gnu/libOSMesa.so
   ```

### OpenGLコンテキストの初期化に失敗する

**エラー**: `Failed to create OSMesa context`

**解決方法**:
- OSMesaの開発パッケージがインストールされているか確認
- システムのMesaバージョンを確認（Mesa 18.0以降を推奨）:
  ```bash
  glxinfo | grep "OpenGL version"
  ```

### レンダリングが真っ黒になる

**原因**:
- シェーダーのコンパイルエラー
- テクスチャの読み込みエラー
- ライティングの問題

**解決方法**:
1. サーバーのログを確認
2. より単純なUSDZファイルでテスト
3. 環境変数`MESA_DEBUG=1`を設定してデバッグ情報を有効化

### パフォーマンスが遅い

**原因**:
OSMesaはソフトウェアレンダリングのため、GPUアクセラレーションよりも遅くなります。

**解決方法**:
- 画像サイズを小さくする（例: 800x600 → 512x512）
- 複雑なモデルを簡略化
- CPUコア数の多いサーバーを使用
- 複数リクエストを並列処理する場合、適切なワーカー数を設定

### テクスチャが正しく表示されない

USDZファイル内のテクスチャパスが正しいか確認してください。

## ライセンス

このプロジェクトはUsdzSharpieプロジェクトの一部です。

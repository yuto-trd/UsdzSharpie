# UsdzSharpie - USDZ ファイルビューワー

UsdzSharpieは、.NETでUSDZ (Universal Scene Description Zip) ファイルを読み込み、OpenGLでレンダリングするライブラリです。

## 機能

- USDZ/USDCファイルの完全なパース
- ジオメトリデータ(頂点、法線、UV座標)の抽出
- トランスフォーム(位置、回転、スケール)の抽出
- マテリアルとシェーダー情報の抽出
- OpenTK (OpenGL 3.3+) を使用した3Dレンダリング
- インタラクティブなカメラコントロール

## 構成

### UsdzSharpie (コアライブラリ)
- **UsdcReader**: USDCバイナリフォーマットのパーサー
- **UsdzReader**: USDZアーカイブの読み込み
- **UsdcScene**: シーングラフとノード階層
- **UsdcMesh**: メッシュデータ(頂点、法線、インデックス)
- **UsdcMaterial**: マテリアルとシェーダー情報
- **UsdcTransform**: トランスフォーム行列とTRS (Translation, Rotation, Scale)

### UsdzSharpie.Viewer (OpenGLビューワー)
- OpenTKを使用したインタラクティブな3Dビューワー
- Phongシェーディングモデル
- マウスによるカメラ回転とズーム

## ビルド方法

```bash
# ライブラリのビルド
cd UsdzSharpie
dotnet build

# ビューワーのビルド
cd UsdzSharpie.Viewer
dotnet build
```

## 使用方法

### ビューワーの実行

```bash
cd UsdzSharpie.Viewer
dotnet run -- <USDZファイルのパス>

# 例:
dotnet run -- ../Examples/chair_swan.usdz
```

### コントロール
- **左クリック + ドラッグ**: カメラ回転
- **マウスホイール**: ズームイン/アウト
- **ESC**: 終了

### ライブラリの使用例

```csharp
using UsdzSharpie;

// USDZファイルを読み込む
var reader = new UsdzReader();
reader.Read("model.usdz");

// シーンを取得
var scene = reader.GetScene();

// メッシュを取得
foreach (var meshNode in scene.GetMeshNodes())
{
    var mesh = meshNode.Mesh;
    Console.WriteLine($"Mesh: {mesh.Name}");
    Console.WriteLine($"  Vertices: {mesh.Vertices.Length}");
    Console.WriteLine($"  Triangles: {mesh.FaceVertexIndices.Length}");

    // ワールド変換行列を取得
    var worldTransform = meshNode.GetWorldTransform();
}

// マテリアルを取得
foreach (var material in scene.Materials.Values)
{
    Console.WriteLine($"Material: {material.Name}");
    Console.WriteLine($"  Diffuse Color: {material.DiffuseColor}");
}
```

## 実装された機能

### USDCパーサー
- ✅ バイナリヘッダーとバージョンの読み込み
- ✅ TOKENS、STRINGS、FIELDS、FIELDSETS、PATHS、SPECSセクションのパース
- ✅ LZ4圧縮データの展開
- ✅ 変数長整数デコーディング
- ✅ シーン階層の再構築

### データ型サポート
- ✅ プリミティブ型: Bool, Int, UInt, Int64, UInt64, Float, Double, Half
- ✅ ベクトル型: Vec2/3/4 (i/f/d/h)
- ✅ 行列型: Matrix2d, Matrix3d, Matrix4d
- ✅ クォータニオン: Quatd, Quatf, Quath
- ✅ 配列のサポート
- ⚠️ Dictionary, PathListOp (基本実装のみ)

### シーン再構築
- ✅ ノード階層のトラバース
- ✅ メッシュデータの抽出(頂点、法線、UV、インデックス)
- ✅ トランスフォームデータの抽出(行列、TRS)
- ✅ マテリアルとシェーダー情報の抽出
- ✅ 法線の自動計算(法線が存在しない場合)

### OpenGLレンダリング
- ✅ Vertex Buffer Object (VBO) / Element Buffer Object (EBO)
- ✅ Vertex Array Object (VAO)
- ✅ 頂点シェーダー / フラグメントシェーダー
- ✅ Phongシェーディング(アンビエント、ディフューズ、スペキュラー)
- ✅ カメラシステム(パースペクティブ投影)
- ✅ インタラクティブカメラコントロール

## 技術仕様

### サポートフレームワーク
- .NET Standard 2.0 (ライブラリ)
- .NET 10.0 (ビューワー)

### 依存関係
- **K4os.Compression.LZ4.Streams** (v1.1.11) - LZ4圧縮解凍
- **OpenTK** (v4.9.4) - OpenGLバインディングとウィンドウ管理
- **NUnit** (テスト用)

### OpenGL バージョン
- OpenGL 3.3 Core Profile以上

## サンプルファイル

Examplesフォルダには20個のUSDZファイルが含まれています:
- BeoSound_2.usdz
- chair_swan.usdz
- crua_hybrid.usdz
- cup_saucer_set.usdz
- ... その他

## 制限事項

現時点での制限:
- テクスチャの読み込みと表示は未実装
- アニメーションのサポートなし
- バリアント(variants)のサポートなし
- スケルタルアニメーション未対応
- USDプレビューサーフェスの完全なサポートは未実装

## ライセンス

このプロジェクトは開発中です。

## 謝辞

- Pixar - USD (Universal Scene Description) フォーマットの開発
- OpenTK チーム - .NET用OpenGLバインディング

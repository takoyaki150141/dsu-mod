# dsu-mod

dynabook Support Utility に独自プラグインを追加する非公式プロジェクトです。

## 構成

- `src/DsuExt/DsuExt.cs` : 追加プラグイン本体（Extensions タブ）
- `src/DsuModPatcher/DsuModPatcher.cs` : コピー版アプリへのパッチ適用ツール
- `inject/DsuBootstrap.c` : ネイティブCLRローダー
- `inject/DsuInjector.cs` : 起動・注入・プラグイン登録を統合したEXE
- `inject/DsuInjectorPayload.cs` : 注入後のプラグイン登録処理
- `tests/PluginSmokeTest.cs` : プラグイン単体テスト
- `build.ps1` / `build-inject.ps1` : ビルドスクリプト

## ビルド

必要なもの:

- Windows x64
- .NET Framework 4.8
- TCC portable (`tools/tcc/tcc.exe`)
- Mono.Cecil (`lib/Mono.Cecil.dll`)

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\build-inject.ps1
```

生成物は `dist/` と `inject/` に出力されます。このリポジトリには実行ファイルを含めていません。

## 使い方

1. `DsuInjector.exe` / `DsuBootstrap.dll` / `DsuExt.dll` を同じフォルダに置く
2. `DsuInjector.exe` を実行
3. ストア版が未起動なら起動し、起動中プロセスへプラグインを注入
4. ストア版を再起動した場合は、もう一度 `DsuInjector.exe` を実行

## 注意

- 本プロジェクトは Dynabook Inc. および本家 dynabook Support Utility とは無関係の非公式なものです。
- 自己責任で使用してください。動作保証はありません。
- Dynabook 製のバイナリやソースは含んでいません。
- アプリの EULA / 利用規約、著作権、商標、各国の法令を確認してください。
- 公開配布する場合は、権利者からの削除要請や規約違反のリスクがあります。

## ライセンス

このリポジトリのオリジナルコードは MIT License です。詳細は `LICENSE` を参照してください。

MIT License は本リポジトリのコードにのみ適用されます。Dynabook Inc. のソフトウェア、バイナリ、商標、ドキュメントに関する権利は含みません。

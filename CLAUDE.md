# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

After Effectsの「監視フォルダー（Watch Folder）」による分散レンダリングを視覚的に管理・整理するWindowsデスクトップアプリ。
AEが生成する非公開仕様のテキストファイル（`_RCF.txt`・HTMLレポート）を定期スキャンし、ステータスをリアルタイムにUIへ反映する。

## ビルドとリリース

```bash
# 開発ビルド
dotnet build AEWatchRenderManager/AEWatchRenderManager.csproj

# リリース（単一EXE出力） ← 通常はこちらを使う
dotnet publish AEWatchRenderManager/AEWatchRenderManager.csproj -p:PublishProfile=win-x64
# 出力先: AEWatchRenderManager/bin/Release/net8.0-windows/win-x64/publish/
```

パブリッシュプロファイル: `AEWatchRenderManager/Properties/PublishProfiles/win-x64.pubxml`

## アーキテクチャ

**MVVM パターン（CommunityToolkit.Mvvm 8.4）**

```
View (XAML)
  └─ MainWindow.xaml / Views/ScanCycleDialog.xaml
        ↕ DataBinding
ViewModel
  └─ ViewModels/MainViewModel.cs   ← [RelayCommand] / [ObservableProperty]
        ↓ 呼び出し
Services
  ├─ TaskPairManager.cs            ← *_RCF.txt の検出・追加・削除管理
  ├─ StatusAnalyzer.cs             ← RCF/レポートファイルのパースとステータス判定
  └─ SettingsService.cs            ← 設定の永続化（JSON）
        ↓ 扱うデータ
Models
  └─ Models/RenderTaskPair.cs      ← ObservableObject。1ジョブ = 1インスタンス
```

### 各クラスの責務

| クラス | 責務 |
|---|---|
| `MainViewModel` | UI状態・コマンド・スキャンタイマーの管理。ビジネスロジックの起点 |
| `TaskPairManager` | サブフォルダをスキャンして `RenderTaskPair` の `ObservableCollection` を同期 |
| `StatusAnalyzer` | `_RCF.txt` と HTML レポートを非同期パースし `RenderStatus` を決定 |
| `SettingsService` | `%LocalAppData%\AEWatchRenderManager\config.json` に設定を永続化 |
| `RenderTaskPair` | 1ジョブの状態（パス・ステータス・表示色）を保持する Observable モデル |

### RCF ファイル仕様（非公開・解析済み）

- ファイル名形式: `{ProjectName}_RCF.txt`
- `init=` フラグ: ジョブ初期化状態の数値
- `html_name=""` フィールド: HTMLレポートファイル名（空の場合はフォルダ内の `*_レポート.txt` を探す）
- `(Finished` を含む場合: `Completed`
- ファイルが AE によってロックされている可能性があるため `FileShare.ReadWrite` で開くこと

### 設定の永続化

`AppSettings` クラス（`SettingsService.cs`）が JSON で保存：
- `MonitorPath`: 監視フォルダのパス
- `MoveTargetPath`: アーカイブ移動先パス
- `ScanIntervalSeconds`: スキャン間隔（秒）

## Tech Stack & Engine Rules

* **[DO] 言語とフレームワーク**: C# 12 / .NET 8.0 / WPF を使用すること。
* **[DO] MVVMパターンの厳守**: `CommunityToolkit.Mvvm` を全面的に採用すること。
  * `[ObservableProperty]` や `[RelayCommand]` などのSource Generatorを活用し、ボイラープレートを減らすこと。
  * ビジネスロジックを View のコードビハインド（`.xaml.cs`）に記述することは**絶対禁止**。
* **[DO NOT] レガシー技術の混入**: `FolderBrowserDialog` 等のWinForms由来APIは使用しないこと。

## Boundaries & Constraints

* **[DO NOT] ファイルロックによるクラッシュ**: AE本体が `_RCF.txt` をロック中でもクラッシュしないよう、`try...catch (IOException)` を実装すること。
* **[DO NOT] 完全削除**: ジョブ削除は `Directory.Delete` ではなく必ず **Windowsごみ箱への移動** を使うこと。
* **[DO NOT] UIスレッドのフリーズ**: スキャン・パース処理は `async/await` で非同期実行し、UI更新は `Application.Current.Dispatcher` を経由すること。

## Coding Conventions

* クラス・メソッド・プロパティ: `PascalCase`
* ローカル変数・引数: `camelCase`
* private フィールド: `_camelCase`
* インターフェース: `I` プレフィックス
* Nullable Reference Types 有効前提で、null 警告が出ないコードを書くこと。

## Knowledge Documentation（最重要）

AEの非公開仕様やWPF特有の問題に対処した箇所には必ず記述すること：
* `@problem`: 直面した仕様の壁・エラーの原因
* `@solution`: 採用した解決策とその理由

## Language & Communication

すべての出力（回答・コメント・CHANGELOG）は必ず**日本語**で行うこと。

## Workflow & Versioning

作業完了後は以下を必ず実施すること：
1. `CHANGELOG.md` を日本語で更新
2. ソース先頭の日付・バージョンを更新
3. 日時形式: `Wed Dec 03 11:05:00 JST 2025`（曜日 月 日 時:分:秒 JST 年）
   - 日時は必ず以下のコマンドで取得すること（ロケールを英語に固定しないと曜日・月名が日本語になるため）
   ```powershell
   powershell -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture; Get-Date -Format 'ddd MMM dd HH:mm:ss JST yyyy'"
   ```
4. バージョンは変更規模に応じてリビジョン/マイナー/メジャーを適切にアップ

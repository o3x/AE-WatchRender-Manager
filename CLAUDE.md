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

リリースフロー:
1. `CHANGELOG.md` 更新
2. ソース先頭の日付・バージョン更新（**`ShowAbout` 内のバージョン文字列も忘れずに更新**）
3. `dotnet publish`
4. `git commit && git push`
5. `gh release create vX.Y.Z "...publish/AEWatchRenderManager.exe" --title "vX.Y.Z" --notes "..."`

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

| クラス | 責務 |
|---|---|
| `MainViewModel` | UI状態・コマンド・スキャンタイマーの管理。ビジネスロジックの起点 |
| `TaskPairManager` | サブフォルダをスキャンして `RenderTaskPair` の `ObservableCollection` を同期 |
| `StatusAnalyzer` | `_RCF.txt` と HTML レポートを非同期パースし `RenderStatus` を決定 |
| `SettingsService` | `%LocalAppData%\AEWatchRenderManager\config.json` に設定を永続化 |
| `RenderTaskPair` | 1ジョブの状態（パス・ステータス・表示色）を保持する Observable モデル |

### スキャンフロー

`DispatcherTimer.Tick` → `ScanMonitorFolderAsync` → `TaskPairManager.SyncWithDirectoriesAsync`（タスク追加/削除） → `StatusAnalyzer.AnalyzeAsync`（各タスクのステータス更新）

- `_isScanning` フラグで多重実行を防止（UIスレッドで完結するので `bool` で十分）
- `Completed` / `Failed` タスクは `AnalyzeAsync` をスキップする（再スキャン不要な確定状態）
- `TaskPairManager.AddOrUpdateRcfTask` は必ず `Dispatcher.Invoke` 内から呼ばれる前提。メソッド内で追加の `Dispatcher.Invoke` を呼ばないこと（二重 Invoke になる）

## AE 非公開仕様（最重要）

### RCF ファイル仕様

- ファイル名形式: `{ProjectName}_RCF.txt`
- 1行目は `After Effects 13.2v1 Render Control File` **固定**（マジックナンバー）。変更するとAEがRCFと認識しない
- `init=0`: 未初期化（Queued）、`init=1`: 処理開始済み
- `html_name="..."`: HTMLレポートファイル名。空文字の場合はフォルダ内の `*_レポート.txt` を探す
- AE本体がロック中でも読めるよう必ず `FileShare.ReadWrite` で開くこと

### ステータス判定の優先順位（`StatusAnalyzer.AnalyzeAsync`）

以下の順で判定し、マッチした時点で early return する：

1. RCF 本文に `(Finished` → `Completed`
2. RCF 本文に `(Error` → `Failed`
3. RCF 本文に `(Suspended` → `Suspended`
4. RCF 本文に `(Pending` → `Pending`
5. RCF 本文に `(Queued` → `Queued`
6. `init=0` → `Queued`
7. ログファイルなし → `Rendering`
8. ログ本文に完了文字列 → `Completed`、エラー文字列 → `Failed`、それ以外 → `Rendering`

Completed/Failed/Suspended の各ブランチでは、`OutputFolderPath` が未設定なら `TryUpdateOutputPathAsync` を呼んでから return する。

### item*.htm のパス解析

AE が生成する `({ProjectName}_00_Logs)/item*.htm` から出力先フォルダを取得する。ファイルは **Shift-JIS**。

**Format B**（`<A>` タグあり）を先に試み、失敗したら **Format A** にフォールバック：

```
[Format A] <LI> 直後にフルパス1行
  <LI>
  C:\render\コンポ 1\コンポ 1_[####].ext
  → Path.GetDirectoryName → C:\render\コンポ 1

[Format B] <A>タグ内にベースパス、</A>後にサブパス
  <A ...>D:\out</A>
  コンポ 1\コンポ 1_[####].ext
  → Path.Combine(D:\out, Path.GetDirectoryName(サブパス)) → D:\out\コンポ 1
```

**`[compName]` プレースホルダー**: AEの特定バージョンはパス内の `[compName]` をコンポ名で展開せずそのまま書き出す。`<H3>` タグ内の `「コンポ名」`（日本語AE）または `"CompName"`（英語AE）から取得して置換する（`ResolveCompName` メソッド）。`<H3>` 内を先に切り出してから引用符を検索しないと、`<meta http-equiv="Content-Type">` の `"Content-Type"` が誤マッチする。

### AE生成ファイルのエンコーディング

AEが生成するファイルは**すべて Shift-JIS**。`StreamReader` にエンコーディングを指定し忘れると日本語が文字化けして正規表現マッチに失敗する：

```csharp
// 正しい
using var sr = new StreamReader(fs, System.Text.Encoding.GetEncoding("shift-jis"));

// 誤り（デフォルトUTF-8 → 文字化け）
using var sr = new StreamReader(fs);
```

対象: `item*.htm`、`*_レポート.txt`、html_name で指定されたHTMLレポート

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
* **[DO NOT] `async void`**: fire-and-forget が必要な場合は `_ = MethodAsync().ContinueWith(t => Debug.WriteLine(t.Exception), TaskContinuationOptions.OnlyOnFaulted)` パターンを使うこと。

## Coding Conventions

* クラス・メソッド・プロパティ: `PascalCase`
* ローカル変数・引数: `camelCase`
* private フィールド: `_camelCase`
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
2. 変更したソースファイル先頭の日付・バージョンを更新
3. `ShowAbout` 内のバージョン文字列（`MainViewModel.cs`）を更新
4. 日時形式: `Wed Dec 03 11:05:00 JST 2025`（曜日 月 日 時:分:秒 JST 年）
   - 日時は必ず以下のコマンドで取得すること（ロケールを英語に固定しないと曜日・月名が日本語になるため）
   ```powershell
   powershell -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture; Get-Date -Format 'ddd MMM dd HH:mm:ss JST yyyy'"
   ```
5. バージョンは変更規模に応じてリビジョン/マイナー/メジャーを適切にアップ

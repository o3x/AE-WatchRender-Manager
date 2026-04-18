# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

After Effectsの「監視フォルダー（Watch Folder）」による分散レンダリングを視覚的に管理・整理するWindowsデスクトップアプリ。
AEが生成する非公開仕様のテキストファイル（`_RCF.txt`・HTMLレポート）を定期スキャンし、ステータスをリアルタイムにUIへ反映する。
v2.0 からはアプリ自体が aerender ワーカーとして監視フォルダに参加し、キュー済みジョブを自動レンダリングする機能も持つ。

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
2. 変更したソースファイル先頭の日付・バージョン更新（**`ShowAbout` 内のバージョン文字列も忘れずに更新**）
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
  ├─ SettingsService.cs            ← 設定の永続化（JSON）
  ├─ AerenderPathResolver.cs       ← aerender パス解決 / AEP バージョン読み取り（静的）
  └─ WatchFolderParticipant.cs     ← aerender 自動参加ワーカー（バックグラウンドループ）
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
| `AerenderPathResolver` | AEP バイナリヘッダー解析・aerender.exe のパス解決（MainViewModel と WatchFolderParticipant の共用） |
| `WatchFolderParticipant` | キュー済み RCF を検出→排他ロック→aerender 実行→RCF 更新のループ |

### UI スキャンフロー（監視モード）

`DispatcherTimer.Tick` → `ScanMonitorFolderAsync` → `TaskPairManager.SyncWithDirectoriesAsync`（タスク追加/削除） → `StatusAnalyzer.AnalyzeAsync`（各タスクのステータス更新）

- `_isScanning` フラグで多重実行を防止（UIスレッドで完結するので `bool` で十分）
- `Completed` / `Failed` タスクは `AnalyzeAsync` をスキップする（再スキャン不要な確定状態）
- `TaskPairManager.AddOrUpdateRcfTask` は必ず `Dispatcher.Invoke` 内から呼ばれる前提。メソッド内で追加の `Dispatcher.Invoke` を呼ばないこと（二重 Invoke になる）

### 参加フロー（WatchFolderParticipant）

`Start()` → `Task.Run(RunLoopAsync)` → `FindQueuedRcf()` でキュー済み RCF 検出 → ロックファイル作成 → RCF に `(Rendering...)` を書き込み → `cmd /C aerender -project` 起動・待機 → RCF に `(Finished...)` or `(Error...)` を書き込み → ロックファイル削除 → 次のジョブへ

- 監視モード（`DispatcherTimer`）と参加モード（`WatchFolderParticipant`）は**独立して動作**。同時起動可能。
- `WatchFolderParticipant` はバックグラウンドスレッドで動き、UI選択状態とは無関係に監視フォルダ全体を対象にする
- `StatusChanged` イベント → `MainViewModel.OnParticipationStatusChanged` → `Dispatcher.Invoke` でステータスバー更新

### aerender の2つの起動モード

| 操作 | 起動方式 | ウィンドウ | 対象 |
|---|---|---|---|
| 右クリック「aerenderで参加」 | `cmd /K` | 完了後も残る | リスト選択アイテム（手動） |
| ツールバー「参加開始」 | `cmd /C` | 完了後に自動で閉じる | 監視フォルダ内のキュー済みジョブ（自動） |

## AE 非公開仕様（最重要）

### RCF ファイル仕様

- ファイル名形式: `{ProjectName}_RCF.txt`
- 1行目は `After Effects 13.2v1 Render Control File` **固定**（マジックナンバー）。変更するとAEがRCFと認識しない
- `init=0`: 未初期化（Queued）、`init=1`: 処理開始済み
- `html_name="..."`: HTMLレポートファイル名。空文字の場合はフォルダ内の `*_レポート.txt` を探す
- AE本体がロック中でも読めるよう必ず `FileShare.ReadWrite` で開くこと

### RCF への書き込み（WatchFolderParticipant が行う）

`WatchFolderParticipant` はジョブをクレームする際に RCF を以下の形式で書き換える：

```
After Effects 13.2v1 Render Control File
max_machines=99
num_machines=1
init=1                                    ← 0→1 に更新
html_init=0
html_name=""
machine0=(Rendering 10:00:00) HOSTNAME (1/1)   ← 追加
```

完了時は `machine0=` 行を `(Finished HH:mm:ss)` または `(Error HH:mm:ss)` に置き換える。
`StatusAnalyzer` はこれらのキーワードをサブストリング検索で判定するため、行フォーマットを厳密に揃える必要はない。

### ロックファイル仕様（複数マシン競合防止）

- パス: `{rcfDir}/{MachineName}_{ProjectName}_RCF.lock`
- 内容: `{MachineName}\r\n{yyyy-MM-dd HH:mm:ss}`
- ロックファイルが存在し、かつ最終更新時刻が **30分以内**であれば別マシンが処理中とみなす
- 30分超のロックはクラッシュ等による残留（スタレロック）とみなし無視する
- クレーム手順: ロックファイル作成 → 200ms 待機 → RCF を再読み込みして依然 Queued か確認（TOCTOU 対策）

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

### AEP バイナリヘッダー解析（`AerenderPathResolver.ReadAepMajorVersion`）

AEselector プロジェクトと同じロジック。48バイト読んでマジックナンバーとバージョンを取得：

- マジックナンバー: `RIFF`/`RIFX`（offset 0）+ `Egg!`（offset 8）
- CS6以降: `b[0x18] == 0x68` で判別、バージョンは offset `0x24`
- CS5以前: バージョンは offset `0x18`
- バージョン計算: `((b[N] << 1) & 0xF8) | ((b[N+1] >> 3) & 0x07)` → AE メジャー番号
- バージョン番号→フォルダ名: v22+ → `Adobe After Effects {2000+v}`、v17-21 → `{2003+v}`、v14-16 → CC `{2003+v}`

### AE生成ファイルのエンコーディング

AEが生成するファイルは**すべて Shift-JIS**。`StreamReader` にエンコーディングを指定し忘れると日本語が文字化けして正規表現マッチに失敗する：

```csharp
// 正しい
using var sr = new StreamReader(fs, System.Text.Encoding.GetEncoding("shift-jis"));
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
* **[DO NOT] バックグラウンドスレッドから直接 ObservableProperty を変更**: `WatchFolderParticipant` のような非UIスレッドから UI バインドプロパティを更新する場合は `Dispatcher.Invoke` を経由すること。

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

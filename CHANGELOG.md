# Changelog

## [2.0.1] - Sat Apr 18 19:06:22 JST 2026

- **修正**: 参加モードの aerender を `cmd /C` で実行するように変更
  - 手動「aerenderで参加」と同様に cmd ウィンドウが開き、aerender の出力がリアルタイムで表示される
  - aerender 完了後はウィンドウが自動で閉じる（`/C` オプション）

## [2.0.0] - Sat Apr 18 19:06:22 JST 2026

- **新機能**: 監視フォルダ参加機能（aerender による自動レンダリングワーカー）を追加
  - ツールバーに「参加開始」「参加停止」ボタンを追加
  - 監視フォルダ内のキュー済み（`init=0`）RCF を自動検出し、aerender でレンダリング
  - AEP バイナリヘッダーから対応 AE バージョンの aerender.exe を自動選択
  - ロックファイル（`{MachineName}_{ProjectName}_RCF.lock`）で複数マシン間の競合を防止
  - 30 分以上古いロックファイルは停止済みマシンのものとして無視（スタレロック対策）
  - aerender の標準出力を `{ProjectName}_{timestamp}_aerender.log` に記録
  - RCF に `(Rendering...)` → `(Finished...)` / `(Error...)` を書き込み、既存の状態表示と連携
  - ステータスバーに参加状態と現在の動作（待機中 / レンダリング中 / 完了）を表示
- **リファクタリング**: aerender パス解決と AEP バージョン読み取りロジックを `AerenderPathResolver` に抽出
  - `MainViewModel` と `WatchFolderParticipant` の両方から共有
- 右クリックメニューの「aerender でレンダリング」を「aerenderで参加」に改名

## [1.21.1] - Sat Apr 18 17:40:06 JST 2026

- 右クリックメニューの「aerender でレンダリング」を最下部に移動

## [1.21.0] - Sat Apr 18 14:08:25 JST 2026

- **改善**: aerender でレンダリング時に AEP バージョンに対応した aerender.exe を自動選択して起動
  - AEP ヘッダーからバージョンを読み取り、対応する AE インストールフォルダの aerender.exe を直接使用
  - バージョン対応フォルダ名の計算は AEselector の ResolveAePath と同ロジック（v22+ → `Adobe After Effects {2000+v}`）
  - 対応バージョンがインストールされていない場合のみ、設定値→最新自動検出にフォールバックしてバージョン不一致を警告

## [1.20.0] - Sat Apr 18 14:02:10 JST 2026

- **新機能**: aerender でレンダリング実行前に AE バージョン互換性チェックを追加
  - AEP バイナリヘッダーから作成バージョン（メジャー番号）を解析（AEselector と同ロジック）
  - `aerender -version` でインストール済みバージョンを取得
  - aerender < AEP の場合、バージョン番号を明示した警告ダイアログを表示し続行可否を確認
- セットアップメニューの「aerender.exe の場所...」を最下部（セパレータ後）に移動

## [1.19.0] - Sat Apr 18 13:55:38 JST 2026

- **新機能**: バージョンチェック機構を追加
  - 起動時にバックグラウンドで GitHub Releases API を確認（サイレント、失敗しても無視）
  - 新バージョンがある場合はステータスバー右端にオレンジで通知表示（クリックでダウンロードページへ）
  - ヘルプメニューに「アップデートを確認...」を追加（手動チェック）

## [1.18.0] - Sat Apr 18 13:50:28 JST 2026

- **新機能**: 選択中のアイテムを aerender でレンダリングする機能を追加
  - 右クリックメニューの最上部に「aerender でレンダリング」を追加
  - 選択アイテムの AEP ファイルを `aerender.exe -project "path.aep"` で実行（cmd.exe ウィンドウで起動）
  - 複数選択時はそれぞれ別ウィンドウで起動
  - aerender.exe を Program Files 内の AE インストールフォルダから自動検出
  - セットアップメニューに「aerender.exe の場所...」を追加
  - **注意**: aerender の `-watchfolder` オプションは非対応のため、個別 AEP レンダリング方式を採用

## [1.17.1] - Sat Apr 18 13:39:29 JST 2026

- `JoinWatchFolder` のバグ修正: `Program Files` 等スペースを含むパスで aerender が起動できなかった問題を修正
  - `cmd /K "path" args` → `cmd /K ""path" args"` に修正（cmd.exe のスペース含みパスのクォート規則に対応）

## [1.17.0] - Sat Apr 18 13:39:29 JST 2026

- **新機能**: aerender で監視フォルダのレンダリングに参加する機能を追加
  - ボタン帯に「aerender で参加」ボタンを追加
  - クリックすると `aerender.exe -watchfolder "監視フォルダ"` を cmd.exe ウィンドウで起動
  - aerender.exe を Program Files 内の AE インストールフォルダから自動検出（複数バージョンがある場合は最新を優先）
  - 自動検出成功時はパスを設定に保存し、次回以降の検索を省略
  - 検出に失敗した場合は手動指定ダイアログを案内
  - セットアップメニューに「aerender.exe の場所...」設定項目を追加

## [1.16.19] - Sat Apr 18 09:28:17 JST 2026

- コードレビュー指摘対応
  - **[B2] バグ修正**: `ParseReportFileAsync` にエンコーディング `shift-jis` を指定
    - AE が生成する `*_レポート.txt` は Shift-JIS だが UTF-8 で読んでいたため日本語プロジェクト名の解析が常に失敗していた
    - `TryUpdateOutputPathAsync` の `item*.htm` 読み込みと同様に `shift-jis` を明示指定
  - **[B1] バグ修正**: バージョン情報ダイアログのバージョン文字列を正しい番号に修正
  - **[B3] 改善**: `TriggerImmediateScan` の `async void` を `ContinueWith` パターンに変更し例外が握り潰されないように修正
  - **[W1] 整理**: `AddOrUpdateRcfTask` 内の二重 `Dispatcher.Invoke` を削除（呼び出し元が既に Dispatcher 内）
  - **[W2] 整理**: `UpdateWindowTitle` の不要な `_scanTimer != null` チェックを削除
  - **[W3] 整理**: `SettingsService` の `JsonSerializerOptions` を `static readonly` フィールドに移動

## [1.16.18] - Sat Apr 18 09:03:59 JST 2026

- `ResolveCompName` の `[compName]` 置換バグを修正
  - `<meta http-equiv="Content-Type" ...>` の `"Content-Type"` が先にマッチして誤った名前で置換されていた
  - `<H3>` タグの内容だけを先に切り出し、その中でコンポ名を探すよう修正（`RegexOptions.Singleline` で複数行 H3 に対応）

## [1.16.17] - Sat Apr 18 08:48:08 JST 2026

- `StatusAnalyzer.TryUpdateOutputPathAsync` で `item*.htm` 内の `[compName]` プレースホルダーを実際のコンポジション名に置換する処理を追加
  - AE の特定バージョンは出力パスの `[compName]` をコンポ名で展開せずそのまま書き出すことがある
  - `<H3>` タグ内の `「コンポ名」`（日本語 AE）または `"CompName"`（英語 AE）からコンポ名を取得し置換
  - `ResolveCompName(path, htmlContent)` として実装し、Format A・Format B の両パス抽出箇所で呼び出し

## [1.16.16] - Thu Apr 16 11:51:21 JST 2026

- `TryUpdateOutputPathAsync` の `Directory.Exists` チェックを削除
  - ネットワークドライブ・別マシン・アンマウント済みパス等、現在アクセスできないパスがすべてスキップされていた根本原因を修正
  - `OutputFolderPath` は「どこに出力されたか」の記録であり、ディレクトリの実在確認は不要
  - フォルダを実際に開く操作（ダブルクリック・右クリックメニュー）側で `Directory.Exists` を確認し、存在しない場合はユーザーに通知する設計に統一

## [1.16.15] - Thu Apr 16 11:32:10 JST 2026

- `StatusAnalyzer.TryUpdateOutputPathAsync` で `item*.htm` の2種類のフォーマットに対応
  - **Format B**（`<A>` タグあり）: `<A>` 内のベースパスと `</A>` 後のサブパスを結合してフォルダを特定
    ```
    <A ...>D:\AAA\_render</A>
    コンポ 1\コンポ 1_[####].[fileextension]
    → D:\AAA\_render\コンポ 1
    ```
  - **Format A**（単純）: `<LI>` 直後の完全パスから `GetDirectoryName` でフォルダを取得（フォールバック）
    ```
    <LI>
    C:\tmp\コンポ 1\コンポ 1_[####].[fileExtension]
    → C:\tmp\コンポ 1
    ```
  - Format B を先に試み、マッチしない場合に Format A へフォールバック

## [1.16.14] - Wed Apr 15 11:49:02 JST 2026

- リスト項目のダブルクリックでパス列に表示されているフォルダを Explorer で開く機能を追加
  - 出力先フォルダが判明している場合はそちらを、未確定の場合はプロジェクトフォルダを開く（`DisplayPath` と同じ優先順位）

## [1.16.13] - Wed Apr 15 11:17:09 JST 2026

- `StatusAnalyzer.TryUpdateOutputPathAsync` のパス抽出を実際の HTML フォーマットに合わせて再修正
  - v1.16.12 で適用した `<A>` タグ方式は誤サンプルに基づくものだったため廃止
  - 実フォーマット: `<LI>` の直後（改行区切り）にフルパスが1行で記述される
    ```
    <LI>
    C:\tmp\コンポ 1\コンポ 1_[####].[fileExtension]
    ```
  - `Path.GetDirectoryName` でファイル名を除去し出力フォルダを確定（例: `C:\tmp\コンポ 1`）
- ログ内容からの完了判定文字列に `レンダリングが完了しました` を追加（`item*.htm` の実文言に対応）

## [1.16.12] - Wed Apr 15 11:02:12 JST 2026

- `StatusAnalyzer.TryUpdateOutputPathAsync` のパス抽出ロジックを実際の HTML フォーマットに合わせて修正
  - 旧実装: `<LI>` 直後にパスが記述されている前提の正規表現 → 実際には該当箇所なし
  - 新実装: `<A>` タグ内のベースパス（例: `D:\_render\abc`）と `</A>` 後のサブパス（例: `def\def_[####].[ext]`）を分離して抽出し `Path.Combine` で結合
  - `Path.GetDirectoryName(subPath)` でファイル名を除去し出力フォルダを確定（例: `D:\_render\abc\def`）

## [1.16.11] - Tue Apr 14 12:24:34 JST 2026

- 監視開始・監視停止ボタンのボタン状態を監視状態に連動させる
  - 監視中は「監視開始」ボタンを無効化（`CanStartMonitoring = !IsMonitoring`）
  - 未監視時は「監視停止」ボタンを無効化（`CanStopMonitoring = IsMonitoring`）
  - `OnIsMonitoringChanged` で両コマンドの `NotifyCanExecuteChanged` を呼び出し即時反映

## [1.16.10] - Tue Apr 14 12:24:34 JST 2026

- セキュリティ強化・コード品質改善（レビュー指摘対応）
  - **S2** `MoveTask` にパストラバーサル検証を追加。`Path.GetFullPath` + `StartsWith(base + DirectorySeparatorChar)` で `MoveTargetPath` 外へのディレクトリ移動を拒否
  - **S3** `StatusAnalyzer.TryUpdateOutputPathAsync` で HTML から抽出したパスに `Path.IsPathFullyQualified` + `Directory.Exists` 検証を追加。相対パスや存在しないディレクトリを拒否
  - **Q1** `catch {}` をすべて `catch (IOException)` / `catch (UnauthorizedAccessException)` / `catch (JsonException)` に限定し、`Debug.WriteLine` で診断ログを出力するよう統一（`StatusAnalyzer`, `SettingsService`, `RenderTaskPair`）
  - **Q2** fire-and-forget `_ = ScanMonitorFolderAsync()` に `.ContinueWith(OnlyOnFaulted)` ハンドラーを追加し、未捕捉例外をデバッグログに記録

## [1.16.9] - Tue Apr 14 12:15:06 JST 2026

- UI をシステム標準に準拠させ、ハードコードカラーを除去
  - ボタン帯の `Background="#F0F0F0"` / `#E3F2FD` を削除 → システムカラー継承
  - ボーダー色を `SystemColors.ControlDarkBrush` に変更
  - リスト行の `Foreground` を文字列バインドから XAML `DataTrigger` に移行。デフォルト（Queued）はシステムカラー継承、Suspended は `SystemColors.GrayTextBrush` を使用
  - `RenderTaskPair.RowForegroundColor` プロパティを削除
- ステータスバーを追加（操作パネルから監視情報を分離）
  - 表示内容: 監視状態（●/○）/ 監視フォルダパス / 最終スキャン時刻 / サイクル秒数
  - `ScanStatusText` を `LastScanText`（時刻のみ）に整理、サイクルはステータスバーに直接バインド
  - 監視パス未設定時は「(未設定)」をグレー表示

## [1.16.8] - Tue Apr 14 12:00:18 JST 2026
- パス列の表示をレンダリング出力先優先に変更
  - `RenderTaskPair.DisplayPath` を追加: `OutputFolderPath` が判明していればそちらを表示し、未判明時は `ProjectFolderPath` にフォールバック
  - `OutputFolderPath` 更新時に `DisplayPath` の変更通知も発火するよう `OnOutputFolderPathChanged` を修正
- パス列に種別表示を追加
  - 出力先未確定（フォールバック表示）時は斜体 + 半透明で視覚的に区別
  - ツールチップに「レンダリング出力先」または「プロジェクトフォルダ（出力先未確定）」とパスを表示
  - `RenderTaskPair.DisplayPathTooltip` プロパティを追加

## [1.16.7] - Tue Apr 14 12:00:18 JST 2026
- 二重起動時の挙動を改善: アラート表示 → 既存ウィンドウをフォアグラウンドにアクティブ化
  - Win32 API `ShowWindow(SW_RESTORE)` + `SetForegroundWindow` で最小化されていても復元して前面に移す

## [1.16.6] - Thu Apr 09 16:00:00 JST 2026
- アイコン画像（PNG）更新に伴い ICO を再生成

## [1.16.5] - Thu Apr 09 15:30:00 JST 2026
- ICO ファイルを Pillow で正規生成し直し、アイコンが正常に表示されるよう修正
  - 手動バイナリ生成の ICO が不正フォーマットだったため、Python Pillow で再生成（256/128/64/48/32/16px）

## [1.16.4] - Thu Apr 09 15:15:00 JST 2026
- アプリケーションアイコンを設定
  - `icon/app.ico` を生成し、EXE アイコンおよびウィンドウタイトルバーアイコンに適用

## [1.16.3] - Thu Apr 09 13:30:00 JST 2026
- レンダリング完了後も「レンダリング先を表示」がグレーアウトし続ける根本原因を修正
  - .NET 8 では `Encoding.GetEncoding("shift-jis")` がデフォルトで `NotSupportedException` をスロー
  - `App.OnStartup` で `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` を登録し、`item*.htm` の Shift-JIS 解析が正常に動作するよう修正

## [1.16.2] - Thu Apr 09 13:00:00 JST 2026
- レンダリング正常完了後も「レンダリング先を表示」がグレーアウトしたままになるバグを修正
  - `StatusAnalyzer.AnalyzeAsync` で `(Finished` 検出後に `return` していたため `TryUpdateOutputPathAsync` が呼ばれていなかった
  - Completed / Failed / Suspended 各ブランチで `OutputFolderPath` が未設定の場合に `TryUpdateOutputPathAsync` を呼ぶよう修正

## [1.16.1] - Thu Apr 09 12:30:00 JST 2026
- 「レンダリング先を表示」が ProjectFolderPath（プロジェクトフォルダ）を開いていたバグを修正
  - item*.htm から解析した OutputFolderPath（C:\tmp 等の実際の出力先）を正しく開くよう変更
- レンダリングが未開始（OutputFolderPath が未確定）の場合、コンテキストメニューの「レンダリング先を表示」をグレーアウト
  - RenderTaskPair に HasOutputPath プロパティを追加し IsEnabled にバインド

## [1.16.0] - Thu Apr 09 12:02:00 JST 2026
- リストのカラムヘッダークリックによるソート機能を追加
  - ステータス・レンダリングアイテム・最終更新・パス の各列でソート可能
  - 同一列を再クリックすると昇順/降順が切り替わる
  - `ICollectionView`（`CollectionViewSource`）を ViewModel に導入し MVVM を維持

## [1.15.4] - Thu Apr 09 12:02:00 JST 2026
- RCF 生成時のバージョン文字列を `After Effects 13.2v1 Render Control File` に戻す（エンバグ修正）
  - 1行目のバージョン文字列は AE がRCFとして認識するためのマジックナンバーであり、省略・変更すると監視フォルダーが RCF を無視してしまうことが判明
  - v1.15.3 での「汎用化」変更が誤りだったため差し戻し

## [1.15.3] - Mon Apr 07 10:00:00 JST 2026
- `FolderMonitorService.cs` を削除（`DispatcherTimer` によるポーリングに完全移行しており未使用だったデッドコード）
- `TaskPairManager.SyncWithDirectoriesAsync` の `Directory.GetFiles` を `Task.Run` に移し、フォルダスキャン I/O が UI スレッドをブロックしないよう修正
- ドロップ時に生成する RCF ファイルのバージョン文字列を `After Effects 13.2v1 Render Control File`（AE CC 2014 固有）から汎用の `After Effects Render Control File` に変更
- 各ソースファイルのヘッダーバージョンをアプリバージョン（1.15.3）に統一

## [1.15.2] - Thu Mar 19 19:30:00 JST 2026
- 「バージョン情報」ダイアログの表示バージョンを最新（1.15.1 → 1.15.2）に修正
- 同ダイアログに著作権表記（Copyright © 2026 OHYAMA Yoshihsia / Apache License 2.0）を追加

## [1.15.1] - Thu Mar 19 18:52:42 JST 2026
- パブリッシュプロファイル `Properties/PublishProfiles/win-x64.pubxml` を追加。`dotnet publish -p:PublishProfile=win-x64` で常に `bin\Release\net8.0-windows\win-x64\publish\` 配下に単一 EXE のみ出力されるよう統一
- `.csproj` に `PublishSingleFile=true` 時の `DebugType=embedded` 設定を追加し、PDB ファイルが別途生成されないよう整理

## [1.15.0] - Thu Mar 19 18:52:42 JST 2026
- スキャンサイクル設定をモーダルダイアログ化。メニューから選ぶと `Views/ScanCycleDialog` が開き、現在の秒数がテキストボックスに表示された状態で任意の秒数を入力・確定できるよう変更（数字以外の入力は弾く）
- スキャン完了時に操作パネル右端へ「最終スキャン: HH:mm:ss　サイクル: XX 秒」を表示する機能を追加。追加タイマーなし・スキャンのタイミングのみ更新するため負荷ゼロ

## [1.14.1] - Sat Mar 14 19:09:07 JST 2026
- プロジェクトに Apache License 2.0 (`LICENSE` ファイル) を追加し、README にライセンス情報と著作権（署名）を記載
- メインウィンドウのソースコードヘッダーにライセンス定型文を追記し、更新日時を反映

## [1.14.0] - Fri Mar 13 12:15:00 JST 2026
- RCFファイル名とAEP名が不一致の場合の検出ロジックを強化。同一フォルダ内にAEPがあれば自動で紐付けるよう改善（`aaaaaaa_1_RCF.txt` 等のケースに対応）
- 監視状態の視覚効果を改善。監視停止時は「未監視: [パス]」とグレーで表示し、監視開始時に「監視中: [パス]」と黒色（強調）に切り替わるインジケーターを実装
- レンダリングログ（`item*.htm`）の自動解析機能を実装。実際に出力されたファイルの保存先フォルダを特定可能に
- 「レンダリング先を表示」コマンドの機能を強化。これまでのプロジェクトフォルダではなく、特定された実際の出力先（`C:\tmp\` 等）を直接開くように変更
- ウィンドウタイトルバーの表記規則を整理し、監視状態とパスを常に正しく表示するよう統一

## [1.13.0] - Fri Mar 13 12:00:00 JST 2026
- UIの設定（監視フォルダ、移動先フォルダ、スキャン間隔）がアプリ終了時に自動保存され、次回起動時に復元される機能を実装（`%LOCALAPPDATA%\AEWatchRenderManager\config.json` に保存）
- メニューバーやボタンが機能しなくなっていた UI バインディングの不具合（`DataContext` の欠落）を修正
- 監視フォルダが未設定・または存在しない状態で「監視開始」を押した際、自動的にフォルダ選択ダイアログを開くようにユーザビリティを向上
- 起動時のタイトルバー表示を正常化し、監視状態に応じて動的に内容が変わるよう改善
- アイテム移動時の確認メッセージを、移動先のパスと対象件数を明記した詳細な内容へ強化

## [1.12.0] - Fri Mar 13 11:45:00 JST 2026
- UIレイアウトを大幅に刷新。監視パスやスキャン設定を「セットアップ/操作」メニューバーへ集約し、メイン画面をリスト表示と主要操作ボタン（開始・停止・即時更新）のみに整理
- GridViewのヘッダー名を「プロジェクトフォルダ」から「レンダリングアイテム」へ変更し、実態に即した名称へ統一
- 新しい便利機能として「すべて選択」「完了済みを選択」コマンドをメニューに追加
- `F5` キーによる「今すぐスキャン」の実行に対応
- `RenderTaskPair` に `IsSelected` プロパティを追加し、ViewModel側からのアイテム選択制御を強化

## [1.11.0] - Fri Mar 13 09:30:00 JST 2026
- ファイルのドロップ、削除、移動といったユーザー操作の直後に、自動更新間隔を待たずに即座にスキャンを実行するよう改善
- 操作直後のスキャンと同時に定期更新タイマーをリセットすることで、不要な連続スキャンの発生を防止し体感レスポンスを向上

## [1.10.0] - Fri Mar 13 09:20:00 JST 2026
- アプリケーションの二重起動防止を実装。二重起動時には警告を表示して終了。
- 定期スキャン処理を非同期化し、さらに多重実行を防止する排他制御 (`_isScanning`) を導入。これによりスキャン動作がより安定化
- 監視間隔（秒数）の設定変更を、監視を停止することなく即座にタイマーへ反映するよう改善

## [1.9.0] - Thu Mar 12 13:45:00 JST 2026
- リスト行を右クリックした際に例外エラーとしてアプリケーションがクラッシュ（終了）する致命的なバインディング競合の不具合を修正
- リストの右クリック（コンテキストメニュー）に、レンダリング状況を即座に確認できる以下の3項目を機能拡張として追加
  - 「レンダリング情報の表示」 (HTMLログファイルを既定のブラウザで展開)
  - 「AEPファイルの表示」 (エクスプローラを開き、コンポジションを持つ.aepファイル本体を選択)
  - 「レンダリング先を表示」 (当該プロジェクトの監視実行先フォルダを開く)

## [1.8.1] - Thu Mar 12 12:40:00 JST 2026
- UIリストのステータスによる色分けを、「行の背景色」から「文字色」へと変更。行を選択した際のハイライト表示との干渉・混乱を解消し、白背景向けに見やすい色（Black, Green, Blue, Red, Gray, DarkOrange）に調整
- 通信先やHDDへの負荷を考慮し、デフォルトのスキャン間隔（定期更新タイミング）を `3秒` から `60秒` に変更

## [1.8.0] - Thu Mar 12 12:35:00 JST 2026
- リスト上のアイテムの視認性を高めるため、タスクのステータス定義を `Queued`(待機中)や `Rendering`(処理中)など6種類に一新
- それぞれのステータスに応じて、UIリストの「行の背景色」が自動的に変化する（白、緑、青、赤、灰、橙）カラーリング機能を実装

## [1.7.1] - Thu Mar 12 12:00:00 JST 2026
- D&Dによるプロジェクト追加時に生成される `_RCF.txt` がAfter Effectsから「無効」と判定される不具合を修正。改行コードをWindows標準の `CRLF (\r\n)` に厳密化し、ヘッダーのバージョン表記 (`13.2v1`) および末尾の空行をAEが本来出力するフォーマットと完全に一致させました

## [1.7.0] - Thu Mar 12 11:10:00 JST 2026
- UIを強化し、リスト上のアイテムを複数選択して一括で「削除」または「移動先フォルダ」へ移動できる機能を追加
- `ListView` 上での `Delete` キー押下による削除ショートカット操作を実装
- コントロールパネルに「移動先フォルダ」の固定設定欄と「参照...」ボタンを追加。以降、移動時に毎回ダイアログが出ずワンクリック（一括）で移動が可能に
- ごみ箱削除や移動を実行する前に、「N件の監視アイテムをごみ箱へ移動しますか？」等の安全なY/N確認ダイアログを表示するよう改善

## [1.6.2] - Thu Mar 12 10:55:00 JST 2026
- 初期スキャン時にUIへのリスト反映が非同期で遅れるため、最初の1サイクル目が「未処理」として表示されてしまう不具合を同期処理への変更により修正
- WPFのListViewにおけるContextMenuのコマンドバインディング不備により、右クリックからの「削除」「別フォルダへ移動」コマンドが発火していなかった深刻な不具合を修正

## [1.6.1] - Thu Mar 12 10:45:00 JST 2026
- `StatusAnalyzer` の完了判定において、HTMLログだけでなく `_RCF.txt` 内部に書き出される `(Finished` などのテキストフラグを最優先で検知・判定する最強のロジックに変更
- UIへのファイルD&D時に生成するダミーの監視設定ファイルを、AEの標準である `*_レポート.txt` の内容（タブや空文字を含む完全なフォーマット）をエミュレートするよう改修
- アプリケーション全体の概要や使い方、仕様をまとめた `README.md` を新規作成

## [1.6.0] - Thu Mar 12 10:25:00 JST 2026
- 監視ロジックをファイル変更検知(FileSystemWatcher)依存から、定周期での「サブフォルダ全スキャン方式」へと完全移行。監視開始前から存在するフォルダも取得可能に
- スキャン間隔（秒）をUIから設定できるように機能追加
- RCFファイル内で `html_name=""` と記述されてHTMLログが出力されない環境向けに、同フォルダ内の `*_レポート.txt` 等を代替読込し、ファイル末尾までの記述からステータス判定とプロジェクト名抽出（「プロジェクト名 : xxx.aep」の正規表現パース）を行う `StatusAnalyzer` の解析強化を実装

## [1.5.0] - Thu Mar 12 10:15:00 JST 2026
- AEプロジェクト（`.aep`）ベースから、レンダリング制御ファイル（`_RCF.txt`）ベースへの監視ロジックに全面改修
- `_RCF.txt`内の `init` フラグと `html_name` に指定されたログファイルの記述を用いたハイブリッドなステータス判定機能を実装
- タスクの「ごみ箱への削除」機能を追加（プロジェクトフォルダ全体を物理削除せずにWindowsのごみ箱へ移動）
- UIへのファイルD&D機能を強化（D&D時に同名のサブフォルダを作成し、コピーしたファイルと監視用 `_RCF.txt` および空の `txt` を自動生成）

## [1.4.2] - Thu Mar 12 08:58:00 JST 2026
- 仕様書関連のファイルを `Specs/` ディレクトリへ移動
- レンダリング制御ファイル(RCF)およびログ(HTML)の非公式仕様メモ (`Specs/RCF_HTML_etc.md`) を追加

## [1.4.1] - Thu Mar 12 08:52:00 JST 2026
- 著作権の観点から仕様書 (`AE_WatchFolder_Specs.md`) をGitHubのリポジトリ管理から除外 (.gitignoreに追加)

## [1.4.0] - Thu Mar 12 08:49:00 JST 2026
- AE公式ドキュメントに基づくネットワークレンダリング仕様書 (`AE_WatchFolder_Specs.md`) を追加

## [1.3.0] - Thu Mar 12 08:39:00 JST 2026
- GitHubリポジトリの作成と初期ソースコードのPush
- C# / WPFプロジェクト向けの `.gitignore` を追加

## [1.2.0] - Wed Mar 11 12:51:00 JST 2026
- Phase 4: ファイル操作と利便性向上
  - ViewModelにタスクの移動・削除処理のコマンドを追加
  - 例外処理（IOException, アクセス権限等へのtry-catch）を追加・強化
  - MainWindowのGridへDrag & Dropイベント追加し、監視フォルダへのファイルコピーに対応
- Phase 5: リファインとテスト完了
  - Release版のビルド確認と単一実行ファイルとしてのパブリッシュ確認

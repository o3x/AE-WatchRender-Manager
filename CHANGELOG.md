# Changelog

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

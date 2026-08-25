# Security policy

## Supported versions

最新版のY-TEC Wi-Fi Capsuleを対象にセキュリティ修正を提供します。
過去版で問題が見つかった場合は、最新版へ更新してください。

## Reporting a vulnerability

公開Issueへ実際のWi-Fi設定名、共有キー、プロファイルXML、`.ywcwifi`、
認証情報、復号可能な秘密情報を書き込まないでください。

GitHubのPrivate vulnerability reportingが有効な場合は、リポジトリの
Securityタブから非公開で報告してください。利用できない場合は、公開Issueに
「非公開で連絡したい」とだけ記載してください。

報告には、影響するバージョン、公式／カスタム／公開開発鍵のビルド種別、
再現条件、想定される影響を含めてください。受領後に内容を確認し、対応方針を
連絡します。

## Encryption boundary

`.ywcwifi`はAES-256-CBCとHMAC-SHA-256で暗号化・認証しますが、Y-TEC公式版の
内蔵鍵は実行ファイル解析への耐性を保証しません。公開ソースの標準ビルドは
公開開発鍵を使用するため、実データ用途ではありません。

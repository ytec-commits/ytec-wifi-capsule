# Code signing policy

## English

Windows release artifacts are built from this public repository by GitHub
Actions on GitHub-hosted Windows runners. Organization members act as
committers and reviewers; organization owners separately approve every signing
request after reviewing the source revision, CI results, and release contents.

The app does not transmit information to any networked system. See the
[privacy policy](PRIVACY.md) for the data handled locally by the app.

Official builds embed a 32-byte compatibility key supplied as an encrypted
GitHub Actions secret. The key preserves compatibility with official
`.ywcwifi` backups from version 1.0.0 onward and is not source code. Its
presence does not select a different source-code path. A standard build from
the public repository uses the documented public development key and displays
a warning; custom-key builds are not compatible with official backups.

This non-public compatibility input is fully disclosed in the SignPath
Foundation application. The project will not describe an artifact as signed by
SignPath Foundation unless that design and the release workflow are accepted.
Until signing is available, releases are explicitly marked unsigned and are
published with SHA-256 checksums.

Free code signing provided by [SignPath.io](https://about.signpath.io/),
certificate by [SignPath Foundation](https://signpath.org/).

## 日本語

Y-TEC Wi-Fi CapsuleのWindows配布物は、GitHub上の公開ソースとビルド手順から
GitHub ActionsのGitHub-hosted Windows runnerで生成します。

Free code signing provided by [SignPath.io](https://about.signpath.io/),
certificate by [SignPath Foundation](https://signpath.org/).

## Team roles

- Committers and reviewers: [ytec-forge-commits organization members](https://github.com/orgs/ytec-forge-commits/people)
- Approvers: [ytec-forge-commits organization owners](https://github.com/orgs/ytec-forge-commits/people?query=role%3Aowner)

外部からのPull Requestは、リポジトリ管理者が内容とCI結果を確認してから
取り込みます。各署名リクエストは、ytec-forge-commits organization ownerが
配布内容と検証結果を確認して承認します。

## Privacy

本アプリは、他のネットワークシステムへ情報を送信しません。詳細は
[プライバシーポリシー](PRIVACY.md)を参照してください。

## Official compatibility key

Y-TEC公式ビルドは、1.0.0以降の公式`.ywcwifi`との互換性を保つため、32バイトの
公式アプリ鍵をビルド時にバイナリリソースとして埋め込みます。この鍵はコードでは
なく、公開リポジトリへ保存しないGitHub Actions Secretです。鍵の有無によって
実行されるソースコードは変わりません。

公開ソースの標準ビルドは公開開発鍵を使用し、カスタムビルドは利用者が用意した
32バイト鍵を使用できます。これらはY-TEC公式バックアップと互換性がありません。

この非公開互換鍵を含むビルドがSignPath Foundationの検証要件を満たすかは、
申請時に構成を開示して審査を受けます。承認を得る前にSignPath Foundation署名済みと
表示しません。

## Release process

1. `main`ブランチのCIでAnyCPU／x86／x64ビルドと合成データ回帰テストを実行します。
2. バージョンタグからGitHub-hosted Windows runner上で、公式アプリ鍵をSecretから
   一時ファイルへ復元し、署名前の実行ファイルとポータブルZIPを再ビルドします。
3. 公式アプリ鍵の一時ファイルを必ず削除し、Workflow Artifactへ含まれないことを
   検査します。
4. SignPathのGitHub連携が利用可能な場合は、署名前ArtifactをSignPathへ提出します。
5. 署名状態、操作マニュアル、ライセンス、NOTICE、SHA-256を検証し、GitHub Releaseと
   Y-TEC Forgeへ掲載します。

SignPath Foundationの採択前または署名サービスを利用できない場合、Releaseは
未署名であることを明記し、SHA-256を掲載します。署名済みと未署名の配布物を
同じ表現で公開しません。

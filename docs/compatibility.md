# Windows・CPU互換性

## 対象

| Windows | 32ビット | 64ビット | 備考 |
| --- | :---: | :---: | --- |
| Windows 7 SP1 | 対象 | 対象 | .NET Framework 4.6.1以降が必要 |
| Windows 8 | 対象 | 対象 | OS自体はサポート終了 |
| Windows 8.1 | 対象 | 対象 | OS自体はサポート終了 |
| Windows 10 | 対象 | 対象 | 32ビット／64ビット |
| Windows 11 | - | 対象 | Microsoftは32ビット版を提供していない |

Windows 7 / 8 / 8.1と.NET Framework 4.6.1はMicrosoftのサポートを終了している。技術的な起動対象と、現在も安全更新を受けられる環境を区別する。

## ビルド

- ターゲット: `.NET Framework 4.6.1`
- 通常配布: `AnyCPU`、`Prefer32Bit=false`
- 固定検証: `x86`、`x64`
- WPF / Native Wi-Fi API

AnyCPU版は32ビットOSで32ビット、64ビットOSで64ビットとして動く。暗号形式はlittle-endianの明示ヘッダーとJSONだけで構成し、ポインター長やP/Invoke構造体を保存しない。

## VM確認結果

2026-07-29にWindows 7 SP1、8、8.1、10の32/64ビットとWindows 11の64ビット、計9台で確認した。

- 対象アーキテクチャの暗号・選択・改ざん耐性テストはすべて24/24成功
- x86／x64双方のUI確認ビルドを起動し、バックアップ／復元画面を画像確認
- 各プロセスからNative Wi-Fi APIへ到達することを確認
- 全VMはNIC無効、合成データ限定

VMではWLAN AutoConfigサービスが停止していたため、Native Wi-Fi確認はWindowsエラー1062を受け取れるところまでとした。実Wi-Fiプロファイルの列挙・登録・接続成功を保証する試験ではない。詳細は[検証記録](testing/validation-report.md)を参照する。

## Native Wi-Fi

WLAN AutoConfigサービスが必要。プロファイル操作にはWi-FiインターフェースGUIDが必要なため、無線LANアダプターが存在しないPCでは一覧／復元を開始できない。

`WLAN_PROFILE_GET_PLAINTEXT_KEY`はWindows 7以降で利用できる。既定ではローカルAdministratorsグループだけが平文キー取得権限を持つため、通常版は管理者権限を要求する。

本アプリは周辺ネットワークスキャン、BSSID、現在位置を取得しない。Windows 11の位置情報制限対象となるスキャン系APIを使用しない。

## Wi-Fi形式

新しいOSの認証方式を古いOSが理解できない場合、バックアップを開くことはできてもWindowsへの登録は失敗する。企業802.1Xのユーザー証明書や外部秘密鍵はWLANProfile XMLだけで完結しないため、移行を保証しない。

## 未対応

- Windows Vista / XP
- Windows on ARM
- Windows Server
- macOS / Linux
- スマートフォン

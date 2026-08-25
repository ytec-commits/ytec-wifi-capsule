# Y-TEC Wi-Fi Capsule

[English](README.en.md)

Windowsに保存されているWi-Fi設定を一覧で確認し、必要な項目だけを暗号化バックアップ／復元するポータブルアプリです。

## 主な機能

- Wi-Fiアダプターごとの保存済みプロファイル一覧
- チェックしたプロファイルだけを1つの`.ywcwifi`へ保存
- バックアップ内容を復号・検証してから、復元項目を再選択
- 同名プロファイルの上書きは既定で無効
- バックアップ時も復元時もWi-Fi XMLを平文ファイルへ書き出さない
- 外部通信、クラウド同期、認証、アクセス解析、自動更新なし
- Windows表示言語に合わせた日本語／英語の自動選択と、起動中の手動切替

起動直後から［バックアップ］と［復元］を操作できます。ランディングページやアカウント登録はありません。

利用手順は[日本語HTMLマニュアル](docs/manual/index.html)、[日本語PDF](docs/manual/Y-TEC%20Wi-Fi%20Capsule%20操作マニュアル.pdf)、[English HTML manual](docs/manual/en/index.html)、[English PDF](docs/manual/en/Y-TEC%20Wi-Fi%20Capsule%20User%20Manual.pdf)を参照してください。

## 対応環境

- Windows 7 SP1 / 8 / 8.1 / 10 / 11
- 32ビットWindows / 64ビットWindows
- .NET Framework 4.6.1以降
- WLAN AutoConfigサービスとNative Wi-Fi API

通常配布はAnyCPU版1本です。32ビットOSでは32ビット、64ビットOSでは64ビットとして動作します。暗号化ファイルはCPUビット数に依存せず、32ビットPCと64ビットPCの間で移動できます。

Windows 7 / 8 / 8.1と.NET Framework 4.6.1はMicrosoftのサポートを終了しています。本アプリの表記は技術的な起動対象であり、OS自体の安全性を保証するものではありません。

## 暗号化と安全境界

Y-TEC Data CapsuleのWi-Fi機能と同系統の方式を、Wi-Fi Capsule専用形式として実装しています。

- AES-256-CBC / PKCS#7
- HMAC-SHA-256
- Encrypt-then-MAC
- バックアップごとのランダムソルトとIV
- 用途別の暗号鍵・認証鍵導出
- HMAC検証成功前は復号しない
- パスワード、鍵ファイル、PC固有DPAPIは使用しない

Y-TEC公式配布版は、1.0.0以降の公式バックアップとの互換性を保つため、公開ソースに含めない公式アプリ鍵をビルド時に埋め込みます。別PCへ移した公式バックアップは、同じ鍵を持つY-TEC公式版から復元できます。

公開ソースをそのままビルドすると、値が公開されている開発鍵を使用し、画面上部へ警告を表示します。このビルドは実データ用途ではありません。独自の32バイト鍵を埋め込むカスタムビルドも作成できますが、Y-TEC公式版や別のカスタム鍵ビルドとはバックアップ互換性がありません。

公式アプリ鍵も実行ファイルには含まれるため、実行ファイルやプロセスメモリを詳しく解析できる相手には再現される可能性があります。紛失媒体から平文を容易に読まれないための保護であり、高度なリバースエンジニアリング耐性、利用者ごとの秘密分離、鍵失効は提供しません。

バックアップ中は`WlanGetProfile`、復元中は`WlanSetProfile`を使用します。Wi-FiプロファイルXMLはメモリ上だけで処理し、平文のXML一時ファイルを作りません。SSIDは選択に必要なため画面へ表示しますが、SSID、Wi-Fiキー、XML、PC名、ユーザー名、完全パスをログへ記録しません。

詳細は[脅威モデル](docs/security/threat-model.md)と[バックアップ形式](docs/backup-format.md)を参照してください。

## 操作

### バックアップ

1. Wi-Fiアダプターを選びます。
2. 必要な保存済みWi-Fi設定だけをチェックします。
3. ［選択した設定をバックアップ］を押します。
4. 新しい`.ywcwifi`ファイル名を指定します。

完成ファイルを上書きしません。同名ファイルがある場合は別名を指定してください。

### 復元

1. ［復元］タブを開きます。
2. `.ywcwifi`を選びます。
3. 復元する設定だけをチェックします。
4. 復元先アダプターを選びます。
5. 必要な場合だけ［同名の保存済みWi-Fi設定も上書きする］を有効にします。
6. ［選択した設定を復元］を押します。

既定では同名設定を変更せず、スキップします。企業802.1X証明書、外部証明書、ハードウェア鍵、移行先OS／無線LANアダプターが非対応の認証方式は復元できない場合があります。

## 管理者権限

Wi-Fiキーを含むプロファイルの取得と、すべてのユーザー向けプロファイル登録のため、通常版は起動時に管理者権限を要求します。管理者権限がない場合、Windowsはキーを暗号化したまま返すことがあり、本アプリは移行不能なバックアップを作らず処理を中止します。

## 開発

```powershell
& "C:\Program Files\dotnet\dotnet.exe" restore .\YtecWifiCapsule.slnx
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -c Release --no-restore
& .\tests\Ytec.WifiCapsule.Tests\bin\Release\net461\Ytec.WifiCapsule.Tests.exe
```

AnyCPU、x86、x64固定ビルドは`Rebuild`を使います。

```powershell
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -t:Rebuild -c Release -p:PlatformTarget=AnyCPU -p:Prefer32Bit=false
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -t:Rebuild -c Release -p:PlatformTarget=x86
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -t:Rebuild -c Release -p:PlatformTarget=x64
```

テストと画面確認では合成プロファイルだけを使います。実Wi-Fi設定を自動テストへ使いません。

### アプリ鍵モード

標準の公開ソースビルドは公開開発鍵を使用します。暗号処理の開発・合成テスト専用であり、実データをバックアップしないでください。

独自のカスタム鍵を使う場合は、リポジトリ外へ32バイト鍵を作成してビルドします。鍵を紛失するとバックアップを復元できません。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\New-CustomApplicationKey.ps1 `
  -OutputPath D:\private\ytec-wifi-capsule-custom-key.bin

& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx `
  -c Release `
  -p:YtecWifiCapsuleCustomKeyFile=D:\private\ytec-wifi-capsule-custom-key.bin
```

公式アプリ鍵はGit、ソースアーカイブ、ログ、Workflow Artifact、配布ZIPへ単独ファイルとして保存しません。

### VM互換性検証

VMラボ用のx86／x64テストと合成UIペイロードを作成します。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\validation\New-VmValidationPayload.ps1
```

対象VMを通常起動し、Guest Additionsの準備完了後に1台ずつ実行します。VM名、NIC無効、資格情報ファイルを検証してから動作し、資格情報の値は表示しません。

```powershell
pwsh -NoProfile -File .\eng\validation\Invoke-WifiCapsuleVmValidation.ps1 `
  -VmName YWB-Win7SP1-x86-Clean
```

Windows 10／11 x64のVMは`-GuestUser YbcTest`を指定します。同じVMラボを別作業と同時に操作しないでください。

### ポータブル配布物

日本語・英語のHTML／PDF操作マニュアルは`docs/manual`で管理します。未署名AnyCPU版のZIPを作成・検証し、既存の配布物は上書きしません。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\New-PortableRelease.ps1 `
  -OfficialKeyFile C:\secure-temp\official-key.bin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\Test-PortableRelease.ps1 `
  -ZipPath .\artifacts\release\Y-TEC-Wi-Fi-Capsule-1.1.0-portable-unsigned.zip
```

正式な配布ZIP作成では、リポジトリ外の公式鍵ファイル指定が必須です。

## 構成

- `src/Ytec.WifiCapsule.Core`: 暗号形式、XML検証、選択バックアップ／復元
- `src/Ytec.WifiCapsule.Windows`: Native Wi-Fi APIとビルド時アプリ鍵読込
- `src/Ytec.WifiCapsule.App`: 日本語／英語WPF UI、OS言語判定、画面内切替
- `tests/Ytec.WifiCapsule.Tests`: 合成データによる暗号・安全性回帰テスト
- `docs`: 設計、互換性、脅威モデル、操作マニュアル
- `eng`: ポータブル配布物の作成・検証

本体の外部NuGet依存はMIT LicenseのNewtonsoft.Jsonだけです。

## コード署名

SignPath Foundationによる無料コード署名を申請します。採択前または署名処理を利用できない配布物は未署名と明記し、SHA-256を掲載します。詳細は[Code signing policy](CODE_SIGNING_POLICY.md)を参照してください。

## ライセンス

Y-TECが著作権を持つソースコード、文書、オリジナルアセットは[Apache License 2.0](LICENSE.txt)で公開します。帰属表示は[NOTICE](NOTICE)、第三者ライブラリは[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt)を参照してください。

Copyright 2026 Y-TEC. Licensed under the Apache License, Version 2.0.

# Contributing to Y-TEC Wi-Fi Capsule

改善提案や不具合報告を歓迎します。

## Issue

不具合では、アプリのバージョン、公式／カスタム／公開開発鍵のビルド種別、
Windowsのバージョンとビット数、再現手順、期待した結果、実際の結果を記載して
ください。スクリーンショットやテストデータへ、実際のSSID、Wi-Fi共有キー、
プロファイルXML、`.ywcwifi`を含めないでください。

## Development

必要な環境はWindowsと.NET SDK 10です。対象は.NET Framework 4.6.1です。

```powershell
& "C:\Program Files\dotnet\dotnet.exe" restore .\YtecWifiCapsule.slnx
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -c Release --no-restore
& .\tests\Ytec.WifiCapsule.Tests\bin\Release\net461\Ytec.WifiCapsule.Tests.exe
```

標準ビルドは公開開発鍵を使用します。実際のWi-Fi設定を開発・テストに使用せず、
合成プロファイルだけを使用してください。

## Pull request

- 変更理由と利用者への影響を説明してください。
- UI変更では、合成データだけを表示したスクリーンショットを添付してください。
- 暗号形式や鍵処理を変更する場合は、旧形式読込、移行、失敗時の復旧、回帰テストを
  含めてください。
- 外部通信や依存関係を追加する場合は、目的、送信情報、ライセンス、無効化方法を
  記載してください。
- 公式アプリ鍵、実Wi-Fiデータ、認証情報をコミットしないでください。

明示しない限り、提出されたContributionにはApache License 2.0が適用されます。

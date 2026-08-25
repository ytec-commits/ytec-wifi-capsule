# Y-TEC Wi-Fi Capsule

[日本語](README.md)

Y-TEC Wi-Fi Capsule is a portable Windows application for selecting saved
Wi-Fi profiles, storing them in one encrypted `.ywcwifi` file, and restoring
only the profiles you choose.

## Features

- Lists saved profiles for each Wi-Fi adapter
- Backs up only checked profiles
- Reopens and validates a backup before restore selection
- Does not overwrite an existing profile unless explicitly enabled
- Processes Wi-Fi profile XML in memory without plaintext temporary files
- No network communication, cloud sync, authentication, analytics, or updater
- Japanese and English UI with Windows-language detection and an in-app switch

The app selects Japanese when the Windows display language is Japanese and
English otherwise. The language can be changed for the current session with
the `English / 日本語` button.

User guides are available as the [English HTML manual](docs/manual/en/index.html),
[English PDF](docs/manual/en/Y-TEC%20Wi-Fi%20Capsule%20User%20Manual.pdf),
[Japanese HTML manual](docs/manual/index.html), and
[Japanese PDF](docs/manual/Y-TEC%20Wi-Fi%20Capsule%20操作マニュアル.pdf).

## Supported systems

- Windows 7 SP1 / 8 / 8.1 / 10 / 11
- 32-bit and 64-bit Windows
- .NET Framework 4.6.1 or later
- WLAN AutoConfig and the Windows Native Wi-Fi API

The standard distribution is one AnyCPU portable build. Windows 7, Windows 8,
Windows 8.1, and .NET Framework 4.6.1 are no longer supported by Microsoft.
Compatibility here describes the technical target and does not make those
operating systems secure.

## Encryption and trust boundary

The container uses AES-256-CBC with PKCS#7 padding and HMAC-SHA-256 in an
Encrypt-then-MAC design. Each backup gets a random salt and IV, and the HMAC is
verified before decryption.

The official Y-TEC build embeds a compatibility key during the build. That key
is not stored in the public source repository, allowing official backups made
by version 1.0.0 and later to remain compatible across PCs.

A standard public-source build uses a public development key and displays a
warning. It is for development with synthetic profiles and must not be used for
real Wi-Fi backups. A custom build can embed its own 32-byte key, but its
backups are not compatible with the official build or builds using another key.

An expert who can analyze the official executable or process memory may still
recover its embedded key. The design protects a lost backup medium from casual
plaintext access; it does not provide strong reverse-engineering resistance,
per-user isolation, or key revocation.

See the [threat model](docs/security/threat-model.md) and
[container format](docs/backup-format.md) for details.

## Build and test

```powershell
& "C:\Program Files\dotnet\dotnet.exe" restore .\YtecWifiCapsule.slnx
& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx -c Release --no-restore
& .\tests\Ytec.WifiCapsule.Tests\bin\Release\net461\Ytec.WifiCapsule.Tests.exe
```

Tests and UI captures use synthetic profiles only. Do not use real Wi-Fi
profiles, SSIDs, passwords, XML, or backup files in tests or public issues.

Japanese and English localization resources are checked for matching keys and
missing references by `eng/Test-Localization.ps1` and CI.

### Custom-key build

Create and retain a 32-byte key outside the repository. Losing it makes its
backups unrecoverable.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\eng\New-CustomApplicationKey.ps1 `
  -OutputPath D:\private\ytec-wifi-capsule-custom-key.bin

& "C:\Program Files\dotnet\dotnet.exe" build .\YtecWifiCapsule.slnx `
  -c Release `
  -p:YtecWifiCapsuleCustomKeyFile=D:\private\ytec-wifi-capsule-custom-key.bin
```

## Privacy

The application does not transfer information to any networked system. See
[PRIVACY.md](PRIVACY.md).

## Code signing policy

The project is applying for free code signing provided by
[SignPath.io](https://about.signpath.io/), certificate by
[SignPath Foundation](https://signpath.org/). Until accepted, releases are
explicitly labeled unsigned and include SHA-256 checksums. See the
[code signing policy](CODE_SIGNING_POLICY.md).

## Contributing and security

- [Contribution guide](CONTRIBUTING.md)
- [Security policy](SECURITY.md)
- [Change log](CHANGELOG.md)
- [Third-party notices](THIRD-PARTY-NOTICES.txt)

## License

Y-TEC-authored source code, documentation, and original assets are licensed
under the [Apache License 2.0](LICENSE.txt). See [NOTICE](NOTICE) for
attribution information.

Copyright 2026 Y-TEC. Licensed under the Apache License, Version 2.0.

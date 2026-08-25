using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using Ytec.WifiCapsule.Core.Models;
using Ytec.WifiCapsule.Core.Services;

namespace Ytec.WifiCapsule.App;

internal sealed class DemoWifiProfileStore : IWifiProfileStore
{
    private static readonly Guid DemoAdapterId =
        new("A86E9B22-DB51-4BCB-B49D-E72A35116601");

    private readonly Dictionary<string, byte[]> _profiles =
        new(StringComparer.Ordinal)
        {
            ["SAMPLE-HOME-5G"] = CreateProfile(
                "SAMPLE-HOME-5G",
                "sample-home-key-2026"),
            ["SAMPLE-OFFICE"] = CreateProfile(
                "SAMPLE-OFFICE",
                "sample-office-key-2026"),
            ["SAMPLE-MOBILE"] = CreateProfile(
                "SAMPLE-MOBILE",
                "sample-mobile-key-2026"),
            ["SAMPLE-GUEST"] = CreateOpenProfile(
                "SAMPLE-GUEST"),
        };

    public IReadOnlyList<WifiAdapterInfo> GetAdapters()
    {
        return new[]
        {
            new WifiAdapterInfo(
                DemoAdapterId,
                "合成 Wi-Fi アダプター",
                "画面確認用・実データなし",
                true),
        };
    }

    public IReadOnlyList<WifiStoredProfile> GetProfiles(
        Guid adapterId)
    {
        EnsureAdapter(adapterId);
        return _profiles.Keys
            .OrderBy(
                name => name,
                StringComparer.CurrentCultureIgnoreCase)
            .Select(
                name => new WifiStoredProfile(
                    name,
                    false,
                    false))
            .ToArray();
    }

    public byte[] ExportProfile(
        Guid adapterId,
        string profileName)
    {
        EnsureAdapter(adapterId);
        if (!_profiles.TryGetValue(profileName, out var xml))
        {
            throw new InvalidOperationException(
                "合成Wi-Fi設定が見つかりません。");
        }

        return (byte[])xml.Clone();
    }

    public void ImportProfile(
        Guid adapterId,
        byte[] profileXml,
        bool overwriteExisting)
    {
        EnsureAdapter(adapterId);
        var metadata =
            WifiProfileXmlValidator.Validate(profileXml);
        if (_profiles.ContainsKey(metadata.Name) &&
            !overwriteExisting)
        {
            throw new InvalidOperationException(
                "同名の合成Wi-Fi設定があります。");
        }

        _profiles[metadata.Name] = (byte[])profileXml.Clone();
    }

    private static void EnsureAdapter(Guid adapterId)
    {
        if (adapterId != DemoAdapterId)
        {
            throw new InvalidOperationException(
                "合成Wi-Fiアダプターが見つかりません。");
        }
    }

    private static byte[] CreateProfile(
        string name,
        string key)
    {
        var escapedName = SecurityElement.Escape(name);
        var escapedKey = SecurityElement.Escape(key);
        return Encoding.UTF8.GetBytes(
            $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{escapedName}</name>
              <SSIDConfig><SSID><name>{escapedName}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>auto</connectionMode>
              <MSM>
                <security>
                  <authEncryption>
                    <authentication>WPA2PSK</authentication>
                    <encryption>AES</encryption>
                    <useOneX>false</useOneX>
                  </authEncryption>
                  <sharedKey>
                    <keyType>passPhrase</keyType>
                    <protected>false</protected>
                    <keyMaterial>{escapedKey}</keyMaterial>
                  </sharedKey>
                </security>
              </MSM>
            </WLANProfile>
            """);
    }

    private static byte[] CreateOpenProfile(string name)
    {
        var escapedName = SecurityElement.Escape(name);
        return Encoding.UTF8.GetBytes(
            $"""
            <?xml version="1.0"?>
            <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
              <name>{escapedName}</name>
              <SSIDConfig><SSID><name>{escapedName}</name></SSID></SSIDConfig>
              <connectionType>ESS</connectionType>
              <connectionMode>manual</connectionMode>
              <MSM>
                <security>
                  <authEncryption>
                    <authentication>open</authentication>
                    <encryption>none</encryption>
                    <useOneX>false</useOneX>
                  </authEncryption>
                </security>
              </MSM>
            </WLANProfile>
            """);
    }
}

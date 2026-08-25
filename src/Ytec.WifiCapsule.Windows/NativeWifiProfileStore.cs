using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Ytec.WifiCapsule.Core.Models;
using Ytec.WifiCapsule.Core.Services;

namespace Ytec.WifiCapsule.Windows;

public sealed class NativeWifiProfileStore : IWifiProfileStore
{
    private const uint ClientVersion = 2;
    private const uint ErrorSuccess = 0;
    private const uint WlanProfileGroupPolicy = 0x00000001;
    private const uint WlanProfileUser = 0x00000002;
    private const uint WlanProfileGetPlaintextKey = 0x00000004;
    private const int ListHeaderSize = sizeof(uint) + sizeof(uint);

    public IReadOnlyList<WifiAdapterInfo> GetAdapters()
    {
        return WithClient(
            handle =>
            {
                IntPtr listPointer = IntPtr.Zero;
                try
                {
                    ThrowIfError(
                        NativeMethods.WlanEnumInterfaces(
                            handle,
                            IntPtr.Zero,
                            out listPointer),
                        "WindowsのWi-Fiアダプターを取得できませんでした。");
                    var count = ReadCount(listPointer);
                    var itemSize =
                        Marshal.SizeOf(typeof(WlanInterfaceInfo));
                    var adapters = new List<WifiAdapterInfo>(
                        checked((int)count));
                    for (var index = 0; index < count; index++)
                    {
                        var itemPointer = IntPtr.Add(
                            listPointer,
                            checked(
                                ListHeaderSize +
                                (int)index * itemSize));
                        var item =
                            (WlanInterfaceInfo)Marshal.PtrToStructure(
                                itemPointer,
                                typeof(WlanInterfaceInfo));
                        var description =
                            string.IsNullOrWhiteSpace(item.Description)
                                ? $"Wi-Fiアダプター {index + 1:N0}"
                                : item.Description.Trim();
                        adapters.Add(
                            new WifiAdapterInfo(
                                item.InterfaceGuid,
                                description,
                                GetStateLabel(item.State),
                                item.State ==
                                    WlanInterfaceState.Connected));
                    }

                    return (IReadOnlyList<WifiAdapterInfo>)adapters
                        .OrderByDescending(adapter => adapter.IsConnected)
                        .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
                finally
                {
                    FreeMemory(listPointer);
                }
            });
    }

    public IReadOnlyList<WifiStoredProfile> GetProfiles(
        Guid adapterId)
    {
        return WithClient(
            handle =>
            {
                IntPtr listPointer = IntPtr.Zero;
                try
                {
                    ThrowIfError(
                        NativeMethods.WlanGetProfileList(
                            handle,
                            ref adapterId,
                            IntPtr.Zero,
                            out listPointer),
                        "Windowsの保存済みWi-Fi設定を取得できませんでした。");
                    var count = ReadCount(listPointer);
                    var itemSize =
                        Marshal.SizeOf(typeof(WlanProfileInfo));
                    var profiles = new List<WifiStoredProfile>(
                        checked((int)count));
                    for (var index = 0; index < count; index++)
                    {
                        var itemPointer = IntPtr.Add(
                            listPointer,
                            checked(
                                ListHeaderSize +
                                (int)index * itemSize));
                        var item =
                            (WlanProfileInfo)Marshal.PtrToStructure(
                                itemPointer,
                                typeof(WlanProfileInfo));
                        if (string.IsNullOrWhiteSpace(item.ProfileName))
                        {
                            continue;
                        }

                        profiles.Add(
                            new WifiStoredProfile(
                                item.ProfileName,
                                (item.Flags &
                                    WlanProfileGroupPolicy) != 0,
                                (item.Flags &
                                    WlanProfileUser) != 0));
                    }

                    return (IReadOnlyList<WifiStoredProfile>)profiles
                        .OrderBy(
                            profile => profile.Name,
                            StringComparer.CurrentCultureIgnoreCase)
                        .ToArray();
                }
                finally
                {
                    FreeMemory(listPointer);
                }
            });
    }

    public byte[] ExportProfile(
        Guid adapterId,
        string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException(
                "Wi-Fiプロファイル名を指定してください。",
                nameof(profileName));
        }

        return WithClient(
            handle =>
            {
                IntPtr xmlPointer = IntPtr.Zero;
                var flags = WlanProfileGetPlaintextKey;
                try
                {
                    ThrowIfError(
                        NativeMethods.WlanGetProfile(
                            handle,
                            ref adapterId,
                            profileName,
                            IntPtr.Zero,
                            out xmlPointer,
                            ref flags,
                            out _),
                        "WindowsからWi-Fi設定を取得できませんでした。");
                    var xml = Marshal.PtrToStringUni(xmlPointer);
                    if (string.IsNullOrWhiteSpace(xml))
                    {
                        throw new InvalidDataException(
                            "Windowsから取得したWi-Fi設定が空です。");
                    }

                    var bytes = new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false,
                        throwOnInvalidBytes: true).GetBytes(xml);
                    WifiProfileXmlValidator.Validate(bytes);
                    return bytes;
                }
                finally
                {
                    FreeMemory(xmlPointer);
                }
            });
    }

    public void ImportProfile(
        Guid adapterId,
        byte[] profileXml,
        bool overwriteExisting)
    {
        if (profileXml is null)
        {
            throw new ArgumentNullException(nameof(profileXml));
        }

        WifiProfileXmlValidator.Validate(profileXml);
        var xml = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(profileXml);
        WithClient(
            handle =>
            {
                var result = NativeMethods.WlanSetProfile(
                    handle,
                    ref adapterId,
                    0,
                    xml,
                    null,
                    overwriteExisting,
                    IntPtr.Zero,
                    out _);
                ThrowIfError(
                    result,
                    "WindowsへWi-Fi設定を登録できませんでした。");
                return true;
            });
    }

    private static T WithClient<T>(
        Func<IntPtr, T> operation)
    {
        IntPtr handle = IntPtr.Zero;
        try
        {
            ThrowIfError(
                NativeMethods.WlanOpenHandle(
                    ClientVersion,
                    IntPtr.Zero,
                    out _,
                    out handle),
                "WindowsのWi-Fi管理機能を開始できませんでした。");
            return operation(handle);
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                NativeMethods.WlanCloseHandle(
                    handle,
                    IntPtr.Zero);
            }
        }
    }

    private static uint ReadCount(IntPtr listPointer)
    {
        if (listPointer == IntPtr.Zero)
        {
            throw new InvalidDataException(
                "Windows Wi-Fi APIの応答が不正です。");
        }

        var count = unchecked((uint)Marshal.ReadInt32(listPointer));
        if (count > 1024)
        {
            throw new InvalidDataException(
                "Windows Wi-Fi APIの項目数が上限を超えています。");
        }

        return count;
    }

    private static void ThrowIfError(
        uint errorCode,
        string message)
    {
        if (errorCode == ErrorSuccess)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{message} Windowsエラー: {errorCode}",
            new Win32Exception(unchecked((int)errorCode)));
    }

    private static void FreeMemory(IntPtr pointer)
    {
        if (pointer != IntPtr.Zero)
        {
            NativeMethods.WlanFreeMemory(pointer);
        }
    }

    private static string GetStateLabel(
        WlanInterfaceState state)
    {
        return state switch
        {
            WlanInterfaceState.Connected => "接続中",
            WlanInterfaceState.Disconnected => "未接続",
            WlanInterfaceState.NotReady => "準備中",
            _ => "利用可能",
        };
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode,
        Pack = 4)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Description;

        public WlanInterfaceState State;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode,
        Pack = 4)]
    private struct WlanProfileInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ProfileName;

        public uint Flags;
    }

    private enum WlanInterfaceState
    {
        NotReady = 0,
        Connected = 1,
        AdHocNetworkFormed = 2,
        Disconnecting = 3,
        Disconnected = 4,
        Associating = 5,
        Discovering = 6,
        Authenticating = 7,
    }

    private static class NativeMethods
    {
        [DllImport("wlanapi.dll")]
        internal static extern uint WlanOpenHandle(
            uint clientVersion,
            IntPtr reserved,
            out uint negotiatedVersion,
            out IntPtr clientHandle);

        [DllImport("wlanapi.dll")]
        internal static extern uint WlanCloseHandle(
            IntPtr clientHandle,
            IntPtr reserved);

        [DllImport("wlanapi.dll")]
        internal static extern uint WlanEnumInterfaces(
            IntPtr clientHandle,
            IntPtr reserved,
            out IntPtr interfaceList);

        [DllImport(
            "wlanapi.dll",
            CharSet = CharSet.Unicode)]
        internal static extern uint WlanGetProfileList(
            IntPtr clientHandle,
            ref Guid interfaceGuid,
            IntPtr reserved,
            out IntPtr profileList);

        [DllImport(
            "wlanapi.dll",
            CharSet = CharSet.Unicode)]
        internal static extern uint WlanGetProfile(
            IntPtr clientHandle,
            ref Guid interfaceGuid,
            string profileName,
            IntPtr reserved,
            out IntPtr profileXml,
            ref uint flags,
            out uint grantedAccess);

        [DllImport(
            "wlanapi.dll",
            CharSet = CharSet.Unicode)]
        internal static extern uint WlanSetProfile(
            IntPtr clientHandle,
            ref Guid interfaceGuid,
            uint flags,
            string profileXml,
            string? allUserProfileSecurity,
            [MarshalAs(UnmanagedType.Bool)] bool overwrite,
            IntPtr reserved,
            out uint reasonCode);

        [DllImport("wlanapi.dll")]
        internal static extern void WlanFreeMemory(
            IntPtr memory);
    }
}

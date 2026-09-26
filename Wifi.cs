using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace WifiRoam;

internal static class Wifi
{
    // ---------- P/Invoke 声明 ----------
    private const string WlanApi = "wlanapi.dll";

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanEnumInterfaces(
        IntPtr hClientHandle,
        IntPtr pReserved,
        out IntPtr ppInterfaceList);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanQueryInterface(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        WlanIntfOpcode OpCode,
        IntPtr pReserved,
        out uint pdwDataSize,
        out IntPtr ppData,
        out WlanOpcodeValueType pWlanOpcodeValueType);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanGetAvailableNetworkList(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        uint dwFlags,
        IntPtr pReserved,
        out IntPtr ppAvailableNetworkList);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanConnect(
        IntPtr hClientHandle,
        ref Guid pInterfaceGuid,
        ref WlanConnectionParameters pConnectionParameters,
        IntPtr pReserved);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanRegisterNotification(
        IntPtr hClientHandle,
        WlanNotificationSource dwNotifSource,
        bool bIgnoreDuplicate,
        WlanNotificationCallbackDelegate funcCallback,
        IntPtr pCallbackContext,
        IntPtr pReserved,
        out WlanNotificationSource pdwPrevNotifSource);

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    // ---------- 常量与枚举 ----------
    private const uint WLAN_CLIENT_VERSION_VISTA = 2;
    private const uint WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES = 0x00000001;
    private const uint WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_PROFILES = 0x00000002;

    private enum WlanIntfOpcode
    {
        wlan_intf_opcode_autoconf_start = 0x000000000,
        wlan_intf_opcode_autoconf_enabled,
        wlan_intf_opcode_background_scan_enabled,
        wlan_intf_opcode_media_streaming_mode,
        wlan_intf_opcode_radio_state,
        wlan_intf_opcode_bss_type,
        wlan_intf_opcode_interface_state,
        wlan_intf_opcode_current_connection,
        wlan_intf_opcode_channel_number,
        wlan_intf_opcode_supported_infrastructure_auth_cipher_pairs,
        wlan_intf_opcode_supported_adhoc_auth_cipher_pairs,
        wlan_intf_opcode_supported_country_or_region_string_list,
        wlan_intf_opcode_current_operation_mode,
        wlan_intf_opcode_supported_safe_mode,
        wlan_intf_opcode_certified_safe_mode,
    }

    private enum WlanOpcodeValueType
    {
        wlan_opcode_value_type_query_only = 0,
        wlan_opcode_value_type_set_by_group_policy,
        wlan_opcode_value_type_set_by_user,
        wlan_opcode_value_type_invalid,
    }

    [Flags]
    private enum WlanNotificationSource
    {
        None = 0,
        ACM = 0x00000008,
        MSM = 0x00000010,
        Security = 0x00000020,
        IHV = 0x00000040,
        All = 0x0000FFFF,
    }

    private delegate void WlanNotificationCallbackDelegate(
        IntPtr pNotifyData,
        IntPtr pContext);

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanConnectionParameters
    {
        public WlanConnectionMode wlanConnectionMode;
        public IntPtr strProfile;
        public IntPtr pDot11Ssid;
        public IntPtr pDesiredBssidList;
        public Dot11BssType dot11BssType;
        public uint dwFlags;
    }

    private enum WlanConnectionMode
    {
        Profile = 0,
        TemporaryProfile,
        DiscoverySecure,
        DiscoveryUnsecure,
        Auto,
        Invalid
    }

    private enum Dot11BssType
    {
        Infrastructure = 1,
        Independent = 2,
        Any = 3
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

    // ---------- 结构体：显式偏移 ----------

    [StructLayout(LayoutKind.Explicit, Size = 648)]
    private struct WlanConnectionAttributes
    {
        [FieldOffset(0)] public WlanInterfaceState isState;
        [FieldOffset(4)] public WlanConnectionMode wlanConnectionMode;
        [FieldOffset(520)] public Dot11Ssid dot11Ssid;
        [FieldOffset(556)] public Dot11BssType dot11BssType;
        [FieldOffset(560)] public uint uNumberOfBssids;
        [FieldOffset(564)] public int bNetworkConnectable;
        [FieldOffset(568)] public uint wlanNotConnectableReason;
        [FieldOffset(572)] public uint uNumberOfPhyTypes;
        [FieldOffset(608)] public int bMorePhyTypes;
        [FieldOffset(612)] public uint wlanSignalQuality;
        [FieldOffset(616)] public int bSecurityEnabled;
        [FieldOffset(620)] public uint dot11AuthAlgorithm;
        [FieldOffset(624)] public uint dot11CipherAlgorithm;
        [FieldOffset(628)] public Guid dot11Bssid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 36)]
    private struct Dot11Ssid
    {
        [FieldOffset(0)] public uint uSSIDLength;
        [FieldOffset(4)] public byte b0;
        [FieldOffset(5)] public byte b1;
        [FieldOffset(6)] public byte b2;
        [FieldOffset(7)] public byte b3;
        [FieldOffset(8)] public byte b4;
        [FieldOffset(9)] public byte b5;
        [FieldOffset(10)] public byte b6;
        [FieldOffset(11)] public byte b7;
        [FieldOffset(12)] public byte b8;
        [FieldOffset(13)] public byte b9;
        [FieldOffset(14)] public byte b10;
        [FieldOffset(15)] public byte b11;
        [FieldOffset(16)] public byte b12;
        [FieldOffset(17)] public byte b13;
        [FieldOffset(18)] public byte b14;
        [FieldOffset(19)] public byte b15;
        [FieldOffset(20)] public byte b16;
        [FieldOffset(21)] public byte b17;
        [FieldOffset(22)] public byte b18;
        [FieldOffset(23)] public byte b19;
        [FieldOffset(24)] public byte b20;
        [FieldOffset(25)] public byte b21;
        [FieldOffset(26)] public byte b22;
        [FieldOffset(27)] public byte b23;
        [FieldOffset(28)] public byte b24;
        [FieldOffset(29)] public byte b25;
        [FieldOffset(30)] public byte b26;
        [FieldOffset(31)] public byte b27;
        [FieldOffset(32)] public byte b28;
        [FieldOffset(33)] public byte b29;
        [FieldOffset(34)] public byte b30;
        [FieldOffset(35)] public byte b31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 628)]
    private struct WlanAvailableNetwork
    {
        [FieldOffset(512)] public Dot11Ssid dot11Ssid;
        [FieldOffset(548)] public Dot11BssType dot11BssType;
        [FieldOffset(552)] public uint uNumberOfBssids;
        [FieldOffset(556)] public int bNetworkConnectable;
        [FieldOffset(560)] public uint wlanNotConnectableReason;
        [FieldOffset(564)] public uint uNumberOfPhyTypes;
        [FieldOffset(600)] public int bMorePhyTypes;
        [FieldOffset(604)] public uint wlanSignalQuality;
        [FieldOffset(608)] public int bSecurityEnabled;
        [FieldOffset(612)] public uint dot11AuthAlgorithm;
        [FieldOffset(616)] public uint dot11CipherAlgorithm;
        [FieldOffset(620)] public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanInterfaceInfoList
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
    }

    // ---------- 内部状态 ----------
    private static IntPtr _clientHandle = IntPtr.Zero;
    private static Guid _interfaceGuid;
    private static bool _initialized;

    private static WlanNotificationCallbackDelegate? _callback;

    private static DateTime _lastSwitchTime = DateTime.MinValue;
    private static DateTime _lastEvaluate = DateTime.MinValue;
    private static string _currentSsid = "";
    private static uint _currentSignal = 0;

    // 扫描缓存，供信号匹配复用
    private static DateTime _lastScanTime = DateTime.MinValue;
    private static List<(string Ssid, uint Signal, bool HasProfile)> _lastScan = new();

    // 配置
    public static int SignalThreshold { get; set; } = 30;
    public static int TargetThreshold { get; set; } = 45;
    public static int CooldownSeconds { get; set; } = 60;
    public static HashSet<string> Blocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<string> Priority { get; set; } = new();

    public static event Action<string, uint>? StatusChanged;
    public static event Action<string>? Log;

    // ---------- 初始化 ----------
    public static bool Initialize()
    {
        if (_initialized) return true;

        var res = WlanOpenHandle(WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _, out _clientHandle);
        if (res != 0)
        {
            Log?.Invoke($"WlanOpenHandle 失败: {res}");
            return false;
        }

        if (!GetFirstInterface(out _interfaceGuid))
        {
            Log?.Invoke("未找到无线网卡");
            WlanCloseHandle(_clientHandle, IntPtr.Zero);
            _clientHandle = IntPtr.Zero;
            return false;
        }

        _callback = OnWlanNotification;
        res = WlanRegisterNotification(
            _clientHandle,
            WlanNotificationSource.ACM | WlanNotificationSource.MSM,
            true,
            _callback,
            IntPtr.Zero,
            IntPtr.Zero,
            out _);

        if (res != 0)
        {
            Log?.Invoke($"WlanRegisterNotification 失败: {res}");
        }

        try
        {
            Microsoft.Win32.SystemEvents.PowerModeChanged += (_, e) =>
            {
                if (e.Mode == Microsoft.Win32.PowerModes.Resume)
                {
                    Log?.Invoke("系统唤醒，准备重新评估网络");
                    System.Threading.Tasks.Task.Delay(5000).ContinueWith(_ => Tick());
                }
            };
        }
        catch (Exception ex)
        {
            Log?.Invoke($"注册电源事件失败: {ex.Message}");
        }

        _initialized = true;
        Log?.Invoke("WLAN 初始化成功");
        return true;
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        _initialized = false;
        if (_clientHandle != IntPtr.Zero)
        {
            WlanCloseHandle(_clientHandle, IntPtr.Zero);
            _clientHandle = IntPtr.Zero;
        }
    }

    private static bool GetFirstInterface(out Guid guid)
    {
        guid = Guid.Empty;
        var res = WlanEnumInterfaces(_clientHandle, IntPtr.Zero, out var ppList);
        if (res != 0 || ppList == IntPtr.Zero) return false;

        try
        {
            const int headerSize = 8;
            var count = Marshal.ReadInt32(ppList, 0);
            if (count == 0) return false;

            var itemPtr = IntPtr.Add(ppList, headerSize);
            guid = Marshal.PtrToStructure<Guid>(itemPtr);
            return true;
        }
        finally
        {
            WlanFreeMemory(ppList);
        }
    }

    // ---------- 获取当前连接 ----------
    public static bool TryGetCurrentConnection(out string ssid, out uint signal)
    {
        ssid = "";
        signal = 0;

        if (!_initialized && !Initialize()) return false;

        var res = WlanQueryInterface(
            _clientHandle,
            ref _interfaceGuid,
            WlanIntfOpcode.wlan_intf_opcode_current_connection,
            IntPtr.Zero,
            out _,
            out var ppData,
            out _);

        if (res != 0 || ppData == IntPtr.Zero) return false;

        try
        {
            var isState = Marshal.ReadInt32(ppData, 0);
            if (isState != 1) return false;

            var ssidStruct = Marshal.PtrToStructure<Dot11Ssid>(IntPtr.Add(ppData, 520));
            ssid = ReadSsid(ssidStruct);
            if (string.IsNullOrEmpty(ssid)) return false;
        }
        finally
        {
            WlanFreeMemory(ppData);
        }

        // 信号从扫描缓存里匹配，避免 WlanConnectionAttributes 布局差异
        signal = GetSignalFromScan(ssid);

        _currentSsid = ssid;
        _currentSignal = signal;
        StatusChanged?.Invoke(ssid, signal);
        return true;
    }

    private static uint GetSignalFromScan(string ssid)
    {
        if ((DateTime.Now - _lastScanTime).TotalSeconds > 5)
        {
            _lastScan = ScanAvailableNetworks();
            _lastScanTime = DateTime.Now;
        }

        foreach (var item in _lastScan)
        {
            if (string.Equals(item.Ssid, ssid, StringComparison.OrdinalIgnoreCase))
                return item.Signal;
        }
        return 0;
    }

    // ---------- 扫描可用网络 ----------
    public static List<(string Ssid, uint Signal, bool HasProfile)> ScanAvailableNetworks()
    {
        var result = new List<(string Ssid, uint Signal, bool HasProfile)>();
        if (!_initialized && !Initialize()) return result;

        var flags = WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_PROFILES
                  | WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES;

        var res = WlanGetAvailableNetworkList(
            _clientHandle,
            ref _interfaceGuid,
            flags,
            IntPtr.Zero,
            out var ppList);

        if (res != 0)
        {
            Log?.Invoke($"WlanGetAvailableNetworkList 失败: {res}");
            return result;
        }
        if (ppList == IntPtr.Zero)
        {
            Log?.Invoke("WlanGetAvailableNetworkList 返回空指针");
            return result;
        }

        try
        {
            const int headerSize = 8;
            const int itemSize = 628;

            var merged = new Dictionary<string, (uint Signal, bool HasProfile)>(StringComparer.OrdinalIgnoreCase);

            var count = Marshal.ReadInt32(ppList, 0);
            for (int i = 0; i < count; i++)
            {
                var itemPtr = IntPtr.Add(ppList, headerSize + i * itemSize);

                var profileName = ReadStringW(itemPtr, 256);
                var network = Marshal.PtrToStructure<WlanAvailableNetwork>(itemPtr);
                var netSsid = ReadSsid(network.dot11Ssid);
                var netSignal = network.wlanSignalQuality;
                var hasProfile = !string.IsNullOrEmpty(profileName);

                if (string.IsNullOrEmpty(netSsid)) continue;
                if (Blocked.Contains(netSsid)) continue;

                if (merged.TryGetValue(netSsid, out var existing))
                {
                    merged[netSsid] = (
                        Math.Max(existing.Signal, netSignal),
                        existing.HasProfile || hasProfile
                    );
                }
                else
                {
                    merged[netSsid] = (netSignal, hasProfile);
                }
            }

            foreach (var kv in merged)
            {
                result.Add((kv.Key, kv.Value.Signal, kv.Value.HasProfile));
            }

            result.Sort((a, b) => b.Signal.CompareTo(a.Signal));
        }
        finally
        {
            WlanFreeMemory(ppList);
        }

        return result;
    }

    // ---------- 自动切换逻辑 ----------
    public static void EvaluateAndSwitch()
    {
        if (!TryGetCurrentConnection(out var currentSsid, out var currentSignal))
            return;

        if (currentSignal >= SignalThreshold)
            return;

        if ((DateTime.Now - _lastSwitchTime).TotalSeconds < CooldownSeconds)
            return;

        var available = ScanAvailableNetworks();
        var candidates = available
            .Where(n => n.HasProfile)
            .Where(n => n.Signal >= TargetThreshold)
            .Where(n => !string.Equals(n.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            return;

        var ordered = candidates
            .OrderBy(n => Priority.IndexOf(n.Ssid) >= 0 ? Priority.IndexOf(n.Ssid) : int.MaxValue)
            .ThenByDescending(n => n.Signal)
            .ToList();

        var target = ordered.First();
        Log?.Invoke($"信号 {currentSignal}% 低于阈值，准备切换到 {target.Ssid} ({target.Signal}%)");
        if (Connect(target.Ssid))
        {
            _lastSwitchTime = DateTime.Now;
            Log?.Invoke($"已切换到 {target.Ssid}");
        }
        else
        {
            Log?.Invoke($"切换到 {target.Ssid} 失败");
        }
    }

    // ---------- 连接指定 SSID ----------
    public static bool Connect(string ssid)
    {
        if (!_initialized && !Initialize()) return false;

        var profileNamePtr = Marshal.StringToHGlobalUni(ssid);
        try
        {
            var connParams = new WlanConnectionParameters
            {
                wlanConnectionMode = WlanConnectionMode.Profile,
                strProfile = profileNamePtr,
                pDot11Ssid = IntPtr.Zero,
                pDesiredBssidList = IntPtr.Zero,
                dot11BssType = Dot11BssType.Any,
                dwFlags = 0
            };

            var res = WlanConnect(_clientHandle, ref _interfaceGuid, ref connParams, IntPtr.Zero);
            return res == 0;
        }
        finally
        {
            Marshal.FreeHGlobal(profileNamePtr);
        }
    }

    // ---------- 通知回调 ----------
    private static void OnWlanNotification(IntPtr pNotifyData, IntPtr pContext)
    {
        var now = DateTime.Now;
        if ((now - _lastEvaluate).TotalMilliseconds < 1000) return;
        _lastEvaluate = now;

        try
        {
            EvaluateAndSwitch();
        }
        catch (Exception ex)
        {
            Log?.Invoke($"通知处理异常: {ex.Message}");
        }
    }

    // ---------- 工具方法 ----------
    private static string ReadSsid(Dot11Ssid ssid)
    {
        var len = (int)ssid.uSSIDLength;
        if (len <= 0 || len > 32) return "";

        var bytes = new byte[32];
        bytes[0] = ssid.b0;  bytes[1] = ssid.b1;  bytes[2] = ssid.b2;  bytes[3] = ssid.b3;
        bytes[4] = ssid.b4;  bytes[5] = ssid.b5;  bytes[6] = ssid.b6;  bytes[7] = ssid.b7;
        bytes[8] = ssid.b8;  bytes[9] = ssid.b9;  bytes[10] = ssid.b10; bytes[11] = ssid.b11;
        bytes[12] = ssid.b12; bytes[13] = ssid.b13; bytes[14] = ssid.b14; bytes[15] = ssid.b15;
        bytes[16] = ssid.b16; bytes[17] = ssid.b17; bytes[18] = ssid.b18; bytes[19] = ssid.b19;
        bytes[20] = ssid.b20; bytes[21] = ssid.b21; bytes[22] = ssid.b22; bytes[23] = ssid.b23;
        bytes[24] = ssid.b24; bytes[25] = ssid.b25; bytes[26] = ssid.b26; bytes[27] = ssid.b27;
        bytes[28] = ssid.b28; bytes[29] = ssid.b29; bytes[30] = ssid.b30; bytes[31] = ssid.b31;

        return System.Text.Encoding.UTF8.GetString(bytes, 0, len);
    }

    private static string ReadStringW(IntPtr ptr, int maxChars)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < maxChars; i++)
        {
            var c = (char)Marshal.ReadInt16(ptr, i * 2);
            if (c == 0) break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    public static void Tick()
    {
        if (!_initialized) return;
        EvaluateAndSwitch();
    }
}

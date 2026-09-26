using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace WifiRoam;

internal static class Wifi
{
    // ---------- P/Invoke 声明 ----------
    // .NET 10 必须显式指定 System32，否则单文件 NativeAOT 找不到 wlanapi.dll
    private const string WlanApi = "wlanapi.dll";

    [DllImport(WlanApi, ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out IntPtr phClientHandle,
        out uint pdwNegotiatedVersion);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanInterfaceInfo
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public WlanInterfaceState isState;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanInterfaceInfoList
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        // 后面跟着 WlanInterfaceInfo 数组，手动偏移读取
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanConnectionAttributes
    {
        public WlanInterfaceState isState;
        public WlanConnectionMode wlanConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public Dot11Ssid dot11Ssid;
        public Dot11BssType dot11BssType;
        public uint uNumberOfBssids;
        public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public Dot11PhyType[] dot11PhyTypes;
        public bool bMorePhyTypes;
        public uint wlanSignalQuality;
        public bool bSecurityEnabled;
        public Dot11AuthAlgorithm dot11AuthAlgorithm;
        public Dot11CipherAlgorithm dot11CipherAlgorithm;
        public Guid dot11Bssid;
    }

    private enum Dot11PhyType
    {
        Unknown = 0,
        Any = 0,
        FHSS = 1,
        DSSS = 2,
        IRBaseband = 3,
        OFDM = 4,
        HRDSSS = 5,
        ERP = 6,
        HT = 7,
        VHT = 8,
        DMG = 9,
        HE = 10,
    }

    private enum Dot11AuthAlgorithm
    {
        Open = 1,
        SharedKey = 2,
        WPA = 3,
        WPA_PSK = 4,
        WPA_NONE = 5,
        RSNA = 6,
        RSNA_PSK = 7,
    }

    private enum Dot11CipherAlgorithm
    {
        None = 0,
        WEP40 = 1,
        TKIP = 2,
        CCMP = 4,
        WEP104 = 5,
        BIP = 6,
        GCMP = 8,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dot11Ssid
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAvailableNetwork
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public Dot11Ssid dot11Ssid;
        public Dot11BssType dot11BssType;
        public uint uNumberOfBssids;
        public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public Dot11PhyType[] dot11PhyTypes;
        public bool bMorePhyTypes;
        public uint wlanSignalQuality;
        public bool bSecurityEnabled;
        public Dot11AuthAlgorithm dot11AuthAlgorithm;
        public Dot11CipherAlgorithm dot11CipherAlgorithm;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WlanAvailableNetworkList
    {
        public uint dwNumberOfItems;
        public uint dwIndex;
        // 后面跟着 WlanAvailableNetwork 数组，手动偏移读取
    }

    // ---------- 内部状态 ----------
    private static IntPtr _clientHandle = IntPtr.Zero;
    private static Guid _interfaceGuid;
    private static bool _initialized;
    private static readonly object _lock = new();

    private static WlanNotificationCallbackDelegate? _callback; // 防止 GC 回收

    // 自动切换相关
    private static DateTime _lastSwitchTime = DateTime.MinValue;
    private static string _currentSsid = "";
    private static uint _currentSignal = 0;

    // 配置（由 Storage 模块注入，这里用简单属性，后续可改为从 config.json 读取）
    public static int SignalThreshold { get; set; } = 30;
    public static int TargetThreshold { get; set; } = 45;
    public static int CooldownSeconds { get; set; } = 60;
    public static HashSet<string> Blocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<string> Priority { get; set; } = new();

    // 事件：状态变化时通知 UI
    public static event Action<string, uint>? StatusChanged;
    public static event Action<string>? Log;

    public static bool Initialize()
    {
        if (_initialized) return true;
    
        var res = WlanOpenHandle(WLAN_CLIENT_VERSION_VISTA, IntPtr.Zero, out _clientHandle, out _);
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
    
        // 注册通知
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
            // 不致命，继续，靠定时器兜底
        }
    
        // 监听电源事件：休眠/唤醒后重新评估
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
            var list = Marshal.PtrToStructure<WlanInterfaceInfoList>(ppList);
            if (list.dwNumberOfItems == 0) return false;

            // 第一个接口结构体紧跟在 WlanInterfaceInfoList 之后
            var offset = Marshal.SizeOf<WlanInterfaceInfoList>();
            var infoPtr = IntPtr.Add(ppList, offset);
            var info = Marshal.PtrToStructure<WlanInterfaceInfo>(infoPtr);
            guid = info.InterfaceGuid;
            return true;
        }
        finally
        {
            WlanFreeMemory(ppList);
        }
    }

    // ---------- 获取当前连接 ----------
    public static bool TryGetCurrentConnection(out string ssid, out uint signal, out Guid bssid)
    {
        ssid = "";
        signal = 0;
        bssid = Guid.Empty;

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
            var attr = Marshal.PtrToStructure<WlanConnectionAttributes>(ppData);
            if (attr.isState != WlanInterfaceState.Connected) return false;

            ssid = SsidToString(attr.dot11Ssid);
            signal = attr.wlanSignalQuality;
            bssid = attr.dot11Bssid;

            _currentSsid = ssid;
            _currentSignal = signal;
            StatusChanged?.Invoke(ssid, signal);
            return true;
        }
        finally
        {
            WlanFreeMemory(ppData);
        }
    }

    // ---------- 扫描可用网络 ----------
    public static List<(string Ssid, uint Signal, bool HasProfile)> ScanAvailableNetworks()
    {
        var result = new List<(string, uint, bool)>();
        if (!_initialized && !Initialize()) return result;

        var flags = WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_MANUAL_PROFILES
                  | WLAN_AVAILABLE_NETWORK_INCLUDE_ALL_ADHOC_PROFILES;

        var res = WlanGetAvailableNetworkList(
            _clientHandle,
            ref _interfaceGuid,
            flags,
            IntPtr.Zero,
            out var ppList);

        if (res != 0 || ppList == IntPtr.Zero) return result;

        try
        {
            var list = Marshal.PtrToStructure<WlanAvailableNetworkList>(ppList);
            var offset = Marshal.SizeOf<WlanAvailableNetworkList>();
            var itemSize = Marshal.SizeOf<WlanAvailableNetwork>();

            for (int i = 0; i < list.dwNumberOfItems; i++)
            {
                var itemPtr = IntPtr.Add(ppList, offset + i * itemSize);
                var network = Marshal.PtrToStructure<WlanAvailableNetwork>(itemPtr);
                var ssid = SsidToString(network.dot11Ssid);
                if (string.IsNullOrEmpty(ssid)) continue;
                if (Blocked.Contains(ssid)) continue;

                bool hasProfile = !string.IsNullOrEmpty(network.strProfileName);
                result.Add((ssid, network.wlanSignalQuality, hasProfile));
            }
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
        if (!TryGetCurrentConnection(out var currentSsid, out var currentSignal, out _))
            return;

        if (currentSignal >= SignalThreshold)
            return; // 当前信号还行，不切

        if ((DateTime.Now - _lastSwitchTime).TotalSeconds < CooldownSeconds)
            return; // 冷却中

        var available = ScanAvailableNetworks();
        var candidates = available
            .Where(n => n.HasProfile)
            .Where(n => n.Signal >= TargetThreshold)
            .Where(n => !string.Equals(n.Ssid, currentSsid, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 0)
            return;

        // 按优先级排序，优先级高的在前；同优先级按信号降序
        var ordered = candidates
            .OrderByDescending(n => Priority.IndexOf(n.Ssid) >= 0 ? Priority.IndexOf(n.Ssid) : int.MaxValue)
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

        // 构造 profile XML 或使用已保存 profile 名称。这里假设 SSID 与 profile 名一致。
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
        // 简化处理：收到任何通知都重新评估一次
        // 实际可解析 WLAN_NOTIFICATION_DATA 判断类型，这里为了简洁直接评估
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
    private static string SsidToString(Dot11Ssid ssid)
    {
        if (ssid.ucSSID == null || ssid.uSSIDLength == 0) return "";
        return System.Text.Encoding.UTF8.GetString(ssid.ucSSID, 0, (int)ssid.uSSIDLength);
    }

    // 供外部定时兜底调用
    public static void Tick()
    {
        if (!_initialized) return;
        EvaluateAndSwitch();
    }
}

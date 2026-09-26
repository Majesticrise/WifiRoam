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

    // ---------- 内部状态 ----------
    private static IntPtr _clientHandle = IntPtr.Zero;
    private static Guid _interfaceGuid;
    private static bool _initialized;

    private static WlanNotificationCallbackDelegate? _callback; // 防止 GC 回收

    // 自动切换相关
    private static DateTime _lastSwitchTime = DateTime.MinValue;
    private static DateTime _lastEvaluate = DateTime.MinValue;
    private static string _currentSsid = "";
    private static uint _currentSignal = 0;

    // 配置（由 Storage 模块注入）
    public static int SignalThreshold { get; set; } = 30;
    public static int TargetThreshold { get; set; } = 45;
    public static int CooldownSeconds { get; set; } = 60;
    public static HashSet<string> Blocked { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public static List<string> Priority { get; set; } = new();

    // 事件：状态变化时通知 UI
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
            // WLAN_INTERFACE_INFO_LIST: dwNumberOfItems (4) + dwIndex (4) + 数组
            // WLAN_INTERFACE_INFO: InterfaceGuid(16) + strInterfaceDescription[256](512) + isState(4) = 532
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
            // WLAN_CONNECTION_ATTRIBUTES 手动按偏移读取
            // 偏移 0:  isState (int)       — 1 = Connected
            // 偏移 520: dot11Ssid           — 4 字节长度 + 32 字节 SSID
            // 偏移 612: wlanSignalQuality   — uint
            var isState = Marshal.ReadInt32(ppData, 0);
            if (isState != 1) return false;

            ssid = ReadSsid(IntPtr.Add(ppData, 520));
            signal = (uint)Marshal.ReadInt32(ppData, 612);

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
            // WLAN_AVAILABLE_NETWORK_LIST: dwNumberOfItems (4) + dwIndex (4) + 数组
            // WLAN_AVAILABLE_NETWORK 大小 624 字节:
            //   0:   strProfileName[256]  (512)
            //   512: dot11Ssid            (36)
            //   604: wlanSignalQuality    (4)
            const int headerSize = 8;
            const int itemSize = 624;

            var count = Marshal.ReadInt32(ppList, 0);
            for (int i = 0; i < count; i++)
            {
                var itemPtr = IntPtr.Add(ppList, headerSize + i * itemSize);

                var profileName = ReadStringW(itemPtr, 256);
                var ssid = ReadSsid(IntPtr.Add(itemPtr, 512));
                var signal = (uint)Marshal.ReadInt32(itemPtr, 604);
                var hasProfile = !string.IsNullOrEmpty(profileName);

                if (string.IsNullOrEmpty(ssid)) continue;
                if (Blocked.Contains(ssid)) continue;

                result.Add((ssid, signal, hasProfile));
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
        if (!TryGetCurrentConnection(out var currentSsid, out var currentSignal))
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

        // 按优先级排序：优先级列表里靠前的排前面，不在列表里的排最后；同优先级按信号降序
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

        // 使用已保存 profile 名连接。假设 SSID 与 profile 名一致。
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
        // 节流：1 秒内多次通知只评估一次
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
    private static string ReadSsid(IntPtr ptr)
    {
        // Dot11Ssid: uint uSSIDLength; byte ucSSID[32]
        var len = Marshal.ReadInt32(ptr);
        if (len <= 0 || len > 32) return "";
        var bytes = new byte[len];
        Marshal.Copy(IntPtr.Add(ptr, 4), bytes, 0, len);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static string ReadStringW(IntPtr ptr, int maxChars)
    {
        // 读 WCHAR 数组，遇到 \0 停止
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < maxChars; i++)
        {
            var c = (char)Marshal.ReadInt16(ptr, i * 2);
            if (c == 0) break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    // 供外部定时兜底调用
    public static void Tick()
    {
        if (!_initialized) return;
        EvaluateAndSwitch();
    }
}

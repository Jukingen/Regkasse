# =======================
# Work Mode: 120 Hz + Balanced + Efficiency power tweaks + RTSS OFF + Optional brightness
# =======================

$PLAN_GUID   = "381b4222-f694-41f0-9685-ff5bb260df2e"  # Balanced
$REFRESH_HZ  = 120

# RTSS
$RTSS_PROC   = "RTSS"

# OPTIONAL: External monitor brightness via DDC/CI tool (ControlMyMonitor)
# If you place ControlMyMonitor.exe here, script will try to set VCP 0x10 brightness.
# Download it yourself, then set the path below:
$CMM_EXE     = "C:\Tools\ControlMyMonitor\ControlMyMonitor.exe"
$BRIGHTNESS  = 30   # 0-100 (work mode için 25-40 arası öneririm)

function Set-ActivePowerPlan($guid) {
  Write-Host "Setting power plan -> $guid"
  powercfg /setactive $guid | Out-Null
}

function Tune-PowerPlanForWork($guid) {
  $SUB_PROCESSOR = "54533251-82be-4824-96c1-47b60b740d00"
  $SUB_PCIEXPRESS = "501a4d13-42af-4429-9fd1-a8218c268e20"

  $PROC_MIN = "893dee8e-2bef-41e0-89c6-b55d0929964c"
  $PROC_MAX = "bc5038f7-23e0-4960-96da-33abaf5935ec"
  $SYS_COOLING = "94d3a615-a899-4ac5-ae2b-e4d8f634367f"
  $PCIE_LSPM = "ee12f906-d277-404b-b6da-e5fa1a576df5"

  Write-Host "Applying work/efficiency power tweaks to scheme..."
  # CPU min 5%, max 99% -> Intel’de genelde “boost uçuşlarını” azaltır, konfor/enerji kazanımı sağlar
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $PROC_MIN 5  | Out-Null
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $PROC_MAX 99 | Out-Null

  # Cooling policy: Passive (önce frekans/voltaj kısar, daha sessiz)
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $SYS_COOLING 1 | Out-Null

  # PCIe LSPM: Moderate power savings (idle GPU watt için yardımcı olur)
  powercfg /setacvalueindex $guid $SUB_PCIEXPRESS $PCIE_LSPM 1 | Out-Null

  powercfg /setactive $guid | Out-Null
}

function Stop-RTSSIfRunning() {
  $p = Get-Process -Name $RTSS_PROC -ErrorAction SilentlyContinue
  if ($p) {
    Write-Host "Stopping RTSS..."
    Stop-Process -Name $RTSS_PROC -Force
  } else {
    Write-Host "RTSS not running."
  }
}

function Try-SetExternalMonitorBrightness($exePath, $value) {
  if (-not (Test-Path $exePath)) {
    Write-Host "Brightness tool not found at: $exePath (skipping brightness)"
    return
  }
  # NOTE: Requires monitor DDC/CI enabled.
  # This attempts to set VCP code 0x10 (Brightness) for all monitors:
  Write-Host "Attempting to set external monitor brightness -> $value"
  & $exePath /SetValue 0 16 $value | Out-Null
}

# Refresh-rate setter
Add-Type @"
using System;
using System.Runtime.InteropServices;

public class DisplayUtil {
  [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Ansi)]
  public struct DEVMODE {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)]
    public string dmDeviceName;
    public short dmSpecVersion;
    public short dmDriverVersion;
    public short dmSize;
    public short dmDriverExtra;
    public int dmFields;
    public int dmPositionX;
    public int dmPositionY;
    public int dmDisplayOrientation;
    public int dmDisplayFixedOutput;
    public short dmColor;
    public short dmDuplex;
    public short dmYResolution;
    public short dmTTOption;
    public short dmCollate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)]
    public string dmFormName;
    public short dmLogPixels;
    public int dmBitsPerPel;
    public int dmPelsWidth;
    public int dmPelsHeight;
    public int dmDisplayFlags;
    public int dmDisplayFrequency;
    public int dmICMMethod;
    public int dmICMIntent;
    public int dmMediaType;
    public int dmDitherType;
    public int dmReserved1;
    public int dmReserved2;
    public int dmPanningWidth;
    public int dmPanningHeight;
  }

  [DllImport("user32.dll", CharSet=CharSet.Ansi)]
  public static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

  [DllImport("user32.dll", CharSet=CharSet.Ansi)]
  public static extern int ChangeDisplaySettings(ref DEVMODE devMode, int flags);

  public const int ENUM_CURRENT_SETTINGS = -1;
  public const int CDS_UPDATEREGISTRY = 0x01;
  public const int CDS_TEST = 0x02;
  public const int DISP_CHANGE_SUCCESSFUL = 0;
  public const int DM_DISPLAYFREQUENCY = 0x400000;

  public static void SetRefreshRate(int hz) {
    DEVMODE dm = new DEVMODE();
    dm.dmDeviceName = new String(new char[32]);
    dm.dmFormName = new String(new char[32]);
    dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

    int ok = EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref dm);
    if (ok == 0) throw new Exception("EnumDisplaySettings failed");

    dm.dmDisplayFrequency = hz;
    dm.dmFields |= DM_DISPLAYFREQUENCY;

    int test = ChangeDisplaySettings(ref dm, CDS_TEST);
    if (test != DISP_CHANGE_SUCCESSFUL) {
      throw new Exception("Refresh rate not supported or test failed. Code=" + test);
    }

    int apply = ChangeDisplaySettings(ref dm, CDS_UPDATEREGISTRY);
    if (apply != DISP_CHANGE_SUCCESSFUL) {
      throw new Exception("Apply failed. Code=" + apply);
    }
  }
}
"@

Write-Host "=== WORK MODE ==="
Set-ActivePowerPlan $PLAN_GUID
Tune-PowerPlanForWork $PLAN_GUID

Write-Host "Setting refresh rate -> $REFRESH_HZ Hz"
[DisplayUtil]::SetRefreshRate($REFRESH_HZ)

Stop-RTSSIfRunning

Try-SetExternalMonitorBrightness $CMM_EXE $BRIGHTNESS

Write-Host "Done: Work Mode (120Hz + Balanced + PCIe Moderate + RTSS OFF + Optional brightness)"

# =======================
# Game Mode: 360 Hz + High Performance + Gaming power tweaks + RTSS ON
# =======================

$PLAN_GUID   = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"  # High performance
$REFRESH_HZ  = 360
$RTSS_EXE    = "C:\Program Files (x86)\RivaTuner Statistics Server\RTSS.exe"

function Set-ActivePowerPlan($guid) {
  Write-Host "Setting power plan -> $guid"
  powercfg /setactive $guid | Out-Null
}

function Tune-PowerPlanForGaming($guid) {
  # Subgroup GUIDs:
  $SUB_PROCESSOR = "54533251-82be-4824-96c1-47b60b740d00"
  $SUB_PCIEXPRESS = "501a4d13-42af-4429-9fd1-a8218c268e20"

  # Setting GUIDs:
  $PROC_MIN = "893dee8e-2bef-41e0-89c6-b55d0929964c"
  $PROC_MAX = "bc5038f7-23e0-4960-96da-33abaf5935ec"
  $SYS_COOLING = "94d3a615-a899-4ac5-ae2b-e4d8f634367f"
  $PCIE_LSPM = "ee12f906-d277-404b-b6da-e5fa1a576df5"

  Write-Host "Applying gaming power tweaks to scheme..."
  # CPU min/max 100% (maksimum tepki)
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $PROC_MIN 100 | Out-Null
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $PROC_MAX 100 | Out-Null

  # System cooling policy: Active (fan önce, throttling sonra)
  powercfg /setacvalueindex $guid $SUB_PROCESSOR $SYS_COOLING 0 | Out-Null

  # PCIe Link State Power Management: Off (latency için)
  powercfg /setacvalueindex $guid $SUB_PCIEXPRESS $PCIE_LSPM 0 | Out-Null

  powercfg /setactive $guid | Out-Null
}

function Start-RTSSIfPresent($path) {
  if (Test-Path $path) {
    $running = Get-Process -Name "RTSS" -ErrorAction SilentlyContinue
    if (-not $running) {
      Write-Host "Starting RTSS..."
      Start-Process -FilePath $path | Out-Null
    } else {
      Write-Host "RTSS already running."
    }
  } else {
    Write-Host "RTSS not found at: $path (skipping)"
  }
}

# Refresh-rate setter (current primary display mode)
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

Write-Host "=== GAME MODE ==="
Set-ActivePowerPlan $PLAN_GUID
Tune-PowerPlanForGaming $PLAN_GUID

Write-Host "Setting refresh rate -> $REFRESH_HZ Hz"
[DisplayUtil]::SetRefreshRate($REFRESH_HZ)

Start-RTSSIfPresent $RTSS_EXE

Write-Host "Done: Game Mode (360Hz + High Performance + PCIe Off + RTSS ON)"

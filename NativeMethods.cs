using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MouseHighlighter;

/// <summary>
/// Windows API(user32.dll, kernel32.dll) 호출 모음.
/// WPF만으로는 할 수 없는 일(전역 마우스 감지, 클릭이 통과하는 창, 물리 픽셀 단위 창 이동)을 여기서 처리합니다.
/// 상수 값은 Microsoft 문서(learn.microsoft.com)의 Win32 API 값 그대로입니다.
/// </summary>
internal static class NativeMethods
{
    // ── 마우스 훅 ────────────────────────────────
    public const int WH_MOUSE_LL = 14;            // 저수준(전역) 마우스 훅: 모든 프로그램의 마우스 입력을 받음
    public const int WM_LBUTTONDOWN = 0x0201;     // 왼쪽 버튼 누름
    public const int WM_RBUTTONDOWN = 0x0204;     // 오른쪽 버튼 누름
    public const int WM_MBUTTONDOWN = 0x0207;     // 휠(가운데) 버튼 누름

    // ── 창 확장 스타일 ───────────────────────────
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;   // 마우스 클릭이 창을 통과해 아래 프로그램으로 감
    private const int WS_EX_TOOLWINDOW = 0x00000080;    // 작업 표시줄·Alt+Tab 목록에 안 나옴
    private const int WS_EX_LAYERED = 0x00080000;       // 반투명 창 (WS_EX_TRANSPARENT와 함께 있어야 클릭 통과가 동작)
    private const int WS_EX_NOACTIVATE = 0x08000000;    // 나타나도 포커스를 뺏지 않음 (입력 중인 창이 그대로 유지)

    // ── 창 위치 ──────────────────────────────────
    private static readonly IntPtr HWND_TOPMOST = new(-1);   // 항상 맨 위
    private const uint SWP_NOSIZE = 0x0001;                  // 크기는 그대로 두고 위치만 변경
    private const uint SWP_NOACTIVATE = 0x0010;              // 위치를 옮겨도 포커스를 뺏지 않음

    /// <summary>화면 좌표. 이 앱은 PerMonitorV2 DPI 모드(app.manifest)라서 항상 물리 픽셀 단위입니다.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    /// <summary>마우스 훅이 넘겨주는 정보. 이 앱은 pt(클릭 위치)만 씁니다.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    /// <summary>다음 훅(다른 프로그램)으로 입력을 넘겨줍니다. 반드시 호출해야 클릭이 정상 동작합니다.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    /// <summary>현재 마우스 커서 위치 (물리 픽셀).</summary>
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // ── 커서 이미지 (트레이 아이콘용) ─────────────
    public const int IDC_ARROW = 32512;              // 기본 화살표 커서
    public const uint IMAGE_CURSOR = 2;
    public const uint LR_COPYFROMRESOURCE = 0x4000;  // 원본 커서 파일에서 원하는 크기로 다시 읽기 (늘린 것보다 선명)

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CopyImage(IntPtr h, uint type, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    /// <summary>
    /// 창을 마우스 클릭이 통과하고, 포커스를 뺏지 않고, Alt+Tab에도 안 보이게 만듭니다.
    /// 창 핸들(HWND)이 있어야 하므로 SourceInitialized 이벤트에서 호출합니다.
    /// </summary>
    public static void MakeClickThrough(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE,
            style | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>
    /// 창의 중심을 화면 좌표(물리 픽셀) 위치에 맞추고 맨 위로 올립니다.
    /// WPF의 Left/Top은 DIP 단위라서 모니터 배율(125%, 150% 등)이 섞이면 어긋나므로,
    /// 커서 좌표와 같은 물리 픽셀 단위로 직접 옮깁니다. 매번 맨 위로 올려서 작업 표시줄 등에 가려지지 않게 합니다.
    /// </summary>
    public static void CenterWindowAt(Window window, int screenX, int screenY)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        DpiScale dpi = VisualTreeHelper.GetDpi(window);
        int w = (int)Math.Round(window.Width * dpi.DpiScaleX);   // DIP → 물리 픽셀
        int h = (int)Math.Round(window.Height * dpi.DpiScaleY);
        SetWindowPos(hwnd, HWND_TOPMOST, screenX - w / 2, screenY - h / 2, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE);
    }
}

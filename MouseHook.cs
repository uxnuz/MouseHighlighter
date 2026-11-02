using System.Runtime.InteropServices;
using System.Windows.Input;

namespace MouseHighlighter;

/// <summary>
/// 전역 마우스 훅. 다른 프로그램 위에서 클릭해도 알 수 있습니다.
/// 클릭을 가로채지는 않고 그대로 넘겨 줍니다.
///
/// 주의할 점:
/// - 훅을 설치한 스레드(UI 스레드)에서 콜백이 실행됩니다. 그래서 ButtonDown 처리기에서 바로 창을 만들어도 됩니다.
/// - 콜백이 오래 걸리면 PC 전체의 마우스가 버벅입니다 (Windows는 일정 시간이 지나면 훅을 건너뜀).
///   ButtonDown 처리기에서는 무거운 작업(파일 읽기, 네트워크 등)을 하지 마세요.
/// - 사용하지 않을 때는 Dispose()로 해제합니다. App.UpdateActiveState()가 이 일을 맡습니다.
/// </summary>
internal sealed class MouseHook : IDisposable
{
    /// <summary>버튼과 화면 좌표(물리 픽셀)를 넘겨 줍니다.</summary>
    public event Action<MouseButton, int, int>? ButtonDown;

    // 델리게이트가 가비지 컬렉션되면 훅이 죽으므로 필드에 붙잡아 둡니다
    private readonly NativeMethods.LowLevelMouseProc _proc;
    private IntPtr _hook;

    public MouseHook()
    {
        _proc = HookCallback;
        // 저수준 훅은 DLL 주입 없이 이 프로세스 안에서 동작합니다. 마지막 인자 0 = 모든 스레드(전역)
        _hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException($"마우스 훅 설치 실패 (오류 {Marshal.GetLastWin32Error()})");
    }

    /// <summary>
    /// Windows가 마우스 입력(이동·클릭·휠)마다 호출합니다. 여기서는 '버튼 누름'만 골라 ButtonDown으로 알립니다.
    /// 버튼을 떼는 순간(WM_LBUTTONUP 등)이 필요하면 NativeMethods에 상수를 추가하고 여기서 분기하면 됩니다.
    /// </summary>
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)   // 음수면 처리하지 말고 바로 넘기라는 뜻 (Win32 규칙)
        {
            MouseButton? button = (int)wParam switch
            {
                NativeMethods.WM_LBUTTONDOWN => MouseButton.Left,
                NativeMethods.WM_RBUTTONDOWN => MouseButton.Right,
                NativeMethods.WM_MBUTTONDOWN => MouseButton.Middle,
                _ => null,
            };

            if (button is { } b)
            {
                var info = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                ButtonDown?.Invoke(b, info.pt.X, info.pt.Y);
            }
        }
        // 다음 훅/프로그램으로 그대로 넘김. 여기서 0이 아닌 값을 바로 반환하면 클릭이 막히니 주의
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}

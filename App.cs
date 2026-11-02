using System.Windows;
using System.Windows.Input;
using Forms = System.Windows.Forms;

namespace MouseHighlighter;

/// <summary>
/// 프로그램 시작점이자 전체를 묶는 곳.
/// - 커서 원(CursorCircleWindow), 마우스 훅(MouseHook), 트레이 아이콘, 설정 창을 만들고 연결합니다.
/// - 무엇을 켜고 끌지는 UpdateActiveState() 한 곳에서만 결정합니다.
/// 트레이 아이콘 우클릭: 설정 / 강조 켜기·끄기 / 종료. 더블클릭: 켜기·끄기.
/// </summary>
public sealed class App : Application
{
    private CursorCircleWindow? _circle;
    private MouseHook? _hook;               // 클릭 효과가 꺼져 있으면 null (훅 해제 상태)
    private Forms.NotifyIcon? _tray;
    private SettingsWindow? _settingsWindow; // 열려 있을 때만 값이 있음
    private bool _enabled = true;           // 트레이의 전체 켜기/끄기 (저장하지 않음, 실행할 때마다 켜진 상태로 시작)

    // App.xaml 없이 코드로만 만든 앱이라 Main을 직접 둡니다.
    [STAThread]
    public static void Main()
    {
        // 두 번 실행되지 않도록 막기 (이미 실행 중이면 조용히 종료)
        using var mutex = new Mutex(true, "MouseHighlighter_SingleInstance", out bool isFirst);
        if (!isFirst) return;

        new App().Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 보이는 창이 없어도(트레이에만 있어도) 앱이 꺼지지 않도록, 종료는 '종료' 메뉴에서만
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _circle = new CursorCircleWindow();
        UpdateActiveState();
        Settings.Changed += UpdateActiveState;   // 설정 창에서 체크박스를 바꾸면 다시 판단

        _tray = CreateTrayIcon();

        // MouseHighlighter.exe --settings 로 실행하면 설정 창을 바로 엽니다 (테스트할 때 편리)
        if (e.Args.Contains("--settings")) OpenSettings();
    }

    /// <summary>
    /// 전체 켜기/끄기(트레이)와 효과별 켜기/끄기(설정)를 보고
    /// 필요한 것만 돌아가게 합니다. 꺼진 효과는 CPU를 쓰지 않습니다.
    /// - 커서 원: 꺼지면 위치 추적 루프를 멈춤
    /// - 클릭 효과: 꺼지면 마우스 훅 자체를 해제
    /// </summary>
    private void UpdateActiveState()
    {
        var s = Settings.Current;
        _circle?.SetActive(_enabled && s.CircleEnabled);

        bool wantHook = _enabled && s.ClickEffectEnabled;
        if (wantHook && _hook is null)
        {
            _hook = new MouseHook();
            _hook.ButtonDown += OnButtonDown;
        }
        else if (!wantHook && _hook is not null)
        {
            _hook.Dispose();
            _hook = null;
        }
    }

    /// <summary>마우스 버튼을 누를 때마다 그 자리에 물결 창을 하나 띄웁니다. 물결은 끝나면 스스로 닫힙니다.</summary>
    private void OnButtonDown(MouseButton button, int x, int y)
    {
        var s = Settings.Current;
        string color = button switch
        {
            MouseButton.Right => s.RightClickColor,
            MouseButton.Middle => s.MiddleClickColor,
            _ => s.LeftClickColor,
        };
        new RippleWindow(x, y, color).Show();
    }

    private void OpenSettings()
    {
        // 이미 열려 있으면 새로 만들지 않고 앞으로 가져오기
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    /// <summary>
    /// 트레이 아이콘과 우클릭 메뉴를 만듭니다.
    /// WPF에는 트레이 아이콘 기능이 없어서 WinForms의 NotifyIcon을 씁니다 (csproj의 UseWindowsForms).
    /// 메뉴 항목을 추가하려면 ToolStripMenuItem을 만들어 menu.Items에 넣으면 됩니다.
    /// </summary>
    private Forms.NotifyIcon CreateTrayIcon()
    {
        var settingsItem = new Forms.ToolStripMenuItem("설정");
        settingsItem.Font = new System.Drawing.Font(settingsItem.Font, System.Drawing.FontStyle.Bold);
        settingsItem.Click += (_, _) => OpenSettings();

        var toggleItem = new Forms.ToolStripMenuItem("강조 켜기/끄기") { Checked = true };
        toggleItem.Click += (_, _) =>
        {
            SetEnabled(!_enabled);
            toggleItem.Checked = _enabled;
        };

        var exitItem = new Forms.ToolStripMenuItem("종료");
        exitItem.Click += (_, _) => Shutdown();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(settingsItem);
        menu.Items.Add(toggleItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        var tray = new Forms.NotifyIcon
        {
            Icon = TrayIcon.CreateFromArrowCursor(),
            Text = "마우스 강조",   // 마우스를 올리면 보이는 이름
            ContextMenuStrip = menu,
            Visible = true,
        };
        // 아이콘을 더블클릭해도 켜기/끄기
        tray.DoubleClick += (_, _) =>
        {
            SetEnabled(!_enabled);
            toggleItem.Checked = _enabled;
        };
        return tray;
    }

    private void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        UpdateActiveState();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Settings.Save();
        _hook?.Dispose();   // 훅은 반드시 해제 (안 하면 Windows가 프로세스 종료 때 정리하긴 하지만 명시적으로)
        if (_tray is not null)
        {
            _tray.Visible = false;   // 종료 후 트레이에 유령 아이콘이 남지 않도록
            _tray.Dispose();
        }
        base.OnExit(e);
    }
}

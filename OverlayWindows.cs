using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MouseHighlighter;

/// <summary>
/// 투명하고 클릭이 통과하는 작은 창의 공통 설정.
/// 화면 전체를 덮는 큰 투명 창 대신 '효과 크기만 한 작은 창'을 움직이는 방식입니다.
/// 큰 투명 창은 매 프레임 다시 그리는 비용이 커서 CPU를 많이 쓰기 때문입니다.
/// </summary>
internal abstract class OverlayWindow : Window
{
    protected OverlayWindow(double size)
    {
        WindowStyle = WindowStyle.None;       // 제목 표시줄·테두리 없음
        AllowsTransparency = true;            // 배경을 완전히 투명하게
        Background = Brushes.Transparent;
        Topmost = true;                       // 다른 창보다 항상 위
        ShowInTaskbar = false;
        ShowActivated = false;                // 나타날 때 포커스를 뺏지 않음
        ResizeMode = ResizeMode.NoResize;
        Width = size;
        Height = size;
        // 위치를 잡기 전에 잠깐이라도 보이지 않도록 화면 밖에서 시작
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000;
        Top = -10000;

        // 창 핸들이 만들어진 직후(화면에 보이기 전)에 클릭 통과 스타일 적용
        SourceInitialized += (_, _) => NativeMethods.MakeClickThrough(this);
    }
}

/// <summary>
/// 커서를 따라다니는 원. 앱 전체에서 하나만 만들어 계속 재사용합니다.
/// 모양은 Settings.Current 값으로 정해지고, 설정이 바뀌면 ApplySettings()로 바로 다시 그립니다.
/// </summary>
internal sealed class CursorCircleWindow : OverlayWindow
{
    private readonly Ellipse _ellipse = new();
    private int _lastX = int.MinValue, _lastY = int.MinValue;   // 마지막으로 옮긴 커서 위치 (같으면 창을 안 옮김)
    private bool _active;

    // 정해진 간격(Settings.CursorUpdateIntervalMs, 기본 33ms ≈ 초당 30회)마다 커서 위치를 확인합니다.
    // 매 프레임(CompositionTarget.Rendering)마다 확인하면 WPF가 그리기 루프를 계속 돌려 CPU를 더 쓰기 때문입니다.
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);

    public CursorCircleWindow() : base(0)   // 크기는 ApplySettings()에서 설정값으로 정함
    {
        Content = _ellipse;
        _timer.Tick += (_, _) => FollowCursor();
        ApplySettings();
        Settings.Changed += ApplySettings;   // 설정 화면에서 바꾸면 바로 반영
    }

    /// <summary>
    /// 켜면 일정 간격으로 커서 위치를 확인해 원을 옮깁니다.
    /// 끄면 창을 숨기고 타이머도 멈춰서 CPU를 쓰지 않습니다.
    /// </summary>
    public void SetActive(bool active)
    {
        if (active == _active) return;
        _active = active;

        if (active)
        {
            ResetPosition();
            Show();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
            Hide();
        }
    }

    /// <summary>설정값(색·크기·투명도)을 원에 적용합니다. Settings.Changed 때마다 호출됩니다.</summary>
    private void ApplySettings()
    {
        var s = Settings.Current;
        Width = Height = s.CircleDiameter + s.CircleBorderThickness * 2;
        _ellipse.Width = _ellipse.Height = s.CircleDiameter;
        _ellipse.Fill = new SolidColorBrush(Settings.ToColor(s.CircleColor, s.CircleOpacity));
        // 테두리는 안쪽보다 진하게 해서 밝은 화면에서도 원이 잘 보이도록
        _ellipse.Stroke = new SolidColorBrush(Settings.ToColor(s.CircleColor, s.CircleOpacity * 2.5));
        _ellipse.StrokeThickness = s.CircleBorderThickness;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, s.CursorUpdateIntervalMs));
        ResetPosition();   // 크기가 바뀌었으니 다음 프레임에 다시 가운데로
    }

    private void FollowCursor()
    {
        if (!IsVisible || !NativeMethods.GetCursorPos(out var p)) return;
        if (p.X == _lastX && p.Y == _lastY) return;   // 안 움직였으면 아무것도 안 함
        _lastX = p.X;
        _lastY = p.Y;
        NativeMethods.CenterWindowAt(this, p.X, p.Y);
    }

    /// <summary>다시 보이게 될 때 바로 제자리로 오도록 마지막 위치를 잊습니다.</summary>
    private void ResetPosition() => _lastX = _lastY = int.MinValue;
}

/// <summary>
/// 클릭한 자리에서 퍼지며 사라지는 물결. 클릭할 때마다 새로 만들고, 애니메이션이 끝나면 스스로 닫힙니다.
/// 클릭마다 창을 따로 두기 때문에 빠르게 여러 번 클릭해도 물결이 겹쳐서 각각 보입니다.
/// </summary>
internal sealed class RippleWindow : OverlayWindow
{
    /// <param name="screenX">클릭 위치 X (물리 픽셀, 마우스 훅에서 받은 값)</param>
    /// <param name="screenY">클릭 위치 Y (물리 픽셀)</param>
    /// <param name="colorHex">물결 색 "#RRGGBB"</param>
    public RippleWindow(int screenX, int screenY, string colorHex)
        : base(Settings.Current.RippleDiameter + Settings.Current.RippleThickness * 2)
    {
        var s = Settings.Current;
        var ring = new Ellipse
        {
            Width = s.RippleDiameter,
            Height = s.RippleDiameter,
            Stroke = new SolidColorBrush(Settings.ToColor(colorHex, 1)),
            StrokeThickness = s.RippleThickness,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.2, 0.2),
        };
        Content = ring;

        // 화면에 보이기 전에 클릭 위치로 옮겨 두기
        SourceInitialized += (_, _) => NativeMethods.CenterWindowAt(this, screenX, screenY);
        // 애니메이션: 20% 크기에서 100%로 커지면서(처음엔 빠르게, 끝엔 천천히) 동시에 투명해짐
        Loaded += (_, _) =>
        {
            var duration = TimeSpan.FromMilliseconds(s.RippleDurationMs);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var grow = new DoubleAnimation(0.2, 1.0, duration) { EasingFunction = ease };
            var fade = new DoubleAnimation(s.RippleOpacity, 0.0, duration);
            fade.Completed += (_, _) => Close();

            ring.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            ring.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            ring.BeginAnimation(OpacityProperty, fade);
        };
    }
}

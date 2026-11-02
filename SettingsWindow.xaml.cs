using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MouseHighlighter;

/// <summary>
/// 트레이 메뉴의 '설정'으로 여는 창. 값을 바꾸면 바로 화면에 반영되고, 창을 닫을 때 저장됩니다.
/// 슬라이더의 '투명도'는 0% = 불투명, 100% = 안 보임 입니다.
///
/// 데이터 흐름 (모든 컨트롤이 같은 길을 탑니다):
///   컨트롤 조작 → Settings.Current 값 변경 → Apply() → 화면 숫자 갱신 + Settings.NotifyChanged()
///   → 커서 원·App이 Settings.Changed를 받아 바로 반영
/// 화면 배치는 SettingsWindow.xaml 에 있습니다.
/// </summary>
public partial class SettingsWindow : Window
{
    // 슬라이더 값을 코드로 채우는 동안에는 변경 이벤트를 무시
    // (InitializeComponent 중에도 ValueChanged가 불리므로 처음부터 true로 시작)
    private bool _loading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadValues();
    }

    /// <summary>Settings.Current 값을 컨트롤에 채웁니다. 창을 열 때와 '기본값으로'를 눌렀을 때 호출됩니다.</summary>
    private void LoadValues()
    {
        _loading = true;
        var s = Settings.Current;
        CircleEnabledCheck.IsChecked = s.CircleEnabled;
        ClickEnabledCheck.IsChecked = s.ClickEffectEnabled;
        CircleSizeSlider.Value = s.CircleDiameter;
        CircleTransparencySlider.Value = (1 - s.CircleOpacity) * 100;
        RippleSizeSlider.Value = s.RippleDiameter;
        RippleTransparencySlider.Value = (1 - s.RippleOpacity) * 100;
        _loading = false;
        RefreshLabels();
    }

    /// <summary>숫자 입력 칸과 색 상자를 현재 설정값으로 다시 표시합니다. (슬라이더는 건드리지 않음)</summary>
    private void RefreshLabels()
    {
        var s = Settings.Current;
        CircleSizeBox.Text = $"{s.CircleDiameter:0}";
        CircleTransparencyBox.Text = $"{(1 - s.CircleOpacity) * 100:0}";
        RippleSizeBox.Text = $"{s.RippleDiameter:0}";
        RippleTransparencyBox.Text = $"{(1 - s.RippleOpacity) * 100:0}";

        CircleColorSwatch.Background = Swatch(s.CircleColor);
        LeftColorSwatch.Background = Swatch(s.LeftClickColor);
        RightColorSwatch.Background = Swatch(s.RightClickColor);
        MiddleColorSwatch.Background = Swatch(s.MiddleClickColor);
    }

    private static Brush Swatch(string hex) => new SolidColorBrush(Settings.ToColor(hex, 1));

    /// <summary>
    /// 슬라이더 4개가 모두 이 하나의 처리기를 씁니다. 어느 슬라이더가 움직였든 4개 값을 전부 다시 읽습니다.
    /// 투명도 슬라이더(0~100%)는 설정에는 불투명도(1~0)로 바꿔서 저장합니다.
    /// </summary>
    private void OnSliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;

        var s = Settings.Current;
        s.CircleDiameter = Math.Round(CircleSizeSlider.Value);
        s.CircleOpacity = 1 - Math.Round(CircleTransparencySlider.Value) / 100;
        s.RippleDiameter = Math.Round(RippleSizeSlider.Value);
        s.RippleOpacity = 1 - Math.Round(RippleTransparencySlider.Value) / 100;
        Apply();
    }

    // ── 숫자 직접 입력 ──────────────────────────
    // 입력 칸의 Tag에 연결된 슬라이더를 움직여서, 슬라이더와 같은 경로로 적용합니다.

    private void OnNumberBoxKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var slider = (Slider)box.Tag;
        switch (e.Key)
        {
            case Key.Enter:
                CommitNumberBox(box);
                box.SelectAll();
                e.Handled = true;
                break;
            case Key.Escape:
                RefreshLabels();   // 입력하던 값 취소
                e.Handled = true;
                break;
            case Key.Up:           // 화살표 위/아래로 1씩 (Shift를 누르면 10씩)
            case Key.Down:
                CommitNumberBox(box);
                double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
                slider.Value = Math.Clamp(slider.Value + (e.Key == Key.Up ? step : -step), slider.Minimum, slider.Maximum);
                box.SelectAll();
                e.Handled = true;
                break;
        }
    }

    private void OnNumberBoxLostFocus(object sender, KeyboardFocusChangedEventArgs e) => CommitNumberBox((TextBox)sender);

    // 칸을 클릭하면 바로 새 숫자를 칠 수 있도록 전체 선택
    private void OnNumberBoxGotFocus(object sender, KeyboardFocusChangedEventArgs e) => ((TextBox)sender).SelectAll();

    // 처음 클릭할 때는 커서 위치를 옮기지 않고 전체 선택이 유지되도록
    private void OnNumberBoxMouseDown(object sender, MouseButtonEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.IsKeyboardFocusWithin) return;
        box.Focus();
        e.Handled = true;
    }

    private void CommitNumberBox(TextBox box)
    {
        var slider = (Slider)box.Tag;
        string text = box.Text.Replace("%", "").Replace("px", "").Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value))
        {
            // 범위를 벗어나면 가장 가까운 끝 값으로
            slider.Value = Math.Clamp(Math.Round(value), slider.Minimum, slider.Maximum);
        }
        // 잘못 입력했거나 범위를 벗어났으면 실제 적용된 값으로 다시 표시
        RefreshLabels();
    }

    // ── 클릭 테스트 영역 ────────────────────────
    // 테스트 영역은 평범한 WPF 영역일 뿐이고, 클릭 효과는 전역 마우스 훅이 그리므로 따로 연결할 것이 없습니다.

    /// <summary>오른쪽 위 펼침 버튼. 창이 SizeToContent라서 패널을 보이게 하면 창이 저절로 넓어집니다.</summary>
    private void OnTestToggleChanged(object sender, RoutedEventArgs e)
    {
        bool open = TestToggle.IsChecked == true;
        TestPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        TestToggle.Content = open ? "◀ 접기" : "클릭 테스트 ▶";
    }

    /// <summary>배경색 선택. 각 RadioButton의 Background 색을 테스트 영역에 그대로 칠합니다 (색은 XAML에서 정함).</summary>
    private void OnTestBackgroundChecked(object sender, RoutedEventArgs e)
    {
        // XAML을 읽는 도중(InitializeComponent)에도 불리므로 아직 없을 수 있음
        if (TestArea is null) return;

        var brush = (SolidColorBrush)((RadioButton)sender).Background;
        TestArea.Background = brush;

        // 어두운 배경에서는 안내 글씨를 밝게 (사람 눈 기준 밝기 공식, 0~255)
        Color c = brush.Color;
        double brightness = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        TestHint.Foreground = brightness < 140 ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
    }

    /// <summary>섹션 제목의 켜기/끄기 체크박스. 실제로 루프·훅을 멈추는 일은 App.UpdateActiveState()가 합니다.</summary>
    private void OnToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var s = Settings.Current;
        s.CircleEnabled = CircleEnabledCheck.IsChecked == true;
        s.ClickEffectEnabled = ClickEnabledCheck.IsChecked == true;
        Apply();
    }

    /// <summary>
    /// 색 상자 버튼 4개가 함께 씁니다. 버튼의 Tag(Circle/Left/Right/Middle)로 어느 색인지 구분합니다.
    /// 색 선택 창은 Windows 기본 색 대화상자(WinForms ColorDialog)입니다.
    /// </summary>
    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        var s = Settings.Current;
        string target = (string)((FrameworkElement)sender).Tag;
        string current = target switch
        {
            "Left" => s.LeftClickColor,
            "Right" => s.RightClickColor,
            "Middle" => s.MiddleClickColor,
            _ => s.CircleColor,
        };

        Color c = Settings.ToColor(current, 1);
        using var dialog = new Forms.ColorDialog
        {
            Color = System.Drawing.Color.FromArgb(c.R, c.G, c.B),
            FullOpen = true,
        };
        if (dialog.ShowDialog() != Forms.DialogResult.OK) return;

        string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        switch (target)
        {
            case "Left": s.LeftClickColor = hex; break;
            case "Right": s.RightClickColor = hex; break;
            case "Middle": s.MiddleClickColor = hex; break;
            default: s.CircleColor = hex; break;
        }
        Apply();
    }

    /// <summary>바뀐 설정을 화면 표시와 실제 효과에 반영합니다. 저장은 창을 닫을 때 한 번만 합니다.</summary>
    private void Apply()
    {
        RefreshLabels();
        Settings.NotifyChanged();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        Settings.ResetToDefaults();
        LoadValues();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        Settings.Save();
        base.OnClosed(e);
    }
}

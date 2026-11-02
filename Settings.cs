using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace MouseHighlighter;

/// <summary>
/// 강조 설정. 트레이 메뉴의 '설정' 화면에서 바꿀 수 있고,
/// %AppData%\MouseHighlighter\settings.json 에 저장되어 다음 실행 때도 유지됩니다.
/// 아래 값들은 처음 실행하거나 '기본값으로'를 눌렀을 때의 기본값입니다.
///
/// - 앱 전체가 Settings.Current 하나를 함께 씁니다.
/// - 값을 바꾼 쪽이 NotifyChanged()를 부르면, Changed를 구독한 곳(커서 원, App)이 바로 반영합니다.
/// - 공개 속성(get/set)은 전부 JSON에 그대로 저장됩니다. 새 설정을 추가할 때는 속성만 만들면 저장·불러오기가 자동입니다.
///   예전 settings.json에 없는 속성은 아래 기본값이 쓰이므로, 속성을 추가해도 기존 사용자 설정이 깨지지 않습니다.
/// - 저장 위치가 exe 위치와 무관하므로 exe를 어디로 옮겨도 설정이 유지됩니다.
/// </summary>
public sealed class Settings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MouseHighlighter", "settings.json");

    // 앱이 처음 Settings를 쓰는 순간 파일에서 한 번 읽어 옵니다
    public static Settings Current { get; private set; } = Load();

    /// <summary>설정이 바뀌면 화면에 바로 반영하도록 알립니다.</summary>
    public static event Action? Changed;

    // ── 커서 주변 원 ──────────────────────────────
    public bool CircleEnabled { get; set; } = true;           // 끄면 커서 위치 추적도 멈춰서 CPU를 쓰지 않음
    public string CircleColor { get; set; } = "#FFE600";
    public double CircleDiameter { get; set; } = 60;          // 원 지름 (DIP, 100% 배율 기준 픽셀)
    public double CircleOpacity { get; set; } = 0.3;          // 0 = 안 보임, 1 = 불투명 (테두리는 이보다 진하게)
    public double CircleBorderThickness { get; set; } = 2;

    // ── 클릭 효과 (물결) ──────────────────────────
    public bool ClickEffectEnabled { get; set; } = true;      // 끄면 마우스 훅도 해제
    public string LeftClickColor { get; set; } = "#FF3C3C";   // 왼쪽 클릭: 빨강
    public string RightClickColor { get; set; } = "#288CFF";  // 오른쪽 클릭: 파랑
    public string MiddleClickColor { get; set; } = "#28C85A"; // 휠 클릭: 초록
    public double RippleDiameter { get; set; } = 90;          // 물결이 퍼지는 최종 지름
    public double RippleOpacity { get; set; } = 0.9;          // 물결이 처음 나타날 때의 불투명도
    public int RippleDurationMs { get; set; } = 450;          // 물결이 사라지기까지 걸리는 시간
    public double RippleThickness { get; set; } = 4;          // 물결 선 두께

    /// <summary>"#RRGGBB" 문자열과 불투명도(0~1)를 WPF 색으로 바꿉니다.</summary>
    public static Color ToColor(string hex, double opacity)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(hex); }
        catch { c = Colors.Yellow; }
        c.A = (byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255);
        return c;
    }

    public static void NotifyChanged() => Changed?.Invoke();

    public static void ResetToDefaults()
    {
        Current = new Settings();
        Changed?.Invoke();
    }

    private static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // 파일이 깨졌으면 기본값으로 시작
        }
        return new Settings();
    }

    /// <summary>설정 창을 닫을 때와 앱을 종료할 때 호출됩니다. (작업 관리자로 강제 종료하면 저장되지 않음)</summary>
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 저장에 실패해도 앱은 계속 동작
        }
    }
}

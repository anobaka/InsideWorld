using Bakabase.Infrastructures.Components.Gui;
using Bakabase.Infrastructures.Components.SystemService;

namespace Bakabase.Shell.Components;

public class CrossPlatformSystemService : ISystemService
{
    public UiTheme UiTheme => UiTheme.FollowSystem;
    public string? Language => null;
    public event Func<UiTheme, Task>? OnUiThemeChange;
}

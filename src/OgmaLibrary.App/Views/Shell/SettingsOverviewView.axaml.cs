using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace OgmaLibrary.App.Views.Shell;

/// <summary>The Settings destination host (Sept-23 Phase 07; sections arrive in Phase 08).</summary>
public sealed partial class SettingsOverviewView : UserControl
{
    /// <summary>Initializes a new instance of the <see cref="SettingsOverviewView"/> class.</summary>
    public SettingsOverviewView() => AvaloniaXamlLoader.Load(this);
}

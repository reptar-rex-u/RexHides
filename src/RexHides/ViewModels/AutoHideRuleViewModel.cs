using CommunityToolkit.Mvvm.ComponentModel;
using RexHides.Models;
using RexHides.Services;

namespace RexHides.ViewModels;

/// <summary>Wraps an AutoHideRule and persists every edit immediately.</summary>
public partial class AutoHideRuleViewModel : ObservableObject
{
    private readonly SettingsService _settings;

    public AutoHideRuleViewModel(AutoHideRule rule, SettingsService settings)
    {
        Rule = rule;
        _settings = settings;
    }

    public AutoHideRule Rule { get; }

    public static RuleMatchType[] MatchTypes { get; } = Enum.GetValues<RuleMatchType>();

    public bool Enabled
    {
        get => Rule.Enabled;
        set => SetAndSave(Rule.Enabled != value, () => Rule.Enabled = value);
    }

    public RuleMatchType MatchType
    {
        get => Rule.MatchType;
        set => SetAndSave(Rule.MatchType != value, () => Rule.MatchType = value);
    }

    public string Pattern
    {
        get => Rule.Pattern;
        set => SetAndSave(Rule.Pattern != value, () => Rule.Pattern = value);
    }

    public bool OnlyFirstLaunch
    {
        get => Rule.OnlyFirstLaunch;
        set => SetAndSave(Rule.OnlyFirstLaunch != value, () => Rule.OnlyFirstLaunch = value);
    }

    private void SetAndSave(bool changed, Action apply, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (!changed)
        {
            return;
        }

        apply();
        OnPropertyChanged(property);
        _settings.Save();
    }
}

namespace Redture.Platform.Abstractions.Gamma;

/// <summary>
/// For platforms that do not restrict gamma ramps at all, which is every one
/// except Windows.
/// </summary>
public sealed class NullGammaRangeUnlock : IGammaRangeUnlock
{
    public GammaRangeState State => GammaRangeState.NotApplicable;

    public bool CanUnlock => false;

    public string? UnlockCommand => null;

    public void Refresh()
    {
    }
}

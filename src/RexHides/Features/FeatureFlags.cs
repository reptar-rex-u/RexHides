namespace RexHides.Features;

public static class FeatureFlags
{
#if GHOME
    public const bool GHome = true;
#else
    public const bool GHome = false;
#endif
}

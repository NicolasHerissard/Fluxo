namespace Finance.Android17.Domain;

/// <summary>Passage entre centimes (<c>long</c>, seule forme stockée) et unité principale (<c>decimal</c>, pour l'affichage).</summary>
public static class MinorUnits
{
    public const decimal PerMajor = 100m;

    public static decimal ToMajor(long minor) => minor / PerMajor;
}

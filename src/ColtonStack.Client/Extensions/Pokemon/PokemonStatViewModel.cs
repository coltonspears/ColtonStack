namespace ColtonStack.Client.Extensions.Pokemon;

/// <summary>One base stat plus its share of the 255 maximum, for the bar.</summary>
public sealed record PokemonStatViewModel(string Name, int Value)
{
    public const int Max = 255;

    public double Fraction => Math.Clamp(Value / (double)Max, 0, 1);
}

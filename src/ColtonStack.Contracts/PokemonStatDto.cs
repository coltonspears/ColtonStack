namespace ColtonStack.Contracts;

/// <summary>One base stat (hp, attack, ...).</summary>
public sealed record PokemonStatDto(string Name, int Value);

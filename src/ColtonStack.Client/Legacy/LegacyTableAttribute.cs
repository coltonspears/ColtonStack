// ============================================================================
//  THE OLD WORLD — FOR DEMO CONTRAST ONLY. DO NOT REFERENCE FROM PRODUCTION CODE.
//
//  This folder deliberately reproduces the patterns ColtonStack retires: a god-tier
//  entity/view-model base class powered by runtime reflection, with "features" that
//  arrive by inheritance whether a model wants them or not.
//
//  It is the ONLY code in this solution allowed to use the APIs listed in
//  BannedSymbols.txt — the pragmas below prove how much suppression that takes.
// ============================================================================

#pragma warning disable RS0030 // Banned APIs are the point of this file.

namespace ColtonStack.Client.Legacy;

/// <summary>
/// Column mapping by string. A typo here compiles fine and fails (or silently no-ops) at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class LegacyTableAttribute(string tableName) : Attribute
{
    public string TableName { get; } = tableName;
}

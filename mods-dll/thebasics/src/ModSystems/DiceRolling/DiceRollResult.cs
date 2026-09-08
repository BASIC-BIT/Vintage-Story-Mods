using System;
using System.Collections.Generic;
using System.Linq;
namespace thebasics.ModSystems.DiceRolling;
public sealed class DiceRollResult
{
    public string Expression { get; }
    public string Reason { get; }
    public decimal Value { get; }
    public bool IsSuccessPool { get; }
    public string Breakdown { get; }
    public int? SimpleSides { get; }
    public IReadOnlyCollection<string> Mechanics { get; }
    public DiceRollResult(string expression, string reason, decimal value, bool isSuccessPool, string breakdown, int? simpleSides, IReadOnlyCollection<string> mechanics)
    {
        Expression = expression; Reason = reason; Value = value; IsSuccessPool = isSuccessPool;
        Breakdown = breakdown; SimpleSides = simpleSides;
        Mechanics = Array.AsReadOnly(mechanics.ToArray());
    }
}

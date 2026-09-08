using System;
namespace thebasics.ModSystems.DiceRolling;
public sealed class DiceRollException : Exception
{
    public string Code { get; }
    public DiceRollException(string code, string message) : base(message) { Code = code; }
}

#pragma warning disable S1694 // Kept as a minimal base type until the old command argument model is retired.
namespace thebasics.Models
{
    public abstract class BaseCommandArgument
    {
        public abstract string Name { get; }
    }

    public class PlayerArgument : BaseCommandArgument
    {
        public override string Name
        {
            get { return "test"; }
        }
    }
}

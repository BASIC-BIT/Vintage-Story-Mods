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
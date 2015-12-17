namespace Qo.Parsing.QueryModel
{
    /// <summary>
    /// Stores a single condition primary key condition.
    /// </summary>
    public class Join
    {
        static public readonly string Symbol = "\u2A1D";
        public Condition Condition { get; set; }

        public Join()
        {
            Condition = new Condition();
        }

        /// <summary>
        /// Converts the Join to its string representation.
        /// </summary>
        public override string ToString()
        {
            return Symbol + " " + Condition;
        }
    }
}

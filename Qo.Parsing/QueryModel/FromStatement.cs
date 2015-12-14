namespace Qo.Parsing.QueryModel
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores a list of Relations.
    /// </summary>
    public class FromStatement
    {
        public List<Relation> Relations { get; set; }

        public FromStatement()
        {
            Relations = new List<Relation>();
        }

        /// <summary>
        /// Converts the FromStatement to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Relations.Any()) return output;

            output = "from ";

            for (var i = 0; i < Relations.Count; i++)
            {
                output += Relations[i].ToString();
                if (i < Relations.Count - 1 && i + 1 < Relations.Count)
                    output = output.Insert(output.Length - 1, ",");
            }

            return output;
        }
    }
}

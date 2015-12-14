namespace Qo.Parsing.QueryModel
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores a list of Attributes.
    /// </summary>
    public class SelectStatement
    {
        public List<Attribute> Attributes { get; set; }

        public SelectStatement()
        {
            Attributes = new List<Attribute>();
        }

        /// <summary>
        /// Converts the SelectStatement to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Attributes.Any()) return output;

            output = "select ";

            for (var i = 0; i < Attributes.Count; i++)
            {
                output += Attributes[i].ToString();
                if (i < Attributes.Count - 1 && i + 1 < Attributes.Count)
                    output = output.Insert(output.Length - 1, ",");
            }

            return output;
        }
    }
}

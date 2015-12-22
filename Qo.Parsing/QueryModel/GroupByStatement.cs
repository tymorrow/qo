namespace Qo.Parsing.QueryModel
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores list of attributes on which a query is grouped
    /// </summary>
    public class GroupByStatement
    {
        public List<dynamic> Attributes { get; set; }

        public GroupByStatement()
        {
            Attributes = new List<dynamic>();
        }

        /// <summary>
        /// Converts the GroupByStatement to its string representation.
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
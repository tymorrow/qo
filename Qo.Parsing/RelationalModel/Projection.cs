namespace Qo.Parsing.RelationalModel
{
    using System.Collections.Generic;
    using System.Linq;
    using QueryModel;

    public class Projection
    {
        public List<Attribute> Attributes { get; set; }

        /// <summary>
        /// Converts the Projection to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Attributes.Any()) return output;

            output = "\u03A0";

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

namespace Qo.Parsing.RelationalModel
{
    using System.Collections.Generic;
    using System.Linq;
    using QueryModel;

    public class Projection
    {
        public List<dynamic> Attributes { get; set; }

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
                if (Attributes[i] is Attribute)
                {
                    var a = Attributes[i] as Attribute;
                    output += a.ToString();
                }
                else if(Attributes[i] is Function)
                {
                    var a = Attributes[i] as Function;
                    output += a.ToString();
                }
                if (i < Attributes.Count - 1 && i + 1 < Attributes.Count)
                    output = output.Insert(output.Length - 1, ",");
            }

            return output;
        }
    }
}

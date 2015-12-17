
namespace Qo.Parsing.QueryModel
{
    using System.Collections.Generic;

    /// <summary>
    /// Stores an type of function and its parameters (attributes/wildcard).
    /// </summary>
    public class Function
    {
        public string Type { get; set; }
        public List<Attribute> Attributes { get; set; }
        public bool IsWildCard { get; set; }

        public Function()
        {
            Attributes = new List<Attribute>();
        }

        /// <summary>
        /// Converts the Function to its string representation.
        /// </summary>
        public override string ToString()
        {
            var display = Type + "(";
            if(IsWildCard)
            {
                display += "*";
            }
            else
            {
                for(int i = 0; i < Attributes.Count; i++)
                {
                    if (i != 0) display += ", ";
                    display += Attributes[i].ToString();
                }
            }
            display += ")";
            return display;
        }
    }
}

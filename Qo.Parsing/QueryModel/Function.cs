
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
                foreach(var a in Attributes)
                {
                    display += a.ToString() + ",";
                }
                display.Remove(display.Length - 1);
            }
            display += ")";
            return display;
        }
    }
}

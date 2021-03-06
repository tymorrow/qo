﻿namespace Qo.Parsing.RelationalModel
{
    using System.Collections.Generic;
    using System.Linq;
    using QueryModel;

    public class Aggregate
    {
        static public readonly string Symbol = "\u2131";
        public List<dynamic> Groupings { get; set; }
        public List<dynamic> Attributes { get; set; }

        /// <summary>
        /// Converts the Aggregate to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Attributes.Any()) return output;

            return "<sub>" + GetGroupingsString() + "</sub> " + Symbol + " <sub>" + GetAttributeString() + "</sub> ";
        }

        public string GetAttributeString()
        {
            var output = string.Empty;
            for (var i = 0; i < Attributes.Count; i++)
            {
                if (Attributes[i] is Attribute)
                {
                    var a = Attributes[i] as Attribute;
                    output += a.ToString();
                }
                else if (Attributes[i] is Function)
                {
                    var a = Attributes[i] as Function;
                    output += a.ToString();
                }
                if (i < Attributes.Count - 1)
                {
                    output += ", ";
                }
            }
            return output;
        }

        public string GetGroupingsString()
        {
            var output = string.Empty;
            for (var i = 0; i < Groupings.Count; i++)
            {
                if (Groupings[i] is Attribute)
                {
                    var a = Groupings[i] as Attribute;
                    output += a.ToString();
                }
                else if (Groupings[i] is Function)
                {
                    var a = Groupings[i] as Function;
                    output += a.ToString();
                }
                if (i < Groupings.Count - 1)
                {
                    output += ", ";
                }
            }
            return output;
        }
    }
}

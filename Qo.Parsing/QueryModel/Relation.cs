namespace Qo.Parsing.QueryModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores a list of Attributes, a subset of which are
    /// store separately as the primary key Attributes.
    /// </summary>
    public class Relation
    {
        public List<Attribute> Attributes { get; set; }
        public List<Attribute> PrimaryKey { get; set; }
        public List<String> Aliases { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }

        public Relation()
        {
            Attributes = new List<Attribute>();
            PrimaryKey = new List<Attribute>();
            Aliases = new List<string>();
            Name = string.Empty;
        }

        /// <summary>
        /// Converts the Relation to its string representation.
        /// </summary>
        public override string ToString()
        {
            return Name + " ";
        }
    }
}

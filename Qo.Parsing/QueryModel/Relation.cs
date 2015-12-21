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
        static public readonly string AliasSymbol = "\u03c1";
        public List<Attribute> Attributes { get; set; }
        public List<Attribute> PrimaryKey { get; set; }
        public List<string> Aliases { get; set; }
        private string name = string.Empty;
        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                Aliases.Add(value);
            }
        }
        public int Priority { get; set; }
        public int TupleCount { get; set; }

        public Relation()
        {
            Attributes = new List<Attribute>();
            PrimaryKey = new List<Attribute>();
            Aliases = new List<string>();
            name = string.Empty;
        }
        public Relation(Relation r)
        {
            Attributes = r.Attributes;
            PrimaryKey = r.PrimaryKey;
            Aliases = new List<string>();
            Name = r.Name;
        }

        /// <summary>
        /// Converts the Relation to its string representation.
        /// </summary>
        public override string ToString()
        {
            if (Aliases.Any())
                return Name + " <sub>" + AliasSymbol + "</sub>" + Aliases.First();
            return Name;
        }
    }
}

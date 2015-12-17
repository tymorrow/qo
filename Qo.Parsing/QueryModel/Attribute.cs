namespace Qo.Parsing.QueryModel
{
    /// <summary>
    /// Stores an Alias, Name, and Type.
    /// </summary>
    public class Attribute
    {
        public string Alias { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int QueryNumber { get; set; }

        public Attribute()
        {
            Alias = string.Empty;
            Name = string.Empty;
            Type = string.Empty;
        }

        /// <summary>
        /// Converts the Attribute to its string representation.
        /// </summary>
        public override string ToString()
        {
            if(Alias.Length > 0)
                return Alias + "." + Name;
            return Name;
        }
    }
}

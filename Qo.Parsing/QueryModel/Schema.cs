namespace Qo.Parsing.QueryModel
{
    using System.Collections.Generic;

    /// <summary>
    /// Stores a list of Relations.
    /// </summary>
    public class Schema
    {
        public List<Relation> Relations { get; set; }
        public Schema()
        {
            Relations = new List<Relation>();
        }
    }
}

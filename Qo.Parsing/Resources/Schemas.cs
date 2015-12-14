namespace Qo.Parsing.Resources
{
    using QueryModel;
    using System.Collections.Generic;

    /// <summary>
    /// Static storage of hardcoded schemas.
    /// </summary>
    public static class Schemas
    {
        public static Schema GetSchema1()
        {
            var relation1 = new Relation
            {
                Name = "sailors",
                Attributes = new List<Attribute>
                {
                    new Attribute { Name = "sid", Type = "int" },
                    new Attribute { Name = "sname", Type = "string" },
                    new Attribute { Name = "rating", Type = "int" },
                    new Attribute { Name = "age", Type = "real" }
                },
                Priority = 1
            };
            relation1.PrimaryKey.AddRange(new[]
            {
                relation1.Attributes[0]
            });

            var relation2 = new Relation
            {
                Name = "boats",
                Attributes = new List<Attribute>
                {
                    new Attribute { Name = "bid", Type = "int" },
                    new Attribute { Name = "bname", Type = "string" },
                    new Attribute { Name = "color", Type = "string" }
                },
                Priority = 3
            };
            relation2.PrimaryKey.AddRange(new[]
            {
                relation2.Attributes[0]
            });

            var relation3 = new Relation
            {
                Name = "reserves",
                Attributes = new List<Attribute>
                {
                    new Attribute { Name = "sid", Type = "int" },
                    new Attribute { Name = "bid", Type = "int" },
                    new Attribute { Name = "day", Type = "date" }
                },
                Priority = 2
            };
            relation3.PrimaryKey.AddRange(new[]
            {
                relation3.Attributes[0],
                relation3.Attributes[1],
                relation3.Attributes[2]
            });
            
            var schema = new Schema();
            schema.Relations.AddRange(new[]{ relation1, relation2, relation3 });

            return schema;
        }
    }
}

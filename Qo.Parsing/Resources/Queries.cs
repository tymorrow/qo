namespace Qo.Parsing.Resources
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using QueryModel;
    using System;
    using System.Linq;
    using Attribute = QueryModel.Attribute;

    /// <summary>
    /// Static storage of hardcoded queries.
    /// </summary>
    public static class Queries
    {
        public static MultiQuery GetQueryA(Schema schema)
        {
            var select = new QueryModel.SelectStatement();
            select.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            from.Relations.Add(relation1);
            from.Relations.Add(relation2);

            var where = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = 103
            };
            where.Conditions.Add(condition1);
            where.Conditions.Add(condition2);
            where.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select,
                From = from,
                Where = where
            };
            multiQuery.Queries.Add(query1);
            return multiQuery;
        }

        public static MultiQuery GetQueryB(Schema schema)
        {
            var select = new QueryModel.SelectStatement();
            select.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation3 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from.Relations.Add(relation1);
            from.Relations.Add(relation2);
            from.Relations.Add(relation3);

            var where = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition3 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "red"
            };
            where.Conditions.Add(condition1);
            where.Conditions.Add(condition2);
            where.Conditions.Add(condition3);
            where.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);
            where.Operators.Add(new Tuple<Condition, Condition>(condition2, condition3), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select,
                From = from,
                Where = where
            };
            multiQuery.Queries.Add(query1);
            return multiQuery;
        }

        public static MultiQuery GetQueryC(Schema schema)
        {
            var select1 = new QueryModel.SelectStatement();
            select1.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from1 = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation3 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from1.Relations.Add(relation1);
            from1.Relations.Add(relation2);
            from1.Relations.Add(relation3);

            var where1 = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition3 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "red"
            };
            where1.Conditions.Add(condition1);
            where1.Conditions.Add(condition2);
            where1.Conditions.Add(condition3);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition2, condition3), BooleanBinaryExpressionType.And);

            var select2 = new QueryModel.SelectStatement();
            select2.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from2 = new FromStatement();
            var relation4 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation5 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation6 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from2.Relations.Add(relation4);
            from2.Relations.Add(relation5);
            from2.Relations.Add(relation6);

            var where2 = new WhereStatement();
            var condition4 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition5 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition6 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "green"
            };
            where2.Conditions.Add(condition4);
            where2.Conditions.Add(condition5);
            where2.Conditions.Add(condition6);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition4, condition5), BooleanBinaryExpressionType.And);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition5, condition6), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select1,
                From = from1,
                Where = where1
            };

            var query2 = new Query
            {
                Select = select2,
                From = from2,
                Where = where2
            };
            multiQuery.Queries.Add(query1);
            multiQuery.Queries.Add(query2);
            multiQuery.Operators.Add(new Tuple<Query, Query>(query1, query2), BinaryQueryExpressionType.Union);
            return multiQuery;
        }

        public static MultiQuery GetQueryD(Schema schema)
        {
            var select = new QueryModel.SelectStatement();
            select.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            from.Relations.Add(relation1);
            from.Relations.Add(relation2);

            var where = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = 100
            };
            var condition3 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "rating" },
                Operator = BooleanComparisonType.GreaterThan,
                RightSide = 5
            };

            var condition4 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "day" },
                Operator = BooleanComparisonType.GreaterThan,
                RightSide = "8/9/09"
            };
            where.Conditions.Add(condition1);
            where.Conditions.Add(condition2);
            where.Conditions.Add(condition3);
            where.Conditions.Add(condition4);
            where.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);
            where.Operators.Add(new Tuple<Condition, Condition>(condition2, condition3), BooleanBinaryExpressionType.And);
            where.Operators.Add(new Tuple<Condition, Condition>(condition3, condition4), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select,
                From = from,
                Where = where
            };
            multiQuery.Queries.Add(query1);
            return multiQuery;
        }

        public static MultiQuery GetQueryE(Schema schema)
        {
            var select1 = new QueryModel.SelectStatement();
            select1.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from1 = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation3 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from1.Relations.Add(relation1);
            from1.Relations.Add(relation2);
            from1.Relations.Add(relation3);

            var where1 = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition3 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "red"
            };
            where1.Conditions.Add(condition1);
            where1.Conditions.Add(condition2);
            where1.Conditions.Add(condition3);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition2, condition3), BooleanBinaryExpressionType.And);

            var select2 = new QueryModel.SelectStatement();
            select2.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from2 = new FromStatement();
            var relation4 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation5 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation6 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from2.Relations.Add(relation4);
            from2.Relations.Add(relation5);
            from2.Relations.Add(relation6);

            var where2 = new WhereStatement();
            var condition4 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition5 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition6 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "green"
            };
            where2.Conditions.Add(condition4);
            where2.Conditions.Add(condition5);
            where2.Conditions.Add(condition6);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition4, condition5), BooleanBinaryExpressionType.And);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition5, condition6), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select1,
                From = from1,
                Where = where1
            };

            var query2 = new Query
            {
                Select = select2,
                From = from2,
                Where = where2
            };
            multiQuery.Queries.Add(query1);
            multiQuery.Queries.Add(query2);
            multiQuery.Operators.Add(new Tuple<Query, Query>(query1, query2), BinaryQueryExpressionType.Intersect);
            return multiQuery;
        }

        public static MultiQuery GetQueryF(Schema schema)
        {
            var select1 = new QueryModel.SelectStatement();
            select1.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from1 = new FromStatement();
            var relation1 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation2 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation3 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from1.Relations.Add(relation1);
            from1.Relations.Add(relation2);
            from1.Relations.Add(relation3);

            var where1 = new WhereStatement();
            var condition1 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition2 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition3 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "red"
            };
            where1.Conditions.Add(condition1);
            where1.Conditions.Add(condition2);
            where1.Conditions.Add(condition3);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition1, condition2), BooleanBinaryExpressionType.And);
            where1.Operators.Add(new Tuple<Condition, Condition>(condition2, condition3), BooleanBinaryExpressionType.And);

            var select2 = new QueryModel.SelectStatement();
            select2.Attributes.Add(new Attribute
            {
                Alias = "s",
                Name = "sname"
            });

            var from2 = new FromStatement();
            var relation4 = schema.Relations.Single(r => r.Name == "sailors");
            relation1.Aliases.Add("s");
            var relation5 = schema.Relations.Single(r => r.Name == "reserves");
            relation2.Aliases.Add("r");
            var relation6 = schema.Relations.Single(r => r.Name == "boats");
            relation3.Aliases.Add("b");
            from2.Relations.Add(relation4);
            from2.Relations.Add(relation5);
            from2.Relations.Add(relation6);

            var where2 = new WhereStatement();
            var condition4 = new Condition
            {
                LeftSide = new Attribute { Alias = "s", Name = "sid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "r", Name = "sid" }
            };
            var condition5 = new Condition
            {
                LeftSide = new Attribute { Alias = "r", Name = "bid" },
                Operator = BooleanComparisonType.Equals,
                RightSide = new Attribute { Alias = "b", Name = "bid" }
            };
            var condition6 = new Condition
            {
                LeftSide = new Attribute { Alias = "b", Name = "color" },
                Operator = BooleanComparisonType.Equals,
                RightSide = "green"
            };
            where2.Conditions.Add(condition4);
            where2.Conditions.Add(condition5);
            where2.Conditions.Add(condition6);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition4, condition5), BooleanBinaryExpressionType.And);
            where2.Operators.Add(new Tuple<Condition, Condition>(condition5, condition6), BooleanBinaryExpressionType.And);

            var multiQuery = new MultiQuery();
            var query1 = new Query
            {
                Select = select1,
                From = from1,
                Where = where1
            };

            var query2 = new Query
            {
                Select = select2,
                From = from2,
                Where = where2
            };
            multiQuery.Queries.Add(query1);
            multiQuery.Queries.Add(query2);
            multiQuery.Operators.Add(new Tuple<Query, Query>(query1, query2), BinaryQueryExpressionType.Except);
            return multiQuery;
        }
    }
}

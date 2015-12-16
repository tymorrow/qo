namespace Qo.Parsing.QueryModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using RelationalModel;
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    /// <summary>
    /// Stores one or more queries separated by a SetOperator
    /// </summary>
    public class MultiQuery
    {
        public static readonly Dictionary<BinaryQueryExpressionType, string> OperatorMap = new Dictionary<BinaryQueryExpressionType, string>
        {
            {BinaryQueryExpressionType.Union, "union "},
            {BinaryQueryExpressionType.Intersect, "intersect "},
            {BinaryQueryExpressionType.Except, "except "}
        };

        public List<Query> Queries { get; set; }

        public Dictionary<Tuple<Query, Query>, BinaryQueryExpressionType> Operators { get; set; }

        public MultiQuery()
        {
            Queries = new List<Query>();
            Operators = new Dictionary<Tuple<Query, Query>, BinaryQueryExpressionType>();
        }

        public override string ToString()
        {
            var output = string.Empty;
            
            if (Queries.Count <= 0) return output;

            if (Queries.Count == 1)
            {
                return Queries.First().ToString();
            }

            output = Queries[0].ToString();
            for (var i = 1; i < Queries.Count; i++)
            {
                var query1 = Queries[i - 1];
                var query2 = Queries[i];
                var op = OperatorMap[Operators[new Tuple<Query, Query>(query1, query2)]];
                output += op + Environment.NewLine + query2 + Environment.NewLine;
            }

            return output;
        }

        /// <summary>
        /// Converts the stored queries into Query tree nodes and returns the root
        /// </summary>
        public Node GetQueryTree()
        {
            var root = new Node();

            if (!Queries.Any()) return root;

            if (!Operators.Any()) return Queries[0].GetQueryTree();

            root.Content = Operators.First().Value;
            root.LeftChild = Queries[0].GetQueryTree();
            root.RightChild = Queries[1].GetQueryTree();

            return root;
        }
    }

    public enum SetOperator
    {
        Union,
        Intersect,
        Except,
        CartesianProduct
    }
}

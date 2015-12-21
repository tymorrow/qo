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
        public static readonly Dictionary<SetOperator, string> OperatorMap = new Dictionary<SetOperator, string>
        {
            {SetOperator.Union, " union "},
            {SetOperator.Intersect, " intersect "},
            {SetOperator.Except, " except "},
            {SetOperator.CartesianProduct, " cartesian product "},
            {SetOperator.Division, " division "}
        };

        public List<dynamic> Queries { get; set; }

        public Dictionary<Tuple<dynamic, dynamic>, SetOperator> Operators { get; set; }

        public MultiQuery()
        {
            Queries = new List<dynamic>();
            Operators = new Dictionary<Tuple<dynamic, dynamic>, SetOperator>();
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
                var op = OperatorMap[Operators[new Tuple<dynamic, dynamic>(query1, query2)]];
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

            if (Queries.Count == 1)
            {
                root = ConvertQueryToNode(Queries[0]);
                root.LeftChild.Parent = root;
            }
            else if (Queries.Count >= 2)
            {
                root.Content = Operators.First().Value;
                root.LeftChild = ConvertQueryToNode(Queries[0]);
                root.LeftChild.Parent = root;
                root.RightChild = ConvertQueryToNode(Queries[1]);
                root.RightChild.Parent = root;
            }

            return root;
        }

        private Node ConvertQueryToNode(dynamic query)
        {
            if (query is Query)
            {
                var q = query as Query;
                return q.GetQueryTree();
            }
            else if (query is MultiQuery)
            {
                var mq = query as MultiQuery;
                return mq.GetQueryTree();
            }
            return new Node();
        }
    }

    public enum SetOperator
    {
        Union,
        Intersect,
        Except,
        CartesianProduct,
        Division
    }
}

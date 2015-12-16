namespace Qo.Parsing.QueryModel
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Stores pairs of conditions joined by a LogicalOperator.
    /// </summary>
    public class WhereStatement
    {
        public static readonly Dictionary<BooleanBinaryExpressionType, string> OperatorMap = new Dictionary<BooleanBinaryExpressionType, string>
        {
            {BooleanBinaryExpressionType.And, "and "},
            {BooleanBinaryExpressionType.Or, "or "}
        };

        public List<Condition> Conditions { get; set; }
        public Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType> Operators { get; set; }

        public WhereStatement()
        {
            Conditions = new List<Condition>();
            Operators = new Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType>();
        }
        /// <summary>
        /// Converts the WhereStatement to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Conditions.Any()) return output;

            output = "where ";

            if (Conditions.Count == 1)
            {
                return output + Conditions.First();
            }

            output += Conditions[0].ToString();
            for (var i = 1; i < Conditions.Count; i++)
            {
                var condition1 = Conditions[i - 1];
                var condition2 = Conditions[i];
                var op = OperatorMap[Operators[new Tuple<Condition, Condition>(condition1, condition2)]];
                output += op + condition2;
            }

            return output;
        }
    }
}

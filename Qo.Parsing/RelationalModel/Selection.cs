namespace Qo.Parsing.RelationalModel
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using QueryModel;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Applies query optimization rules on a query tree 
    /// based on a given schema and query tree
    /// </summary>
    public class Selection
    {
        private readonly Dictionary<BooleanBinaryExpressionType, string> _operatorMap = new Dictionary<BooleanBinaryExpressionType, string>
        {
            {BooleanBinaryExpressionType.And, "AND "},
            {BooleanBinaryExpressionType.Or, "OR "}
        };

        public List<Condition> Conditions { get; set; }
        public Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType> Operators { get; set; }

        public Selection()
        {
            Conditions = new List<Condition>();
            Operators = new Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType>();
        }

        /// <summary>
        /// Converts the Selection to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (!Conditions.Any()) return output;

            output = "\u03C3";

            if (Conditions.Count == 1)
            {
                return output + Conditions.First();
            }

            output += Conditions[0].ToString();
            for (var i = 1; i < Conditions.Count; i++)
            {
                var condition1 = Conditions[i - 1];
                var condition2 = Conditions[i];
                var op = _operatorMap[Operators[new Tuple<Condition, Condition>(condition1, condition2)]];
                output += op + condition2;
            }

            return output;
        }
        /// <summary>
        /// Swaps each pair of Conditions in the store Conditions list.
        /// </summary>
        public void SwapOperators()
        {
            foreach (var condition in Conditions)
            {
                var temp = condition.LeftSide;
                condition.LeftSide = condition.RightSide;
                condition.RightSide = temp;
            }
            
        }
    }
}

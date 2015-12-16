namespace Qo.Parsing.QueryModel
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using System.Collections.Generic;

    /// <summary>
    /// Stores dynamically typed left and right side of a condition
    /// joined by a ConditionalOperator.
    /// </summary>
    public class Condition
    {
        // TODO: Replace with unicode values later.
        public static readonly Dictionary<BooleanComparisonType, string> OperatorMap = new Dictionary<BooleanComparisonType, string>
        {
            {BooleanComparisonType.Equals, "= "},
            {BooleanComparisonType.NotEqualToExclamation, "!= "},
            {BooleanComparisonType.GreaterThan, "> "},
            {BooleanComparisonType.LessThan, "< "},
            {BooleanComparisonType.GreaterThanOrEqualTo, ">= "},
            {BooleanComparisonType.LessThanOrEqualTo, "<= "}
        };
        public dynamic LeftSide { get; set; }
        public BooleanComparisonType Operator { get; set; }
        public dynamic RightSide { get; set; }
        public int QueryNumber { get; set; }

        /// <summary>
        /// Finds the type of the provided side and returns its string
        /// representation.
        /// </summary>
        public string GetSide(object side)
        {
            if (side is string)
            {
                return "\'" + side + "\' ";
            }
            if (side is int)
            {
                return ((int)side) + " ";
            }
            if (side is double)
            {
                return ((double)side) + " ";
            }
            if (side is Attribute)
            {
                return side.ToString();
            }
            return " ";
        }
        /// <summary>
        /// Returns a list of Attributes based on the LeftSide
        /// and RightSide property types.
        /// </summary>
        public List<Attribute> GetSideAttributes()
        {
            var result = new List<Attribute>();

            if (LeftSide is Attribute)
            {
                result.Add(LeftSide);
            }
            if (RightSide is Attribute)
            {
                result.Add(RightSide);
            }

            return result;
        }
        /// <summary>
        /// Converts the Condition to its string representation.
        /// </summary>
        public override string ToString()
        {
            return GetSide(LeftSide) + OperatorMap[Operator] + GetSide(RightSide);
        }
    }
}

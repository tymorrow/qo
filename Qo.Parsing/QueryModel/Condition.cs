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
            {BooleanComparisonType.Equals, " = "},
            {BooleanComparisonType.NotEqualToExclamation, " != "},
            {BooleanComparisonType.GreaterThan, " > "},
            {BooleanComparisonType.LessThan, " < "},
            {BooleanComparisonType.GreaterThanOrEqualTo, " >= "},
            {BooleanComparisonType.LessThanOrEqualTo, " <= "}
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
                int parsedInteger;
                double parsedReal;
                if(int.TryParse(side as string, out parsedInteger))
                {
                    return parsedInteger + " ";
                }
                else if(double.TryParse(side as string, out parsedReal))
                {
                    return parsedReal + " ";
                }
                else
                {
                    return "\'" + side + "\' ";
                }
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
                var s = side as Attribute;
                return s.ToString();
            }
            if (side is Function)
            {
                var s = side as Function;
                return s.ToString();
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
                var s = LeftSide as Attribute;
                result.Add(s);
            }
            if (RightSide is Attribute)
            {
                var s = RightSide as Attribute;
                result.Add(s);
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

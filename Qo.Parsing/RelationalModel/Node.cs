namespace Qo.Parsing.RelationalModel
{
    using System.Globalization;
    using QueryModel;
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Stores dynamic content and references to other nodes.
    /// </summary>
    public class Node
    {
        private static int _idCounter;
        private readonly Dictionary<SetOperator, string> _operatorMap = new Dictionary<SetOperator, string>
        {
            {SetOperator.CartesianProduct, "\u00D7"},
            {SetOperator.Union, "\u222A"},
            {SetOperator.Intersect, "\u2229"},
            {SetOperator.Except, "\u002D"},
        };

        public Node Parent { get; set; }
        public Node LeftChild { get; set; }
        public Node RightChild { get; set; }

        public dynamic Content { get; set; }
        public string Id { get; private set; }

        public Node()
        {
            Id = GetId();
        }

        /// <summary>
        /// Converts the Content property to its appropriate string representation.
        /// </summary>
        public string GetContentString()
        {
            if (Content == null) return string.Empty;

            if (Content is SetOperator) return _operatorMap[Content];

            return Content.ToString();
        }
        /// <summary>
        /// Returns the Node Id.
        /// </summary>
        private static string GetId()
        {
            _idCounter++;
            return _idCounter.ToString(CultureInfo.InvariantCulture);
        }
        /// <summary>
        /// Sets all Node reference properties to null.
        /// </summary>
        public void Clear()
        {
            Parent = null;
            LeftChild = null;
            RightChild = null;
        }
        /// <summary>
        /// Converts the Node to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            if (Content == null) return output;

            if(Content is string)
            {
                output = LeftChild.ToString();
            }
            if (Content is Relation)
            {
                output = Content.ToString();
            }
            else if (Content is Projection)
            {
                output = Content + LeftChild.ToString();
            }
            else if (Content is Selection)
            {
                output = Content + LeftChild.ToString();
            }
            else if (Content is SetOperator)
            {
                output = "(" + LeftChild + _operatorMap[Content] + RightChild + ")";
            }

            return output;
        }
    }
}

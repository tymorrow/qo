namespace Qo.Parsing.RelationalModel
{
    using QueryModel;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Stores dynamic content and references to other nodes.
    /// </summary>
    public class Node
    {
        private static int _idCounter;
        private readonly Dictionary<SetOperator, string> _setOperatorMap = new Dictionary<SetOperator, string>
        {
            {SetOperator.CartesianProduct, " \u00D7 "},
            {SetOperator.Union, " \u222A "},
            {SetOperator.Intersect, " \u2229 "},
            {SetOperator.Except, " \u002D "},
            {SetOperator.Division, " \u00F7 "}
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

            if (Content is SetOperator) return _setOperatorMap[Content];

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
        /// Converts the Node to a CleanNode for display
        /// </summary>
        public CleanNode GetCleanNode()
        {
            var node = new CleanNode();
            if(Content is Relation)
            {
                var c = Content as Relation;
                if (c.Aliases.Any())
                    node.subscript = Relation.AliasSymbol + " " + c.Aliases.First();
                node.name = c.Name;
            }
            else if (Content is Projection)
            {
                var c = Content as Projection;
                node.name = Projection.Symbol;
                node.subscript = c.GetAttributeString();
            }
            else if (Content is Selection)
            {
                var c = Content as Selection;
                node.name = Selection.Symbol;
                node.subscript = c.GetConditionsString();
            }
            else if (Content is SetOperator)
            {
                node.name = _setOperatorMap[Content];
            }

            var children = new List<CleanNode>();
            if(LeftChild != null) children.Add(LeftChild.GetCleanNode());
            if(RightChild != null) children.Add(RightChild.GetCleanNode());
            node.children = children.ToArray();

            return node;
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
            if (Content is int)
            {
                output = Content.ToString();
            }
            if (Content is double)
            {
                output = Content.ToString();
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
                output = "(" + LeftChild + _setOperatorMap[Content] + RightChild + ")";
            }
            else if (Content is Query)
            {
                output = Content.ToString();
            }

            return output;
        }
    }
}

namespace Qo.Parsing.QueryModel
{
    using RelationalModel;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    /// <summary>
    /// Stores a Select, From, and Where Statement.
    /// </summary>
    public class Query
    {
        public string OriginalString { get; set; }
        public SelectStatement Select { get; set; }
        public FromStatement From { get; set; }
        public WhereStatement Where { get; set; }

        public Query()
        {
            Select = new SelectStatement();
            From = new FromStatement();
            Where = new WhereStatement();
        }

        /// <summary>
        /// Converts the Query to its string representation.
        /// </summary>
        public override string ToString()
        {
            var output = string.Empty;

            output += Select + Environment.NewLine;
            output += From + Environment.NewLine;
            output += Where + Environment.NewLine;

            return output;
        }
        /// <summary>
        /// Converts the Select, From, and Where properties into
        /// into Node objects and returns the root.
        /// </summary>
        public Node GetQueryTree()
        {
            var projectionNode = new Node();
            projectionNode.Content = new Projection
            {
                Attributes = Select.Attributes
            };

            Node selectionNode = new Node();
            if (Where.Conditions.Any())
            {
                selectionNode.Content = new Selection
                {
                    Conditions = Where.Conditions,
                    Operators = Where.Operators
                };
            }

            List<Node> relationNodes = new List<Node>();
            foreach(var r in From.Relations)
            {
                relationNodes.Add(GetRelationNode(r));
            }

            var root = projectionNode;
            var iter = root;
            if(selectionNode.Content != null)
            {
                root.LeftChild = selectionNode;
                root.LeftChild.Parent = root;
                iter = iter.LeftChild;
            }

            // TODO: Could do some sorting of relations here to help later.
            if(relationNodes.Count == 1)
            {
                relationNodes[0].Parent = iter;
                relationNodes[0].Parent.LeftChild = relationNodes[0];
            }
            else
            {
                for (var i = 0; i < relationNodes.Count; i++)
                {
                    var cart = new Node();
                    cart.Content = SetOperator.CartesianProduct;

                    cart.Parent = iter;
                    cart.Parent.LeftChild = cart;
                    cart.RightChild = relationNodes[i];
                    cart.RightChild.Parent = cart;

                    if (i >= relationNodes.Count - 2)
                    {
                        cart.LeftChild = relationNodes[i + 1];
                        cart.LeftChild.Parent = cart;
                        i++;
                    }

                    iter = cart;
                }
            }

            return root;
        }
        /// <summary>
        /// Returns a new node whose content is the provided Relation.
        /// </summary>
        private Node GetRelationNode(Relation relation)
        {
            return new Node
            {
                Content = relation
            }; ;
        }
    }
}

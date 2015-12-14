namespace Qo.Parsing.QueryModel
{
    using RelationalModel;
    using System;
    
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
            var root = new Node();
            root.Content = new Projection
            {
                Attributes = Select.Attributes
            };

            root.LeftChild = new Node();
            root.LeftChild.Parent = root;
            root.LeftChild.Content = new Selection
            {
                Conditions = Where.Conditions,
                Operators = Where.Operators
            };

            if(From.Relations.Count == 1)
            {
                root.LeftChild.LeftChild.Parent = root.LeftChild;
                root.LeftChild.LeftChild = GetRelationNode(From.Relations[0]);
            }
            else
            {
                var iterator = root.LeftChild.LeftChild = new Node
                {
                    Parent = root.LeftChild,
                    Content = SetOperator.CartesianProduct
                };

                for (var i = From.Relations.Count - 1; i > 0; i--)
                {
                    iterator.RightChild = GetRelationNode(From.Relations[i]);
                    iterator.RightChild.Parent = iterator;

                    if(i == 1)
                    {
                        iterator.LeftChild = GetRelationNode(From.Relations[i - 1]);
                        iterator.LeftChild.Parent = iterator;
                    }
                    else
                    {
                        iterator.LeftChild = new Node
                        {
                            Parent = iterator,
                            Content = SetOperator.CartesianProduct
                        };
                    }
                    iterator = iterator.LeftChild;
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

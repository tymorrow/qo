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
        public GroupByStatement GroupBy { get; set; }
        public HavingStatement Having { get; set; }

        public Query()
        {
            Select = new SelectStatement();
            From = new FromStatement();
            Where = new WhereStatement();
            GroupBy = new GroupByStatement();
            Having = new HavingStatement();
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
            // Projection
            var projectionNode = new Node();
            var groupByNode = new Node();
            var havingNode = new Node();
            // Selection from Group By
            if (GroupBy.Attributes.Any())
            {
                groupByNode.Content = new Aggregate()
                {
                    Attributes = Select.Attributes,
                    Groupings = GroupBy.Attributes
                };
                if(Having.Conditions.Any())
                {
                    havingNode.Content = new Selection()
                    {
                        Conditions = Having.Conditions,
                        Operators = Having.Operators
                    };
                }
            }
            else
            {
                projectionNode.Content = new Projection
                {
                    Attributes = Select.Attributes
                };
            }

            // Selection from where
            Node selectionNode = new Node();
            if (Where.Conditions.Any())
            {
                selectionNode.Content = new Selection
                {
                    Conditions = Where.Conditions,
                    Operators = Where.Operators
                };
            }

            // Relations
            List<Node> relationNodes = new List<Node>();
            foreach(var r in From.Relations)
            {
                relationNodes.Add(GetRelationNode(r));
            }

            var root = projectionNode;
            var iter = root;
            if (projectionNode.Content != null)
            {
                if (selectionNode.Content != null)
                {
                    root.LeftChild = selectionNode;
                    root.LeftChild.Parent = root;
                    iter = iter.LeftChild;
                }
            }
            else if(groupByNode.Content != null)
            {
                if(havingNode.Content != null)
                {
                    root = havingNode;
                    iter = root;

                    // Add having node
                    root.LeftChild = groupByNode;
                    root.LeftChild.Parent = root;
                    iter = iter.LeftChild;
                    // Add selection node
                    if (selectionNode.Content != null)
                    {
                        iter.LeftChild = selectionNode;
                        iter.LeftChild.Parent = iter;
                        iter = iter.LeftChild;
                    }
                }
                else
                {
                    root = groupByNode;
                    iter = root;

                    if (selectionNode.Content != null)
                    {
                        root.LeftChild = selectionNode;
                        root.LeftChild.Parent = root;
                        iter = iter.LeftChild;
                    }
                }
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

        public void RemoveRedundantRelations()
        {
            var necessary = new List<Relation>();
            foreach(var c in Where.Conditions)
            {
                foreach(var r in From.Relations.Select(l => l as Relation))
                {
                    var atts = r.Attributes.Select(a => a.Name);
                    if (c.LeftSide is Attribute)
                    {
                        var cl = c.LeftSide as Attribute;              
                        if(atts.Contains(cl.Name))
                        {
                            necessary.Add(r);
                        }
                    }
                    if(c.LeftSide is Function)
                    {
                        var cl = c.LeftSide as Function;
                        if(cl.Attributes.Any())
                        {
                            foreach(var a in cl.Attributes.Select(n => n.Name))
                            {
                                if (atts.Contains(a))
                                {
                                    necessary.Add(r);
                                }
                            }
                        }
                    }
                    if(c.RightSide is Attribute)
                    {
                        var cr = c.RightSide as Attribute;
                        if (atts.Contains(cr.Name))
                        {
                            necessary.Add(r);
                        }
                    }
                    if (c.RightSide is Function)
                    {
                        var cr = c.RightSide as Function;
                        if (cr.Attributes.Any())
                        {
                            foreach (var a in cr.Attributes.Select(n => n.Name))
                            {
                                if (atts.Contains(a))
                                {
                                    necessary.Add(r);
                                }
                            }
                        }
                    }
                }
            }
            foreach (var r in necessary.Distinct())
            {
                if(!From.Relations.Contains(r))
                {
                    From.Relations.Add(r);
                }
            }
        }
    }
}

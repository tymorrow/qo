namespace Qo.Parsing
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using QueryModel;
    using RelationalModel;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Attribute = QueryModel.Attribute;

    /// <summary>
    /// Applies query optimization rules on a query tree 
    /// based on a given schema and query tree
    /// </summary>
    public class QoOptimizer
    {
        private readonly Schema _schema;

        public QoOptimizer()
        {
            _schema = Resources.Schemas.GetSchema1();
        }
        public QoOptimizer(Schema schema)
        {
            _schema = schema;
        }

        /// <summary>
        /// Applies all query optimization rules on a query tree 
        /// and outputs the Graphviz files after each optimization.
        /// </summary>
        public void Run(QoPackage package)
        {
            var tree = package.Tree;
            if (tree.Content is SetOperator)
            {
                ApplyRule1(tree.LeftChild);
                ApplyRule1(tree.RightChild);
                package.Optimization1 = tree.GetCleanNode();
                ApplyRule2(tree.LeftChild);
                ApplyRule2(tree.RightChild);
                package.Optimization2 = tree.GetCleanNode();
                //ApplyRule3(tree.LeftChild);
                //ApplyRule3(tree.RightChild);
                package.Optimization3 = tree.GetCleanNode();
                //ApplyRule4(tree.LeftChild);
                //ApplyRule4(tree.RightChild);
                //package.Optimization4 = tree.GetCleanNode();
                //ApplyRule5(tree.LeftChild);
                //ApplyRule5(tree.RightChild);
                //package.Optimization5 = tree.GetCleanNode();
            }
            else
            {
                ApplyRule1(tree);
                package.Optimization1 = tree.GetCleanNode();
                ApplyRule2(tree);
                package.Optimization2 = tree.GetCleanNode();
                //ApplyRule3(tree);
                package.Optimization3 = tree.GetCleanNode();
                //ApplyRule4(tree);
                package.Optimization4 = tree.GetCleanNode();
                //ApplyRule5(tree);
                package.Optimization5 = tree.GetCleanNode();
            }

            ApplyRule6(tree);
        }
        /// <summary>
        /// Breaks up conjunctive conditions of a selection into a cascade.
        /// </summary>
        private void ApplyRule1(Node root)
        {
            var selectionNodes = GetAllSelectionNodes(root);
            foreach (var node in selectionNodes)
            {
                if (!IsConjunctiveSelectionNode(node)) continue;

                var conditions = ((Selection) node.Content).Conditions;
                var iterator = node.Parent;
                var lastNode = node.LeftChild;
                for (var i = 0; i < conditions.Count; i++)
                {
                    var newNode = new Node
                    {
                        Content = new Selection(),
                        Parent = iterator
                    };
                    ((Selection)newNode.Content).Conditions.Add(conditions[i]);
                    newNode.Parent.LeftChild = newNode;
                    iterator = newNode;

                    if (i != conditions.Count - 1) continue;
                    newNode.LeftChild = lastNode;
                    lastNode.Parent = newNode;
                }
            }
        }
        /// <summary>
        /// Moves selections as close to their relations as possible.
        /// </summary>
        private void ApplyRule2(Node root)
        {
            var treeNodes = GetNodesList(root);
            var treeRelations = treeNodes.Where(n => n.Content is Relation);
            var selectionNodes = GetAllSelectionNodes(root);
            var joinNodes = selectionNodes.Where(n => IsJoinCondition((n.Content as Selection).Conditions.First()));
            if(joinNodes.Any())
            {
                var cartRankings = new Dictionary<Node, int>();
                foreach (var n in treeRelations)
                {
                    cartRankings.Add(n, 0);
                }
                foreach (var n in joinNodes)
                {
                    var relation1 = GetRelationForAttribute((n.Content as Selection).Conditions.First().LeftSide as Attribute, treeRelations);
                    var relation2 = GetRelationForAttribute((n.Content as Selection).Conditions.First().RightSide as Attribute, treeRelations);
                    cartRankings[relation1]++;
                    cartRankings[relation2]++;
                }
                var firstCart = GetFirstCartesian(root);
                var iter = firstCart;
                var orderedRelations = cartRankings.OrderBy(o => o.Value).Select(n => n.Key);
                for(int i = 0; i < orderedRelations.Count() - 1; i++)
                {
                    if (i == orderedRelations.Count() - 2)
                    {
                        var ele1 = orderedRelations.ElementAt(i);
                        var ele2 = orderedRelations.ElementAt(i+1);
                        iter.RightChild = ele1;
                        ele1.Parent = iter;
                        iter.LeftChild = ele2;
                        ele2.Parent = iter;
                    }
                    else
                    {
                        var ele = orderedRelations.ElementAt(i);
                        iter.RightChild = ele;
                        iter = iter.LeftChild;
                        ele.Parent = iter;
                    }
                }
            }

            // Bury the selection nodes within the query tree as deeply as possible.
            //foreach (var node in selectionNodes)
            //{
            //    var selection = node.Content as Selection;
            //    var condition = selection.Conditions.First();
            //    var isJoin = IsJoinCondition(condition);

            //    // Reposition surrounding nodes
            //    node.Parent.LeftChild = node.LeftChild;
            //    node.LeftChild.Parent = node.Parent;
            //    if (isJoin)
            //    {
            //        var relation1 = GetRelationForAttribute(condition.LeftSide as Attribute, treeRelations);
            //        var relation2 = GetRelationForAttribute(condition.RightSide as Attribute, treeRelations);
            //        var iter = node.LeftChild;
            //        while(!ContainsRelation(iter.LeftChild, relation1.Content as Relation) && 
            //            !ContainsRelation(iter.RightChild, relation2.Content as Relation))
            //        {
            //            iter = iter.LeftChild;
            //        }

            //        iter.Parent.LeftChild = node;
            //        node.Parent = iter.Parent;
            //        node.LeftChild = iter;
            //        iter.Parent = node;
            //    }
            //    else
            //    {
            //        var relation1 = GetRelationForAttribute(condition.LeftSide as Attribute, treeRelations);
            //        var relation2 = GetRelationForAttribute(condition.RightSide as Attribute, treeRelations);

            //        if(relation1 != null)
            //        {
            //            if (relation1.Parent.LeftChild == relation1)
            //            {
            //                relation1.Parent.LeftChild = node;
            //            }
            //            else
            //            {
            //                relation1.Parent.RightChild = node;
            //            }
            //            node.Parent = relation1.Parent;
            //            relation1.Parent = node;
            //            node.LeftChild = relation1;
            //        }
            //        else if(relation2 != null)
            //        {
            //            if (relation2.Parent.LeftChild == relation2)
            //            {
            //                relation2.Parent.LeftChild = node;
            //            }
            //            else
            //            {
            //                relation2.Parent.RightChild = node;
            //            }
            //            node.Parent = relation2.Parent;
            //            relation2.Parent = node;
            //            node.LeftChild = relation2;
            //        }
            //    }
            //}
        }
        /// <summary>
        /// Moves non-Join selection conditions to the left side of the tree.
        /// </summary>
        private void ApplyRule3(Node root)
        {
            var rootCart = root;
            // Find uppermost cartesian product node.
            while(!(rootCart.Content is SetOperator))
            {
                rootCart = rootCart.LeftChild;
            }
            var iter = rootCart;
            // Move heaviest selects to bottom left
            while(!(iter.Content is Relation))
            {
                if (iter.Content is SetOperator)
                {
                    if(iter.Parent.Content is Selection)
                    {
                        if(iter.LeftChild.Content is Selection && 
                           !(iter.LeftChild.LeftChild.Content is Selection) &&
                           !(iter.LeftChild.LeftChild.Content is Relation)) // It's another cartesian
                        {
                            var leftRank = GetRestrictiveWeight(iter.LeftChild);
                            var leftLeftRank = GetRestrictiveWeight(iter.LeftChild.LeftChild.LeftChild);
                            var leftRightRank = GetRestrictiveWeight(iter.LeftChild.LeftChild.RightChild);
                            var rightRank = GetRestrictiveWeight(iter.RightChild);

                            if (leftLeftRank < rightRank)
                            {
                                var select1 = iter.Parent;
                                var select1Parent = iter.Parent.Parent;
                                var select2 = iter.LeftChild;
                                var cart1 = iter;
                                var cart2 = iter.LeftChild.LeftChild;

                                cart1.LeftChild = cart1.RightChild;
                                cart1.RightChild = cart2.RightChild;
                                cart2.RightChild.Parent = cart1.RightChild;
                                select1.Parent = cart2;                                
                                cart2.RightChild = cart2.LeftChild;
                                cart2.LeftChild = select1;
                                select2.Parent = iter.Parent.Parent;
                            }
                        }
                        else // Might not handle full cartesians properly here.
                        {
                            SwapOnRank(iter);
                        }
                    }
                    else
                    {
                        SwapOnRank(iter);
                    }
                }
                iter = iter.LeftChild;
            }
        }

        private void SwapOnRank(Node node)
        {
            var leftRank = GetRestrictiveWeight(node.LeftChild);
            var rightRank = GetRestrictiveWeight(node.RightChild);
            if (leftRank < rightRank)
            {
                var temp = node.LeftChild;
                node.LeftChild = node.RightChild;
                node.RightChild = temp;
            }
        }

        private bool ContainsRelation(Node node, Relation relation)
        {
            if (node == null) return false;
            var result = false;

            if (node.Content is Relation &&
                    (node.Content as Relation == relation)) return true;

            if (node.LeftChild != null)
            {
                result = ContainsRelation(node.LeftChild, relation);
            }
            if (node.RightChild != null && !result)
            {
                result = ContainsRelation(node.RightChild, relation);
            }

            return result;
        }

        private Node GetFirstCartesian(Node node)
        {
            var iter = node;
            while(iter != null && !(iter.Content is SetOperator))
            {
                iter = iter.LeftChild;
            }
            return node;
        }

        /// <summary>
        /// Counts a nodes weight down to relation
        /// </summary>
        public static int GetRestrictiveWeight(Node node)
        {
            var count = 0;
            if (node.Content is SetOperator)
            {
                count += GetRestrictiveWeight(node.LeftChild);
                count += GetRestrictiveWeight(node.RightChild);
            }
            else if (node.Content is Selection)
            {
                var s = node.Content as Selection;
                // Add weight
                foreach (var con in s.Conditions)
                {
                    if (con.Operator == BooleanComparisonType.Equals ||
                       con.Operator == BooleanComparisonType.NotEqualToExclamation)
                    {
                        count += 1;
                    }
                    count += 1;
                }
                count += GetRestrictiveWeight(node.LeftChild);
            }
            else if (node.Content is Projection)
            {
                count += GetRestrictiveWeight(node.LeftChild);
            }
            else if (node.Content is Relation)
            {
                count = 0;
            }
            return count;
        }

        private Node GetAdjoiningSelect(Node r)
        {
            var node = r;
            while(!(node.Content is SetOperator))
            {
                node = node.Parent;
            }
            if(node.Parent.Content is Selection)
            {
                return node.Parent;
            }
            return node;
        }

        /// <summary>
        /// Replaces Cartesian products with join operator.
        /// </summary>
        private void ApplyRule4(Node root)
        {
            var cartesianNodes = GetAllCartesianProductNodes(root);

            foreach (var node in cartesianNodes)
            {
                var newNode = new Node()
                {
                    Content = new Join
                    {
                        Condition = ((Selection)node.Parent.Content).Conditions.First()
                    },
                    Parent = node.Parent.Parent,
                    LeftChild = node.LeftChild,
                    RightChild = node.RightChild
                };
                node.Parent.Parent.LeftChild = newNode;
                node.LeftChild.Parent = newNode;
                node.RightChild.Parent = newNode;
            }
        }
        /// <summary>
        /// Moves attribute projections as close as possible to their associated Relation.
        /// </summary>
        private void ApplyRule5(Node root)
        {
            var nodesInTree = GetNodesList(root);
            var treeRelations = nodesInTree.Where(n => n.Content is Relation).ToList();

            foreach (var node in treeRelations)
            {
                var iterator = node;
                while (iterator != root)
                {
                    if (iterator.Content is Projection)
                    {
                        iterator = iterator.Parent;
                        continue;
                    }
                    if (!(iterator.Parent.Content is Projection))
                    {
                        var allParentalAttributes = GetMinimumParentalAttributes(iterator, root);
                        var availableAttributes = GetAccessibleAttributes(iterator);

                        var intersection = new List<Attribute>();
                        foreach (var attribute in allParentalAttributes)
                        {
                            if (!(attribute is Attribute)) continue;
                            foreach (var availableAttribute in availableAttributes)
                            {
                                if (attribute.Name == availableAttribute.Name &&
                                    attribute.Alias == availableAttribute.Alias)
                                {
                                    intersection.Add(attribute);
                                }
                            }
                        }

                        var newNode = new Node
                        {
                            Content = new Projection
                            {
                                Attributes = new List<dynamic>(intersection)
                            },
                            Parent = iterator.Parent,
                            LeftChild = iterator
                        };

                        // Insert node projection node.
                        if (iterator.Parent.LeftChild == iterator)
                        {
                            iterator.Parent.LeftChild = newNode;
                        }
                        else
                        {
                            iterator.Parent.RightChild = newNode;
                        }
                        iterator.Parent = newNode;
                    }
                    iterator = iterator.Parent;
                }
            }
        }
        /// <summary>
        /// Identifies all subtrees that represent groups of operations that 
        /// can be executed by a single algorithm
        /// </summary>
        private void ApplyRule6(Node root)
        {
            var projectionNodes = GetAllProjectionNodes(root, root);

            var counter = 1;
            foreach (var node in projectionNodes)
            {
                GenerateGraph(node, "OP6subgraph" + counter);
                counter++;
            }
        }

        #region Graph Generation Methods

        /// <summary>
        /// Generates a Graphviz file for the given node.
        /// </summary>
        private void GenerateGraph(Node root, string nameSuffix)
        {
            // Gather the nodes and edges for the graph from the parse tree
            var nodes = GetNodesList(root);
            var edges = GetEdgesList(root);

            // Build the raw file contents
            var sb = new StringBuilder();
            sb.AppendLine("digraph G {");
            sb.AppendLine("\tnode [color=transparent]");
            sb.AppendLine("\tedge [dir=none]");
            foreach (var node in nodes)
            {
                sb.AppendLine("\t" + node.Id + " [label=\"" + node.GetContentString() + "\"]");
            }
            foreach (var edge in edges)
            {
                sb.AppendLine("\t" + edge);
            }
            sb.AppendLine("}");

            // Output the contents to a file
            const string outputFolder = @"Graphs\";
            var folderPath = Path.Combine(Environment.CurrentDirectory, outputFolder);
            var exists = Directory.Exists(folderPath);
            if (!exists)
                Directory.CreateDirectory(folderPath);

            using (var outfile = new StreamWriter(Path.Combine(folderPath, nameSuffix + ".gv")))
            {
                outfile.Write(sb.ToString());
            }
        }
        /// <summary>
        /// Generates a list of nodes for all children of the given node.
        /// </summary>
        private List<Node> GetNodesList(Node node)
        {
            var list = new List<Node> { node };

            if (node.LeftChild != null)
            {
                list.AddRange(GetNodesList(node.LeftChild));
            }
            if (node.RightChild != null)
            {
                list.AddRange(GetNodesList(node.RightChild));
            }

            return list;
        }
        /// <summary>
        /// Generates the Graphviz edges for the children of the given node.
        /// </summary>
        private IEnumerable<string> GetEdgesList(Node node)
        {
            var list = new List<string>();

            if (node.LeftChild != null)
            {
                list.Add(node.Id + "->" + node.LeftChild.Id + ";");
                list.AddRange(GetEdgesList(node.LeftChild));
            }
            if (node.RightChild != null)
            {
                list.Add(node.Id + "->" + node.RightChild.Id + ";");
                list.AddRange(GetEdgesList(node.RightChild));
            }

            return list;
        }

        #endregion

        #region Utility Methods for Node traversal

        /// <summary>
        /// Returns a list of attributes needed directly above the provided node.
        /// </summary>
        private List<dynamic> GetMinimumParentalAttributes(Node node, Node root)
        {
            var result = new List<dynamic>();

            var iterator = node.Parent;
            while (iterator != root)
            {
                if (iterator.Content is Selection)
                {
                    foreach (var condition in ((Selection)iterator.Content).Conditions)
                    {
                        foreach (var attribute in condition.GetSideAttributes())
                        {
                            result.Add(attribute);
                        }
                    }
                }
                else if (iterator.Content is Projection)
                {
                    result.AddRange(((Projection)iterator.Content).Attributes);
                    return result;
                }
                else if (iterator.Content is Join)
                {
                    var joinNode = (Join)iterator.Content;
                    result.Add(joinNode.Condition.LeftSide);
                    result.Add(joinNode.Condition.RightSide);
                }

                iterator = iterator.Parent;
            }

            return result;
        }
        /// <summary>
        /// Returns a list of attributes accessible from the current node.
        /// </summary>
        private List<dynamic> GetAccessibleAttributes(Node node)
        {
            var result = new List<dynamic>();

            if (node.Content is Projection)
            {
                result.AddRange(((Projection)node.Content).Attributes);
                return result;
            }
            if (node.Content is Relation)
            {
                var attributes = ((Relation) node.Content).Attributes;
                foreach (var att in attributes)
                {
                    att.Alias = ((Relation)node.Content).Aliases[att.QueryNumber];
                }
                result.AddRange(attributes);
                return result;
            }

            if (node.LeftChild != null)
            {
                result.AddRange(GetAccessibleAttributes(node.LeftChild));
            }
            if (node.RightChild != null)
            {
                result.AddRange(GetAccessibleAttributes(node.RightChild));
            }

            return result;
        }

        /// <summary>
        /// Determines if a Node with content of type Selection contains conjunctive conditions.
        /// </summary>
        private bool IsConjunctiveSelectionNode(Node node)
        {
            if (!(node.Content is Selection)) return false;

            return ((Selection)node.Content).Operators
                .All(conditionOperator => conditionOperator.Value != BooleanBinaryExpressionType.Or);
        }
        /// <summary>
        /// Determines if the given attribute is a primary key based on the set schema.
        /// </summary>
        private bool IsKey(Attribute attribute)
        {
            foreach (var rel in _schema.Relations)
            {
                foreach (var att in rel.Attributes)
                {
                    if (attribute.Name == att.Name && rel.PrimaryKey.Contains(att))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Finds the node of type Relation to which the given attribute refers.
        /// </summary>
        private Node GetRelationForAttribute(Attribute attribute, IEnumerable<Node> relations)
        {
            if (attribute == null) return null;
            if (relations.Count() == 1) return relations.First();
            foreach (var r in relations)
            {
                var relation = r.Content as Relation;
                if(relation.Aliases.Any())
                {
                    foreach (var att in relation.Attributes)
                    {
                        if (att.Name == attribute.Name && relation.Aliases.Contains(attribute.Alias))
                        {
                            return r;
                        }
                    }
                }
                else
                {
                    foreach (var att in relation.Attributes)
                    {
                        if (att.Name == attribute.Name && attribute.Alias == relation.Name)
                        {
                            return r;
                        }
                    }
                }

            }
            return null;
        }
        /// <summary>
        /// Determines if the given condition contains a comparison between two primary key attributes.
        /// </summary>
        private bool IsJoinCondition(Condition condition)
        {
            var attributes = condition.GetSideAttributes();

            if (attributes.Count != 2) return false;

            var attribute1IsKey = IsKey(attributes[0]);
            var attribute2IsKey = IsKey(attributes[1]);

            return attribute1IsKey && attribute2IsKey;
        }
        /// <summary>
        /// Determines if a list of conditions contains a condition involving two key attributes.
        /// </summary>
        public bool? ContainsJoinCondition(List<Condition> conditions)
        {
            // Assumes Optimization 1 has been applied, meaning all selections contain one condition
            if (conditions.Count != 1) return null;
            var result = false;

            foreach (var condition in conditions)
            {
                var attributes = condition.GetSideAttributes();

                if (attributes.Count != 2) return false;

                var attribute1IsKey = IsKey(attributes[0]);
                var attribute2IsKey = IsKey(attributes[1]);

                result = attribute1IsKey && attribute2IsKey;
            }

            return result;
        }
        /// <summary>
        /// Finds the nearest parent node of type Selection for the given node.
        /// </summary>
        public Node GetParentalJoinNode(Node node)
        {
            if (node == null) return null;
            var iterator = node.Parent;
            var nodeFound = false;
            do
            {
                while (!(iterator.Content is Selection))
                {
                    iterator = iterator.Parent;
                }

                if (ContainsJoinCondition(((Selection)iterator.Content).Conditions) ?? false)
                {
                    nodeFound = true;
                }
                else
                {
                    iterator = iterator.Parent;
                }
            } while (!nodeFound);
            return iterator;
        }
        /// <summary>
        /// Returns a list of all nodes whose contents are SetOperator.CartesianProduct.
        /// </summary>
        public List<Node> GetAllCartesianProductNodes(Node node)
        {
            var nodes = new List<Node>();

            if (node.Content is SetOperator && 
                ((SetOperator) node.Content) == SetOperator.CartesianProduct)
            {
                nodes.Add(node);
            }
            if(node.LeftChild != null)
                nodes.AddRange(GetAllCartesianProductNodes(node.LeftChild));
            if(node.RightChild != null)
                nodes.AddRange(GetAllCartesianProductNodes(node.RightChild));

            return nodes;
        }
        /// <summary>
        /// Returns a list of all nodes whose contents are of type Selection.
        /// </summary>
        public List<Node> GetAllSelectionNodes(Node node)
        {
            var nodes = new List<Node>();

            if (node.Content is Selection)
            {
                nodes.Add(node);
            }
            if (node.LeftChild != null)
                nodes.AddRange(GetAllSelectionNodes(node.LeftChild));
            if (node.RightChild != null)
                nodes.AddRange(GetAllSelectionNodes(node.RightChild));

            return nodes;
        }
        /// <summary>
        /// Returns a list of all nodes whose contents are of type projection 
        /// except the root node.
        /// </summary>
        public List<Node> GetAllProjectionNodes(Node node, Node root)
        {
            var nodes = new List<Node>();

            if (node.Content is Projection && node != root)
            {
                nodes.Add(node);
            }
            if (node.LeftChild != null)
                nodes.AddRange(GetAllProjectionNodes(node.LeftChild, root));
            if (node.RightChild != null)
                nodes.AddRange(GetAllProjectionNodes(node.RightChild, root));

            return nodes;
        }

        #endregion
    }
}

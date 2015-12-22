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
        public void Optimize(QoPackage package)
        {
            var tree = package.Tree;            
            try
            { 
                if (tree.Content is SetOperator)
                {
                    ApplyRule1(tree.LeftChild);
                    ApplyRule1(tree.RightChild);
                    package.Optimization1 = tree.GetCleanNode();
                    ApplyRule2(tree.LeftChild);
                    ApplyRule2(tree.RightChild);
                    package.Optimization2 = tree.GetCleanNode();
                    ApplyRule3(tree.LeftChild);
                    ApplyRule3(tree.RightChild);
                    package.Optimization3 = tree.GetCleanNode();
                    ApplyRule4(tree.LeftChild);
                    ApplyRule4(tree.RightChild);
                    package.Optimization4 = tree.GetCleanNode();
                    ApplyRule5(tree.LeftChild);
                    ApplyRule5(tree.RightChild);
                    package.Optimization5 = tree.GetCleanNode();
                }
                else
                {
                    ApplyRule1(tree);
                    package.Optimization1 = tree.GetCleanNode();
                    tree = ApplyRule2(tree);
                    package.Optimization2 = tree.GetCleanNode();
                    ApplyRule3(tree);
                    package.Optimization3 = tree.GetCleanNode();
                    ApplyRule4(tree);
                    package.Optimization4 = tree.GetCleanNode();
                    ApplyRule5(tree);
                    package.Optimization5 = tree.GetCleanNode();
                }

                ApplyRule6(tree);
            }
            catch (Exception e)
            {
                package.Error = e.Message;
            }
        }
        /// <summary>
        /// Breaks up conjunctive conditions of a selection into a cascade.
        /// </summary>
        private void ApplyRule1(Node root)
        {
            var selectionNodes = GetAllSelectionNodes(root);
            foreach (var node in selectionNodes)
            {
                if (((Selection)node.Content).Conditions.Count <= 1 || !IsConjunctiveSelectionNode(node)) continue;

                var conditions = ((Selection) node.Content).Conditions;
                var iterator = node.Parent ?? root;
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
        private Node ApplyRule2(Node root)
        {
            if(root.Content is SetOperator)
            {
                ApplyRule2(root.LeftChild);
                ApplyRule2(root.RightChild);
                return root;
            }
            var treeNodes = GetNodesList(root);
            var treeRelations = treeNodes.Where(n => n.Content is Relation);
            var selectionNodes = GetAllSelectionNodes(root);
            if (!selectionNodes.Any()) return root;
            if (selectionNodes.All(s => !(s.Content as Selection).Conditions.Any())) return root;

            // Remove duplicate selections
            var duplicates = new List<Tuple<Node,Node>>();
            if (selectionNodes.Count > 1)
            {
                for (var i = 0; i < selectionNodes.Count; i++)
                {
                    var conditions1 = ((Selection)selectionNodes[i].Content).Conditions;
                    for (var j = i + 1; j < selectionNodes.Count; j++)
                    {
                        var conditions2 = ((Selection)selectionNodes[j].Content).Conditions;
                        foreach(var c1 in conditions1)
                        {
                            foreach(var c2 in conditions2)
                            {
                                var lefts = c1.LeftSide.ToString() == c2.LeftSide.ToString();
                                var rights = c1.RightSide.ToString() == c2.RightSide.ToString();
                                var lefts2 = c1.LeftSide.ToString() == c2.RightSide.ToString();
                                var rights2 = c1.RightSide.ToString() == c2.LeftSide.ToString();
                                if ((lefts && rights) || (lefts2 && rights2))
                                {
                                    var tuple = new Tuple<Node, Node>(selectionNodes[i], selectionNodes[j]);
                                    if (!duplicates.Contains(tuple)) duplicates.Add(tuple);
                                }
                            }
                        }
                    }
                }
            }
            if(duplicates.Any())
            {
                foreach(var t in duplicates)
                {
                    if(t.Item1 == root || t.Item2 == root)
                    {
                        root.LeftChild.LeftChild = null; // not really necessary
                        root.LeftChild.Parent = null;
                        selectionNodes.Remove(root);
                        root = root.LeftChild;
                    }
                    else // So it doesn't really matter which node is removed in this case so I'm just picking the first one
                    {
                        t.Item1.LeftChild.Parent = t.Item1.Parent;
                        t.Item1.Parent = t.Item1.LeftChild;
                        selectionNodes.Remove(t.Item1);
                    }
                }
            }

            var joinNodes = selectionNodes.Where(n => IsJoinCondition((n.Content as Selection).Conditions.First()));
            
            // Move relations with more selects to the very far left.
            // This prevents cases where cartesian products do not have an immediate selection as their parent.
            if (joinNodes.Any())
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
                var firstValue = cartRankings.First().Value;
                if (!cartRankings.All(c => c.Value == firstValue)) // If they all have the same value, don't do anything.
                {
                    var firstCart = GetFirstCartesian(root);
                    var iter = firstCart;
                    var orderedRelations = cartRankings.OrderBy(o => o.Value).Select(n => n.Key);
                    for (int i = 0; i < orderedRelations.Count() - 1; i++)
                    {
                        if (i == orderedRelations.Count() - 2)
                        {
                            var ele1 = orderedRelations.ElementAt(i);
                            var ele2 = orderedRelations.ElementAt(i + 1);
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
            }

            // Bury the selection nodes within the query tree as deeply as possible.
            foreach (var node in selectionNodes)
            {
                if (node == root) continue;
                var selection = node.Content as Selection;
                var condition = selection.Conditions.First();
                var isJoin = IsJoinCondition(condition);

                // Reposition surrounding nodes
                node.Parent.LeftChild = node.LeftChild;
                node.LeftChild.Parent = node.Parent;
                if (isJoin)
                {
                    var relation1 = GetRelationForAttribute(condition.LeftSide as Attribute, treeRelations);
                    var relation2 = GetRelationForAttribute(condition.RightSide as Attribute, treeRelations);
                    var iter = node.LeftChild;
                    var leftHasRelation1 = ContainsRelation(iter.LeftChild, relation1.Content as Relation);
                    var leftHasRelation2 = ContainsRelation(iter.LeftChild, relation2.Content as Relation);
                    var rightHasRelation1 = ContainsRelation(iter.RightChild, relation1.Content as Relation);
                    var rightHasRelation2 = ContainsRelation(iter.RightChild, relation2.Content as Relation);
                    while (!(leftHasRelation1 && rightHasRelation2) && !(leftHasRelation2 && rightHasRelation1))
                    {
                        iter = iter.LeftChild;
                        leftHasRelation1 = ContainsRelation(iter.LeftChild, relation1.Content as Relation);
                        leftHasRelation2 = ContainsRelation(iter.LeftChild, relation2.Content as Relation);
                        rightHasRelation1 = ContainsRelation(iter.RightChild, relation1.Content as Relation);
                        rightHasRelation2 = ContainsRelation(iter.RightChild, relation2.Content as Relation);
                    }

                    iter.Parent.LeftChild = node;
                    node.Parent = iter.Parent;
                    node.LeftChild = iter;
                    iter.Parent = node;
                }
                else
                {
                    var relation1 = GetRelationForAttribute(condition.LeftSide as Attribute, treeRelations);
                    var relation2 = GetRelationForAttribute(condition.RightSide as Attribute, treeRelations);

                    if (relation1 != null)
                    {
                        if (relation1.Parent.LeftChild == relation1)
                        {
                            InsertNodeAboveLeftChild(relation1, node);
                        }
                        else
                        {
                            InsertNodeAboveRightChild(relation1, node);
                        }
                    }
                    else if (relation2 != null)
                    {
                        if (relation2.Parent.LeftChild == relation2)
                        {
                            InsertNodeAboveLeftChild(relation2, node);
                        }
                        else
                        {
                            InsertNodeAboveRightChild(relation2, node);
                        }
                    }
                }
            }
            return root;
        }
        /// <summary>
        /// Moves non-Join selection conditions to the left side of the tree.
        /// </summary>
        private void ApplyRule3(Node root)
        {
            if(root.Content is SetOperator && (SetOperator)root.Content != SetOperator.CartesianProduct)
            {
                ApplyRule3(root.LeftChild);
                ApplyRule3(root.RightChild);
                return;
            }
            var rootCart = root;
            // Find uppermost cartesian product node.
            while(!(rootCart.LeftChild != null) && !(rootCart.Content is SetOperator))
            {
                rootCart = rootCart.LeftChild;
            }
            if (rootCart.LeftChild == null) return;
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
        /// <summary>
        /// Replaces Cartesian products with join operator.
        /// </summary>
        private void ApplyRule4(Node root)
        {
            var cartesianNodes = GetAllCartesianProductNodes(root);

            foreach (var node in cartesianNodes)
            {
                if(!(node.Parent.Content is Selection)) continue;

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
            
        }

        #region Graph Generation Methods
        
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
        public static Node GetRelationForAttribute(Attribute attribute, IEnumerable<Node> relations)
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
        public static List<Node> GetAllCartesianProductNodes(Node node)
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
        /// Returns a list of all nodes whose contents are a Relation.
        /// </summary>
        public static List<Node> GetAllRelationNodes(Node node)
        {
            var nodes = new List<Node>();

            if (node.Content is Relation)
            {
                nodes.Add(node);
            }
            if (node.LeftChild != null)
                nodes.AddRange(GetAllCartesianProductNodes(node.LeftChild));
            if (node.RightChild != null)
                nodes.AddRange(GetAllCartesianProductNodes(node.RightChild));

            return nodes;
        }
        /// <summary>
        /// Returns a list of all nodes whose contents are of type Selection.
        /// </summary>
        public static List<Node> GetAllSelectionNodes(Node node)
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
        public static List<Node> GetAllProjectionNodes(Node node, Node root)
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
        /// <summary>
        /// Returns a list of all nodes whose contents are of type projection 
        /// including the root.
        /// </summary>
        public static List<Node> GetAllProjectionNodes(Node root)
        {
            var nodes = new List<Node>();

            if (root.Content is Projection)
            {
                nodes.Add(root);
            }
            if (root.LeftChild != null)
                nodes.AddRange(GetAllProjectionNodes(root.LeftChild));
            if (root.RightChild != null)
                nodes.AddRange(GetAllProjectionNodes(root.RightChild));

            return nodes;
        }
        /// <summary>
        /// Determines the distance a node is from a given root node.
        /// This will cause a runtime issue if the given root is not an eventual parent of the given target.
        /// </summary>
        public int GetDepth(Node root, Node target)
        {
            var iter = target;
            var depth = 0;

            while(iter != root)
            {
                iter = iter.Parent;
                depth++;
            }

            return depth;
        }
        /// <summary>
        /// Moves the more selective child of the given node to be the left child.
        /// </summary>
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
        /// <summary>
        /// Determines if the given relation is an eventual child of the given node.
        /// </summary>
        private bool ContainsRelation(Node node, Relation relation)
        {
            if (node == null) return false;
            var result = false;

            if (node.Content is Relation && node.Content as Relation == relation) return true;

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
        /// <summary>
        /// Returns the first node containing a cartesian product.
        /// </summary>
        private Node GetFirstCartesian(Node node)
        {
            var iter = node;
            while (iter != null && !(iter.Content is SetOperator))
            {
                iter = iter.LeftChild;
            }
            return iter;
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
        /// <summary>
        /// Finds the nearest parent selection node and returns it.
        /// </summary>
        private Node GetAdjoiningSelect(Node r)
        {
            var node = r;
            while (!(node.Content is SetOperator))
            {
                node = node.Parent;
            }
            if (node.Parent.Content is Selection)
            {
                return node.Parent;
            }
            return node;
        }
        /// <summary>
        /// Places a node such that is becomes the new parent of a given target node
        /// while maintaining the connections of the location the node is coming from.
        /// </summary>
        private void InsertNodeAboveRightChild(Node location, Node node)
        {
            node.LeftChild = location;
            node.Parent = location.Parent;

            node.Parent.RightChild = node;
            node.LeftChild.Parent = node;
        }
        /// <summary>
        /// Places a node such that is becomes the new parent of a given target node
        /// while maintaining the connections of the location the node is coming from.
        /// </summary>
        private void InsertNodeAboveLeftChild(Node location, Node node)
        {
            node.LeftChild = location;
            node.Parent = location.Parent;

            node.Parent.LeftChild = node;
            node.LeftChild.Parent = node;
        }
        /// <summary>
        /// Verifies that every single node descending from the given root is proper, i.e.,
        /// each descendent's parent and children pointers are properly set.
        /// </summary>
        public static bool DescendentsAreProper(Node root)
        {
            var isValid = true;
            if(root.LeftChild != null)
            {
                if (root.LeftChild.Parent != root)
                    return false;
                else
                    isValid = DescendentsAreProper(root.LeftChild);
            }
            if(root.RightChild != null)
            {
                if (root.RightChild.Parent != root)
                    return false;
                else
                    isValid = DescendentsAreProper(root.LeftChild);
            }
            return isValid;
        }
    }
}

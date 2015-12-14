namespace Qo.Parsing
{
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
    public class QueryOptimizer
    {
        private readonly Schema _schema;
        private string _name;

        public QueryOptimizer(Schema schema)
        {
            _schema = schema;
        }

        /// <summary>
        /// Applies all query optimization rules on a query tree 
        /// and outputs the Graphviz files after each optimization.
        /// </summary>
        public void Run(Node queryTree, string name)
        {
            _name = name;
            GenerateGraph(queryTree, name + "Op0");
            if (queryTree.Content is SetOperator)
            {
                ApplyOptimizationRule1(queryTree.LeftChild);
                ApplyOptimizationRule1(queryTree.RightChild);
                GenerateGraph(queryTree, name + "Op1");

                ApplyOptimizationRule2(queryTree.LeftChild);
                ApplyOptimizationRule2(queryTree.RightChild);
                GenerateGraph(queryTree, name + "Op2");

                ApplyOptimizationRule3(queryTree.LeftChild);
                ApplyOptimizationRule3(queryTree.RightChild);
                GenerateGraph(queryTree, name + "Op3");

                ApplyOptimizationRule4(queryTree.LeftChild);
                ApplyOptimizationRule4(queryTree.RightChild);
                GenerateGraph(queryTree, name + "Op4");

                ApplyOptimizationRule5(queryTree.LeftChild);
                ApplyOptimizationRule5(queryTree.RightChild);
                GenerateGraph(queryTree, name + "Op5");
            }
            else
            {
                ApplyOptimizationRule1(queryTree);
                GenerateGraph(queryTree, name + "Op1");
                ApplyOptimizationRule2(queryTree);
                GenerateGraph(queryTree, name + "Op2");
                ApplyOptimizationRule3(queryTree);
                GenerateGraph(queryTree, name + "Op3");
                ApplyOptimizationRule4(queryTree);
                GenerateGraph(queryTree, name + "Op4");
                ApplyOptimizationRule5(queryTree);
                GenerateGraph(queryTree, name + "Op5");
            }

            ApplyOptimizationRule6(queryTree);
        }
        /// <summary>
        /// Breaks up conjunctive conditions of a selection into a cascade.
        /// </summary>
        private void ApplyOptimizationRule1(Node root)
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
        /// Moves selections close to their relations as possible.
        /// </summary>
        private void ApplyOptimizationRule2(Node root)
        {
            var treeNodes = GetNodesList(root);
            var treeRelations = treeNodes.Where(n => n.Content is Relation).ToList();
            var selectionNodes = GetAllSelectionNodes(root);

            // Bury the selection nodes within the query tree as deeply as possible.
            foreach (var node in selectionNodes)
            {
                var condition = ((Selection) node.Content).Conditions.First();
                var leftRelation = (Node)GetRelationForAttribute(condition.LeftSide, treeRelations);
                if (leftRelation == null) continue;
                var isJoinCondition = IsJoinCondition(condition);

                // Reposition surrounding nodes
                node.Parent.LeftChild = node.LeftChild;
                node.LeftChild.Parent = node.Parent;
                if (isJoinCondition)
                {
                    var rightRelation = (Node)GetRelationForAttribute(condition.RightSide, treeRelations);

                    // Resposition node
                    node.Parent = rightRelation.Parent.Parent;
                    node.LeftChild = rightRelation.Parent;

                    while (node.LeftChild.RightChild != rightRelation &&
                            node.LeftChild.RightChild != leftRelation)
                    {
                        node.Parent = node.Parent.Parent;
                        node.LeftChild = node.Parent.LeftChild;
                    }
                    node.Parent.LeftChild = node;
                    node.LeftChild.Parent = node;
                    continue;
                }

                // Reposition node
                node.Parent = leftRelation.Parent;
                node.LeftChild = leftRelation;

                if (node.Parent.LeftChild == leftRelation)
                {
                    node.Parent.LeftChild = node;
                }
                else
                {
                    node.Parent.RightChild = node;
                }

                node.LeftChild.Parent = node;
            }
        }
        /// <summary>
        /// Moves non-Join selection conditions to the left side of the tree.
        /// </summary>
        private void ApplyOptimizationRule3(Node root)
        {
            var nodesInTree = GetNodesList(root);
            var treeRelations = nodesInTree.Where(n => n.Content is Relation).ToList();
            var treeSelections = nodesInTree.Where(n => n.Content is Selection).ToList();

            foreach (var node in treeSelections)
            {
                var containsJoinCondition = ContainsJoinCondition(((Selection)node.Content).Conditions);
                if (containsJoinCondition == null || containsJoinCondition == true) continue;
                if (GetAllCartesianProductNodes(root).Count == 1)
                {
                    if (!(node.Parent.Content is SetOperator)) continue;

                    var cartesianNode = node.Parent;

                    if (cartesianNode.RightChild != node) continue;

                    cartesianNode.RightChild = cartesianNode.LeftChild;
                    cartesianNode.LeftChild = node;
                    ((Selection)cartesianNode.Parent.Content).SwapOperators();
                }
                else
                {
                    var joinCondition = ((Selection)node.Parent.Parent.Content).Conditions.First();
                    var leftRelationNode = (Node)GetRelationForAttribute(joinCondition.LeftSide, treeRelations);
                    var rightRelationNode = (Node)GetRelationForAttribute(joinCondition.RightSide, treeRelations);
                    var leftJoinNode = GetParentalJoinNode(leftRelationNode);
                    var rightJoinNode = GetParentalJoinNode(rightRelationNode);

                    // Point the parent of the right relation join node at the left relation's join node
                    rightJoinNode.Parent.LeftChild = leftJoinNode;
                    leftJoinNode.Parent = rightJoinNode.Parent;
                    rightJoinNode.LeftChild.LeftChild = rightJoinNode.LeftChild.RightChild;
                    rightJoinNode.LeftChild.RightChild = leftRelationNode;
                    leftRelationNode.Parent = rightJoinNode.LeftChild;
                    leftJoinNode.LeftChild.RightChild = leftJoinNode.LeftChild.LeftChild;
                    leftJoinNode.LeftChild.LeftChild = rightJoinNode;
                    rightJoinNode.Parent = leftJoinNode.LeftChild;

                    // Swap attributes join conditions
                    ((Selection)rightJoinNode.Content).SwapOperators();
                    ((Selection)leftJoinNode.Content).SwapOperators();
                }
            }
        }
        /// <summary>
        /// Replaces Cartesian products with join operator.
        /// </summary>
        private void ApplyOptimizationRule4(Node root)
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
        private void ApplyOptimizationRule5(Node root)
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
                                Attributes = new List<Attribute>(intersection)
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
        private void ApplyOptimizationRule6(Node root)
        {
            var projectionNodes = GetAllProjectionNodes(root, root);

            var counter = 1;
            foreach (var node in projectionNodes)
            {
                GenerateGraph(node, _name + "OP6subgraph" + counter);
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
        private List<Attribute> GetMinimumParentalAttributes(Node node, Node root)
        {
            var result = new List<Attribute>();

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
        private List<Attribute> GetAccessibleAttributes(Node node)
        {
            var result = new List<Attribute>();

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
                .All(conditionOperator => conditionOperator.Value != LogicalOperator.Or);
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
            foreach (var relation in relations)
            {
                if (((Relation)relation.Content).Aliases[attribute.QueryNumber] == attribute.Alias)
                {
                    foreach (var relationAttribute in ((Relation)relation.Content).Attributes)
                    {
                        if (relationAttribute.Name == attribute.Name)
                        {
                            return relation;
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

namespace Qo.Parsing
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using QueryModel;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using TSql = Microsoft.SqlServer.TransactSql.ScriptDom;

    public class QoParser
    {
        private readonly IConsole _console;
        private readonly Schema _schema;
        private readonly List<string> _setOperators = new List<string>
        {
            "union",
            "intersect",
            "except"
        };
        
        public QoParser()
        {
            _console = new Console();
            _schema = Resources.Schemas.GetSchema1();
        }
        public QoParser(Schema schema)
        {
            _console = new Console();
            _schema = schema;
        }
        public QoParser(IConsole console, Schema schema)
        {
            _console = console;
            _schema = schema;
        }

        public QoPackage Parse(string query)
        {
            // Sanitize original query string
            query = query.Replace("’", "'").Replace("`", "'").Replace("‘", "'");
            var package = new QoPackage();
            try
            {
                IList<ParseError> errors;
                var reader = new StringReader(query);
                var parser = new TSql120Parser(false);
                var script = parser.Parse(reader, out errors) as TSqlScript;

                if (errors.Any())
                {
                    var sb = new StringBuilder();
                    foreach (var e in errors)
                    {
                        sb.AppendLine(e.Message);
                    }
                    package.Error = sb.ToString();
                }
                else
                {
                    var batch = script.Batches.First();
                    var statement = batch.Statements.First();
                    var result = ProcessStatement(statement);
                    var schemaErrors = new List<string>();
                    var isValid = QueryIsValid(result, schemaErrors);

                    if (isValid)
                    {
                        if (result is Query)
                        {
                            var unboxedResult = result as Query;
                            package.Tree = unboxedResult.GetQueryTree();
                            package.RelationalAlgebra = package.Tree.ToString();
                            package.InitialTree = package.Tree.GetCleanNode();
                        }
                        if (result is MultiQuery)
                        {
                            var unboxedResult = result as MultiQuery;
                            package.Tree = unboxedResult.GetQueryTree();
                            package.RelationalAlgebra = package.Tree.ToString();
                            package.InitialTree = package.Tree.GetCleanNode();
                        }
                        package.ParseSuccess = true;
                    }
                    else
                    {
                        var sb = new StringBuilder();
                        foreach (var e in schemaErrors)
                        {
                            sb.AppendLine(e);
                        }
                        package.Error = sb.ToString();
                    }
                }
            }
            catch (Exception e)
            {
                package.Error = e.InnerException == null ? e.Message : e.InnerException.Message;
            }
            return package;
        }
        
        #region TSqlParser Methods

        public dynamic ProcessStatement(TSqlStatement statement)
        {
            // Could be Stored Procedure, While, or If but for this project, we're not expecting those.
            // if(statement is TSql.WhileStatement)
            //     ProcessStatements(((TSql.WhileStatement)statement).Statement);
            if (statement is TSql.SelectStatement)
                return ProcessQueryExpression(((TSql.SelectStatement)statement).QueryExpression);
            return null;
        }

        public dynamic ProcessQueryExpression(QueryExpression exp)
        {            
            if (exp is QuerySpecification) // Actual SELECT Statement
            {
                return ProcessQuerySpecification(exp as QuerySpecification);
            }
            else if (exp is BinaryQueryExpression) // UNION, INTERSECT, EXCEPT
            {
                return ProcessBinaryQueryExpression(exp as BinaryQueryExpression);
            }
            else if (exp is QueryParenthesisExpression) // SELECT statement surrounded by paranthesis
            {
                var par = exp as QueryParenthesisExpression;
                return ProcessQueryExpression(par.QueryExpression);
            }
            else
            {
                throw new Exception("QueryExpression type could not be identified.");
            }
        }

        public dynamic ProcessQuerySpecification(QuerySpecification spec)
        {
            var query = new Query();

            #region SELECT

            foreach (SelectScalarExpression exp in spec.SelectElements)
            {
                var att = ProcessSelectScalarExpression(exp);
                if(exp.ColumnName != null)
                {
                    if (att is Function) ((Function)att).Alias = exp.ColumnName.Value;
                }
                query.Select.Attributes.Add(att);
            }

            #endregion
            #region FROM 

            foreach (NamedTableReference t in spec.FromClause.TableReferences)
            {
                var tableName = t.SchemaObject.BaseIdentifier.Value;
                var schemaRelation = _schema.Relations.SingleOrDefault(r => r.Name == tableName);
                if (schemaRelation == null)
                {
                    var relation = new Relation();
                    relation.Name = tableName;
                    query.From.Relations.Add(relation);
                }
                else
                {
                    var copy = new Relation(schemaRelation);
                    query.From.Relations.Add(copy);
                    if (t.Alias != null)
                    {
                        if (!schemaRelation.Aliases.Contains(t.Alias.ToString()))
                        {
                            schemaRelation.Aliases.Add(t.Alias.Value);
                        }
                        copy.Aliases.Add(t.Alias.Value);
                    }
                }
            }

            #endregion
            #region WHERE
            if (spec.WhereClause != null)
            {

                var whereClauseExpression = spec.WhereClause.SearchCondition;
                if (whereClauseExpression is BooleanBinaryExpression)
                {
                    #region BooleanBinary Expression

                    var whereClause = whereClauseExpression as BooleanBinaryExpression;
                    var tuple = ProcessBooleanBinaryExpression(whereClause);
                    query.Where = new WhereStatement
                    {
                        Conditions = tuple.Item1,
                        Operators = tuple.Item2,
                    };

                    #endregion
                }
                else if (whereClauseExpression is BooleanComparisonExpression)
                {
                    #region Comparison Expression

                    var whereClause = whereClauseExpression as BooleanComparisonExpression;

                    if (whereClause.FirstExpression is ColumnReferenceExpression &&
                        whereClause.SecondExpression is ColumnReferenceExpression)
                    {
                        var condition = ProcessBooleanComparisonExpression(whereClause);
                        query.Where.Conditions.Add(condition);
                    }
                    else if (whereClause.FirstExpression is ColumnReferenceExpression &&
                        whereClause.SecondExpression is IntegerLiteral)
                    {
                        var condition = ProcessBooleanComparisonExpression(whereClause);
                        query.Where.Conditions.Add(condition);
                    }
                    else if (whereClause.FirstExpression is ColumnReferenceExpression &&
                        whereClause.SecondExpression is StringLiteral)
                    {
                        var condition = ProcessBooleanComparisonExpression(whereClause);
                        query.Where.Conditions.Add(condition);
                    }
                    else if (whereClause.FirstExpression is ColumnReferenceExpression &&
                            whereClause.SecondExpression is ScalarSubquery)
                    {
                        var multiQuery = new MultiQuery();
                        var leftExp = whereClause.FirstExpression as ColumnReferenceExpression;
                        var rightExp = whereClause.SecondExpression as ScalarSubquery;
                        var att = ProcessColumnReferenceExpression(leftExp) as QueryModel.Attribute;
                        dynamic rightQueryExp = ProcessQueryExpression(rightExp.QueryExpression);

                        if (rightQueryExp is Query)
                        {
                            var rightQuery = rightQueryExp as Query;
                            // Add comparison to where clause of subquery 
                            ModifyQueryDueToComparison(rightQuery, att, whereClause.ComparisonType);
                            rightQuery.Select.Attributes.Clear();
                            rightQuery.Select.Attributes.Add(query.Select.Attributes.First());
                            // Build multi-query
                            multiQuery.Queries.Add(query);
                            multiQuery.Queries.Add(rightQuery);
                            var tuple = new Tuple<dynamic, dynamic>(query, rightQuery);
                            multiQuery.Operators.Add(tuple, SetOperator.Division);
                            // Adjust for NOT EXISTS
                            var neMultiQuery = new MultiQuery();
                            var neTuple = new Tuple<dynamic, dynamic>(query, multiQuery);
                            neMultiQuery.Operators.Add(tuple, SetOperator.Except);
                            neMultiQuery.Queries.Add(query);
                            neMultiQuery.Queries.Add(multiQuery);
                            return neMultiQuery;
                        }
                        else if (rightQueryExp is MultiQuery)
                        {
                            var rightMultiQuery = rightQueryExp as MultiQuery;
                            // Add comparison to where clause of left subquery
                            ModifyQueryDueToComparison(rightMultiQuery.Queries[0], att, whereClause.ComparisonType);
                            // Add comparison to where clause of right subquery
                            ModifyQueryDueToComparison(rightMultiQuery.Queries[1], att, whereClause.ComparisonType);
                            var right = rightMultiQuery.Queries[1] as Query;
                            right.Select.Attributes.Clear();
                            right.Select.Attributes.Add(query.Select.Attributes.First());
                            // Build/return multi-query
                            multiQuery.Queries.Add(query);
                            multiQuery.Queries.Add(rightMultiQuery);
                            var tuple = new Tuple<dynamic, dynamic>(query, rightMultiQuery);
                            multiQuery.Operators.Add(tuple, SetOperator.Division);
                            // Adjust for NOT EXISTS
                            var neMultiQuery = new MultiQuery();
                            var neTuple = new Tuple<dynamic, dynamic>(query, multiQuery);
                            neMultiQuery.Operators.Add(neTuple, SetOperator.Except);
                            neMultiQuery.Queries.Add(query);
                            neMultiQuery.Queries.Add(multiQuery);
                            return neMultiQuery;
                        }
                    }
                    else
                    {
                        throw new NotImplementedException("Where clause condition type not found.");
                    }

                    #endregion
                }
                else if (whereClauseExpression is InPredicate)
                {
                    #region IN Predicate

                    MultiQuery rightMultiQuery;
                    dynamic rightQueryExp;
                    var whereClause = whereClauseExpression as InPredicate;
                    var expression = whereClause.Expression as ColumnReferenceExpression;
                    var subQuery = whereClause.Subquery as ScalarSubquery;
                    var att = ProcessColumnReferenceExpression(expression) as QueryModel.Attribute;
                    var multiQuery = new MultiQuery();
                    multiQuery.Queries.Add(query);

                    rightQueryExp = ProcessQueryExpression(subQuery.QueryExpression);

                    if (rightQueryExp is Query)
                    {
                        var rightQuery = rightQueryExp as Query;
                        // Add comparison to where clause of subquery 
                        ModifyQueryDueToIn(rightQuery, att, query.Select.Attributes.First());
                        rightQuery.From.Relations.Add(query.From.Relations.First()); // This may not be enough
                                                                                     // Build/return multi-query
                        multiQuery.Queries.Add(rightQuery);
                        var tuple = new Tuple<dynamic, dynamic>(query, rightQuery);
                        multiQuery.Operators.Add(tuple, SetOperator.Division);
                        return multiQuery;
                    }
                    else if (rightQueryExp is MultiQuery)
                    {
                        rightMultiQuery = rightQueryExp as MultiQuery;
                        var query1 = rightMultiQuery.Queries[0] as Query;
                        var query2 = rightMultiQuery.Queries[1] as Query;

                        // Add comparison to where clause of left subquery
                        ModifyQueryDueToIn(query1, att, query.Select.Attributes.First());
                        query1.From.Relations.Insert(0, query.From.Relations.First()); 
                        
                        ModifyQueryDueToIn(query2, att, query.Select.Attributes.First());
                        query2.From.Relations.Insert(0, query.From.Relations.First()); 

                        multiQuery.Queries.Add(rightMultiQuery);
                        var tuple = new Tuple<dynamic, dynamic>(query, rightMultiQuery);
                        multiQuery.Operators.Add(tuple, SetOperator.Division);
                        return multiQuery;
                    }

                    #endregion
                }
                else if(whereClauseExpression is BooleanNotExpression)
                {
                    var expression = (whereClauseExpression as BooleanNotExpression).Expression;
                    if(expression is ExistsPredicate)
                    {
                        var multiQuery = new MultiQuery();
                        var subExp = expression as ExistsPredicate;
                        var subQuery = ProcessScalarExpression(subExp.Subquery);

                        if(subQuery is Query)
                        {
                            var q = subQuery as Query;
                            foreach(var r in _schema.Relations)
                            {
                                if(!q.From.Relations.Select(n => n.Name).Contains(r.Name))
                                {
                                    var relation = new Relation(r);
                                    relation.Aliases.Add(r.Name.Substring(0,1));
                                    q.From.Relations.Add(relation);
                                }
                            }
                            q.RemoveRedundantRelations();
                        }
                        else if(subQuery is MultiQuery)
                        {
                            var q = subQuery as MultiQuery;
                            var q1 = q.Queries[0] as Query;
                            var q1FirstSelect = q1.Select.Attributes.First() as QueryModel.Attribute;

                            var newQuery = new Query();
                            newQuery.Select = query.Select;
                            var r1 = query.From.Relations.First() as Relation;
                            var att1 = r1.PrimaryKey.First();
                            var condition = new Condition();
                            condition.LeftSide = att1;
                            condition.RightSide = q1FirstSelect;
                            condition.Operator = BooleanComparisonType.Equals;
                            newQuery.Where.Conditions.Add(condition);
                            newQuery.From.Relations.Add(r1);
                            newQuery.From.Relations.Add(subQuery);
                            subQuery = newQuery;
                        }

                        var tuple = new Tuple<dynamic, dynamic>(query, subQuery);
                        multiQuery.Queries.Add(query);
                        multiQuery.Queries.Add(subQuery);
                        multiQuery.Operators.Add(tuple, SetOperator.Except);
                        return multiQuery;
                    }
                }
                else
                {
                    throw new NotImplementedException("Where clause expression type not found.");
                }
            }

            #endregion
            #region GROUP BY

            // Group By Expression
            if (spec.GroupByClause != null)
            {
                var groupByClause = spec.GroupByClause;
                foreach (ExpressionGroupingSpecification gSpec in groupByClause.GroupingSpecifications)
                {
                    if (gSpec.Expression is ColumnReferenceExpression)
                    {
                        var attribute = ProcessColumnReferenceExpression(gSpec.Expression as ColumnReferenceExpression);
                        query.GroupBy.Attributes.Add(attribute);
                    }
                }
            }

            #endregion
            #region HAVING

            // Having Expression
            if (spec.HavingClause != null)
            {
                var havingClauseExpression = spec.HavingClause.SearchCondition;
                if (havingClauseExpression is BooleanBinaryExpression)
                {
                    var conditionTuple = ProcessBooleanBinaryExpression(havingClauseExpression as BooleanBinaryExpression);
                    query.Having.Conditions = conditionTuple.Item1;
                    query.Having.Operators = conditionTuple.Item2;

                }
                else if (havingClauseExpression is BooleanComparisonExpression)
                {
                    var condition = ProcessBooleanComparisonExpression(havingClauseExpression as BooleanComparisonExpression);
                    query.Having.Conditions.Add(condition);
                }
                else
                {
                    throw new NotImplementedException("HavingClause type not found.");
                }
            }

            #endregion

            return query;
        }

        private void ModifyQueryDueToIn(Query query, QueryModel.Attribute conAttribute, QueryModel.Attribute selAttribute)
        {
            var condition = new Condition
            {
                LeftSide = conAttribute,
                Operator = BooleanComparisonType.Equals
            };
            condition.RightSide = query.Select.Attributes.First();
            if (query.Where.Conditions.Any())
            {
                var condTuple = new Tuple<Condition, Condition>(query.Where.Conditions.Last(), condition);
                query.Where.Operators.Add(condTuple, BooleanBinaryExpressionType.And);
            }
            query.Where.Conditions.Add(condition);

            query.Select.Attributes.Clear();
            query.Select.Attributes.Add(selAttribute);
        }

        private void ModifyQueryDueToComparison(Query query, QueryModel.Attribute att, BooleanComparisonType op)
        {
            var condition = new Condition
            {
                LeftSide = att
            };
            switch(op)
            {
                case BooleanComparisonType.Equals:
                    condition.Operator = BooleanComparisonType.NotEqualToExclamation; break;
                case BooleanComparisonType.NotEqualToExclamation:
                    condition.Operator = BooleanComparisonType.Equals; break;
                case BooleanComparisonType.GreaterThan:
                    condition.Operator = BooleanComparisonType.LessThanOrEqualTo; break;
                case BooleanComparisonType.GreaterThanOrEqualTo:
                    condition.Operator = BooleanComparisonType.LessThan; break;
                case BooleanComparisonType.LessThan:
                    condition.Operator = BooleanComparisonType.GreaterThanOrEqualTo; break;
                case BooleanComparisonType.LessThanOrEqualTo:
                    condition.Operator = BooleanComparisonType.GreaterThan; break;
            }
            condition.RightSide = query.Select.Attributes.First();
            if (query.Where.Conditions.Any())
            {
                var condTuple = new Tuple<Condition, Condition>(query.Where.Conditions.Last(), condition);
                query.Where.Operators.Add(condTuple, BooleanBinaryExpressionType.And);
            }
            query.Where.Conditions.Add(condition);
        }

        public MultiQuery ProcessBinaryQueryExpression(BinaryQueryExpression exp)
        {
            var multi = new MultiQuery();
            var query1 = ProcessQueryExpression(exp.FirstQueryExpression) as Query;
            var query2 = ProcessQueryExpression(exp.SecondQueryExpression) as Query;
            var tuple = new Tuple<dynamic, dynamic>(query1, query2);
            SetOperator op = 
                exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Except
                ? SetOperator.Except
                : exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Intersect
                    ? SetOperator.Intersect
                    : exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Union
                        ? SetOperator.Union
                        : SetOperator.CartesianProduct; // This last case should not happen.

            multi.Operators.Add(tuple, op);
            multi.Queries.Add(query1);
            multi.Queries.Add(query2);
            return multi;
        }

        public dynamic ProcessColumnReferenceExpression(ColumnReferenceExpression exp)
        {
            if (exp.ColumnType == ColumnType.Regular)
            {
                var att = new QueryModel.Attribute();
                if(exp.MultiPartIdentifier.Count == 1)
                {
                    att.Name = exp.MultiPartIdentifier.Identifiers[0].Value;
                }
                else if(exp.MultiPartIdentifier.Count == 2)
                {
                    att.Alias = exp.MultiPartIdentifier.Identifiers[0].Value;
                    att.Name = exp.MultiPartIdentifier.Identifiers[1].Value;
                }
                return att;
            }
            else if (exp.ColumnType == ColumnType.Wildcard)
            {
                return "*";
            }
            else
            {
                throw new Exception("ColumnReferenceExpression could not be identified.");
            }
        }

        public Tuple<List<Condition>, Dictionary<Tuple<Condition,Condition>,BooleanBinaryExpressionType>>
            ProcessBooleanBinaryExpression(BooleanBinaryExpression exp)
        {
            if (exp == null) return null;
            Condition left;
            Condition right;
            var conditions = new List<Condition>();
            var operators = new Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType>();
            
            if (exp.FirstExpression is BooleanComparisonExpression)
            {
                var leftExp = exp.FirstExpression as BooleanComparisonExpression;
                left = ProcessBooleanComparisonExpression(leftExp);
                conditions.Insert(0, left);
            }
            else if (exp.FirstExpression is BooleanBinaryExpression)
            {
                var leftExp = exp.FirstExpression as BooleanBinaryExpression;
                var tuple = ProcessBooleanBinaryExpression(leftExp);
                left = tuple.Item1.Last();
                foreach (var c in tuple.Item1)
                {
                    conditions.Add(c);
                }
                foreach (var t in tuple.Item2)
                {
                    operators.Add(t.Key, t.Value);
                }
            }
            else
            {
                throw new Exception();
            }
            
            var op = exp.BinaryExpressionType;
            if (exp.SecondExpression is BooleanComparisonExpression)
            {
                var rightExp = exp.SecondExpression as BooleanComparisonExpression;
                right = ProcessBooleanComparisonExpression(rightExp);
                var tuple = new Tuple<Condition, Condition>(left, right);
                operators.Add(tuple, op);
                conditions.Add(right);
            }
            else
            {
                throw new Exception();
            }
            return new Tuple<List<Condition>, Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType>>(conditions, operators);
        }

        public Condition ProcessBooleanComparisonExpression(BooleanComparisonExpression exp)
        {
            var condition = new Condition()
            {
                LeftSide = ProcessScalarExpression(exp.FirstExpression),
                Operator = exp.ComparisonType,
                RightSide = ProcessScalarExpression(exp.SecondExpression)
            };
            return condition;
        }

        public dynamic ProcessSelectScalarExpression(SelectScalarExpression exp)
        {
            return ProcessScalarExpression(exp.Expression);
        }

        public dynamic ProcessScalarExpression(ScalarExpression exp)
        {
            if (exp is ColumnReferenceExpression)
            {
                return ProcessColumnReferenceExpression(exp as ColumnReferenceExpression);
            }
            else if (exp is FunctionCall)
            {
                var funcExp = exp as FunctionCall;
                var func = new Function();
                func.Type = funcExp.FunctionName.Value;
                
                foreach (ScalarExpression p in funcExp.Parameters)
                {                    
                    var col = ProcessScalarExpression(p);
                    if(col is QueryModel.Attribute)
                    {
                        var att = col as QueryModel.Attribute;
                        func.Attributes.Add(att);
                    }
                    else if(col is string)
                    {
                        var att = col as string;
                        if(att == "*")
                        {
                            func.IsWildCard = true;
                            break;
                        }
                    }
                }
                return func;
            }
            else if (exp is StringLiteral)
            {
                var text = exp as StringLiteral;
                return text.Value;
            }
            else if (exp is ScalarSubquery)
            {
                var subquery = exp as ScalarSubquery;
                var query = ProcessQuerySpecification(subquery.QueryExpression as QuerySpecification);
                return query;
            }
            else if (exp is IntegerLiteral)
            {
                var integer = exp as IntegerLiteral;
                return integer.Value;
            }
            else
            {
                _console.WriteLine("Scalar expression not identified.");
                return 0;
            }
        }

        #endregion

        private bool QueryIsValid(dynamic query, List<string> errors)
        {
            if (query == null) return true;

            var isValid = true;
            if (query is MultiQuery)
            {
                var unboxed = query as MultiQuery;
                if (unboxed.Queries.Count == 1)
                    isValid = QueryIsValid(unboxed.Queries[0], errors);
                else if (unboxed.Queries.Count == 2 && isValid) // Don't continue validation if we're already invalid
                    isValid = QueryIsValid(unboxed.Queries[0], errors) && QueryIsValid(unboxed.Queries[1], errors);
            }
            else if (query is Query)
            {
                var unboxed = query as Query;
                // Check that table names exist in schema
                foreach (var r in unboxed.From.Relations)
                {
                    if (!_schema.Relations.Select(l => l.Name).Contains(r.Name))
                    {
                        errors.Add(string.Format("'{0}' is not a table found in the given schema.", r.Name));
                    }
                    if (errors.Any()) return false;
                }
                // Check that attributes in Select exist in specified table
                foreach (var a in unboxed.Select.Attributes)
                {
                    errors.AddRange(ValidateAttribute(a, unboxed.From.Relations));
                    if (errors.Any()) return false;
                }
                // Check that attributes in From exist in schema.
                foreach(var c in unboxed.Where.Conditions)
                {
                    errors.AddRange(ValidateAttribute(c.LeftSide, unboxed.From.Relations));
                    errors.AddRange(ValidateAttribute(c.RightSide, unboxed.From.Relations));
                    if (errors.Any()) return false;
                }
            }
            return isValid;
        }
        private List<string> ValidateAttribute(dynamic attribute, List<Relation> relations)
        {
            List<string> errors = new List<string>();
            if (attribute is QueryModel.Attribute)
            {
                var attr = attribute as QueryModel.Attribute;
                if (string.IsNullOrEmpty(attr.Alias))
                {
                    var matchingRelations = relations.Where(r => r.Attributes.Any(t => t.Name == attr.Name));
                    if (matchingRelations.Count() > 1)
                    {
                        errors.Add(string.Format("The attribute '{0}' with no alias was found on multiple tables.", attr.Name));
                    }
                }
                else
                {
                    var relation = relations.SingleOrDefault(r => r.Aliases.Contains(attr.Alias) && r.Attributes.Select(a => a.Name).Contains(attr.Name));
                    if (relation == null)
                    {
                        errors.Add(string.Format("The attribute '{0}' with alias '{1}' was not found on any existing table.", attr.Name, attr.Alias));
                    }
                }
            }
            else if (attribute is Function)
            {
                var func = attribute as Function;
                foreach (var f in func.Attributes)
                {
                    if (string.IsNullOrEmpty(f.Alias))
                    {
                        var matchingRelations = relations.Where(r => r.Attributes.Any(t => t.Name == f.Name));
                        if (matchingRelations.Count() > 1)
                        {
                            errors.Add(string.Format("The attribute '{0}' with no alias was found on multiple tables.", f.Name));
                        }
                    }
                    else
                    {
                        var relation = relations.SingleOrDefault(r => r.Aliases.Contains(f.Alias) && r.Attributes.Select(a => a.Name).Contains(f.Name));
                        if (relation == null)
                        {
                            errors.Add(string.Format("The attribute '{0}' with alias '{1}' was not found on any existing table.", f.Name, f.Alias));
                        }
                    }
                }
            }
            return errors;
        }
    }
}

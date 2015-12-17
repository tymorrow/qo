namespace Qo.Parsing
{
    using Microsoft.SqlServer.TransactSql.ScriptDom;
    using QueryModel;
    using RelationalModel;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using TSql = Microsoft.SqlServer.TransactSql.ScriptDom;

    public class QoParser
    {
        private readonly IConsole _console;
        private readonly Schema _schema;
        private string _lastQueryString;
        private readonly List<string> _setOperators = new List<string>
        {
            "union",
            "intersect",
            "except"
        };
        private int _queryCounter;
        
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
                    _console.WriteLine(sb.ToString());
                }
                else
                {
                    var batch = script.Batches.First();
                    var statement = batch.Statements.First();
                    var result = ProcessStatement(statement);
                    if(result is Query)
                    {
                        var unboxedResult = result as Query;
                        var tree = unboxedResult.GetQueryTree();
                        package.RelationalAlgebra = tree.ToString();
                        package.InitialTree = tree.GetCleanNode();
                    }
                    if(result is MultiQuery)
                    {
                        var unboxedResult = result as MultiQuery;
                        var tree = unboxedResult.GetQueryTree();
                        package.RelationalAlgebra = tree.ToString();
                        package.InitialTree = tree.GetCleanNode();
                    }
                    package.Success = true;
                }
            }
            catch (Exception e)
            {
                _console.WriteLine(e.Message);
            }
            _console.WriteLine(package.RelationalAlgebra);
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
            MultiQuery multiQuery;

            #region SELECT

            _console.WriteLine("SELECT");
            Debug.Indent();
            foreach (SelectScalarExpression exp in spec.SelectElements)
            {
                var att = ProcessSelectScalarExpression(exp);
                query.Select.Attributes.Add(att);
            }
            Debug.Unindent();

            #endregion
            #region FROM 

            _console.WriteLine("FROM");
            Debug.Indent();
            foreach (NamedTableReference t in spec.FromClause.TableReferences)
            {
                var tableName = t.SchemaObject.BaseIdentifier.Value;
                var schemaRelation = _schema.Relations.Single(r => r.Name == tableName);
                var relation = new Relation(schemaRelation);
                query.From.Relations.Add(relation);

                if (t.Alias != null)
                {
                    if (!schemaRelation.Aliases.Contains(t.Alias.ToString()))
                    {
                        relation.Aliases.Add(t.Alias.Value);
                        schemaRelation.Aliases.Add(t.Alias.Value);
                    }
                    _console.Write(t.Alias.Value + " ");
                }
                _console.Write(t.SchemaObject.BaseIdentifier.Value);
                _console.WriteLine(string.Empty);
            }
            Debug.Unindent();

            #endregion
            #region WHERE

            _console.WriteLine("WHERE");
            Debug.Indent();
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
                else if(whereClause.FirstExpression is ColumnReferenceExpression &&
                        whereClause.SecondExpression is ScalarSubquery)
                {
                    multiQuery = new MultiQuery();
                    var leftExp = whereClause.FirstExpression as ColumnReferenceExpression;
                    var rightExp = whereClause.SecondExpression as ScalarSubquery;
                    var att = ProcessColumnReferenceExpression(leftExp) as QueryModel.Attribute;
                    dynamic rightQueryExp = ProcessQueryExpression(rightExp.QueryExpression);

                    if (rightQueryExp is Query)
                    {
                        var rightQuery = rightQueryExp as Query;
                        // Add comparison to where clause of subquery 
                        ModifyQueryDueToComparison(rightQuery, att, whereClause.ComparisonType);
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
                multiQuery = new MultiQuery();
                multiQuery.Queries.Add(query);

                _console.WriteLine("IN ");
                _console.WriteLine("(");
                Debug.Indent();
                rightQueryExp = ProcessQueryExpression(subQuery.QueryExpression);
                Debug.Unindent();
                _console.WriteLine(")");
                
                if (rightQueryExp is Query)
                {
                    var rightQuery = rightQueryExp as Query;
                    // Add comparison to where clause of subquery 
                    ModifyQueryDueToIn(rightQuery, att);
                    // Build/return multi-query
                    multiQuery.Queries.Add(rightQuery);
                    var tuple = new Tuple<dynamic, dynamic>(query, rightQuery);
                    multiQuery.Operators.Add(tuple, SetOperator.Division);
                    return multiQuery;
                }
                else if(rightQueryExp is MultiQuery)
                {
                    rightMultiQuery = rightQueryExp as MultiQuery;
                    // Add comparison to where clause of left subquery
                    ModifyQueryDueToIn(rightMultiQuery.Queries[0], att);
                    // Add comparison to where clause of right subquery
                    ModifyQueryDueToIn(rightMultiQuery.Queries[1], att);
                    // Build/return multi-query
                    multiQuery.Queries.Add(rightMultiQuery);
                    var tuple = new Tuple<dynamic, dynamic>(query, rightMultiQuery);
                    multiQuery.Operators.Add(tuple, SetOperator.Division);
                    return multiQuery;
                }
                #endregion
            }
            else
            {
                throw new NotImplementedException("WhereClause type not found.");
            }
            Debug.Unindent();

            #endregion
            #region GROUP BY

            // Group By Expression
            if (spec.GroupByClause != null)
            {
                _console.WriteLine("GROUP BY");
                Debug.Indent();
                var groupByClause = spec.GroupByClause;
                foreach (ExpressionGroupingSpecification gSpec in groupByClause.GroupingSpecifications)
                {
                    if (gSpec.Expression is ColumnReferenceExpression)
                    {
                        ProcessColumnReferenceExpression(gSpec.Expression as ColumnReferenceExpression);
                    }
                }
                Debug.Unindent();
                _console.WriteLine(string.Empty);
            }

            #endregion
            #region HAVING

            // Having Expression
            if (spec.HavingClause != null)
            {
                _console.WriteLine("HAVING ");
                Debug.Indent();
                var havingClauseExpression = spec.HavingClause.SearchCondition;
                if (havingClauseExpression is BooleanBinaryExpression)
                {
                    ProcessBooleanBinaryExpression(havingClauseExpression as BooleanBinaryExpression);
                }
                else if (havingClauseExpression is BooleanComparisonExpression)
                {
                    ProcessBooleanComparisonExpression(havingClauseExpression as BooleanComparisonExpression);
                }
                else
                {
                    throw new NotImplementedException("HavingClause type not found.");
                }
                Debug.Unindent();
            }

            #endregion

            return query;
        }

        private void ModifyQueryDueToIn(Query query, QueryModel.Attribute att)
        {
            var condition = new Condition
            {
                LeftSide = att,
                Operator = BooleanComparisonType.Equals
            };
            condition.RightSide = query.Select.Attributes.First();
            if (query.Where.Conditions.Any())
            {
                var condTuple = new Tuple<Condition, Condition>(query.Where.Conditions.Last(), condition);
                query.Where.Operators.Add(condTuple, BooleanBinaryExpressionType.And);
            }
            query.Where.Conditions.Add(condition);
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
            _console.WriteLine("(");
            var query1 = ProcessQueryExpression(exp.FirstQueryExpression) as Query;
            _console.WriteLine(")");
            _console.WriteLine(exp.BinaryQueryExpressionType.ToString());
            SetOperator op = 
                exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Except
                ? SetOperator.Except
                : exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Intersect
                    ? SetOperator.Intersect
                    : exp.BinaryQueryExpressionType == BinaryQueryExpressionType.Union
                        ? SetOperator.Union
                        : SetOperator.CartesianProduct; // This last case should not happen.
            _console.WriteLine("(");
            var query2 = ProcessQueryExpression(exp.SecondQueryExpression) as Query;
            _console.WriteLine(")");
            var tuple = new Tuple<dynamic, dynamic>(query1, query2);
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
                foreach (var i in exp.MultiPartIdentifier.Identifiers)
                {
                    _console.Write(i.Value + " ");
                }
                return att;
            }
            else if (exp.ColumnType == ColumnType.Wildcard)
            {
                _console.Write("*");
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

            _console.WriteLine(exp.BinaryExpressionType.ToString());
            var op = exp.BinaryExpressionType;
            if (exp.SecondExpression is BooleanComparisonExpression)
            {
                var rightExp = exp.SecondExpression as BooleanComparisonExpression;
                right = ProcessBooleanComparisonExpression(rightExp);
                var tuple = new Tuple<Condition, Condition>(left, right);
                //var kvp = new KeyValuePair<Tuple<Condition, Condition>, BooleanBinaryExpressionType>(tuple, op);
                operators.Add(tuple, op);
                conditions.Add(right);
            }
            //else if (exp.SecondExpression is BooleanBinaryExpression)
            //{
            //    var rightExp = exp.SecondExpression as BooleanBinaryExpression;
            //    var tuple = ProcessBooleanBinaryExpression(rightExp);
            //    right = tuple.Item1.First();
            //    foreach (var c in tuple.Item1)
            //    {
            //        conditions.Add(c);
            //    }
            //    foreach(var t in tuple.Item2)
            //    {
            //        operators.Add(t.Key, t.Value);
            //    }
            //}
            else
            {
                throw new Exception();
            }
            return new Tuple<List<Condition>, Dictionary<Tuple<Condition, Condition>, BooleanBinaryExpressionType>>(conditions, operators);
        }

        public Condition ProcessBooleanComparisonExpression(BooleanComparisonExpression exp)
        {
            var condition = new Condition();
            condition.LeftSide = ProcessScalarExpression(exp.FirstExpression);
            _console.Write(exp.ComparisonType + " ");
            condition.Operator = exp.ComparisonType;
            condition.RightSide = ProcessScalarExpression(exp.SecondExpression);
            _console.WriteLine(string.Empty);
            return condition;
        }

        public dynamic ProcessSelectScalarExpression(SelectScalarExpression exp)
        {
            var att = ProcessScalarExpression(exp.Expression);
            if (exp.ColumnName != null)
            {
                _console.Write(exp.ColumnName.Value);
            }
            _console.WriteLine(string.Empty);

            return att;
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

                _console.Write(funcExp.FunctionName.Value + "(");
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
                _console.Write(") ");
                return func;
            }
            else if (exp is StringLiteral)
            {
                var text = exp as StringLiteral;
                _console.Write("\"" + text.Value + "\" ");
                return text.Value;
            }
            else if (exp is ScalarSubquery)
            {
                var subquery = exp as ScalarSubquery;
                _console.WriteLine(string.Empty);
                _console.WriteLine("(");
                var query = ProcessQuerySpecification(subquery.QueryExpression as QuerySpecification);
                _console.WriteLine(")");
                return query;
            }
            else if (exp is IntegerLiteral)
            {
                var integer = exp as IntegerLiteral;
                _console.Write(integer.Value);
                return integer.Value;
            }
            else
            {
                _console.WriteLine("Scalar expression not identified.");
                return 0;
            }
        }

        #endregion
    }

    public class QoPackage
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public string RelationalAlgebra { get; set; }
        public CleanNode InitialTree { get; set; }
    }
}

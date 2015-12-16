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
        IList<TSqlParserToken> _tokens;
        private string _lastQueryString;
        private readonly List<string> _setOperators = new List<string>
        {
            "union",
            "intersect",
            "except"
        };
        private int _queryCounter;
        private Node _tree = new Node();

        
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

        public bool Parse(string query)
        {
            var success = false;
            _tree = new Node();
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
                    _console.WriteToConsole(sb.ToString());
                }
                else
                {
                    foreach (TSqlBatch batch in script.Batches)
                    {
                        foreach (TSqlStatement statement in batch.Statements)
                        {
                            ProcessStatement(statement);
                        }
                    }
                    success = true;
                }
            }
            catch (Exception e)
            {
                _console.WriteToConsole(e.Message);
            }
            
            return success;
        }
        
        #region TSqlParser Methods

        public void ProcessStatement(TSqlStatement statement)
        {
            // Could be Stored Procedure, While, or If but for this project, we're not expecting those.
            // if(statement is TSql.WhileStatement)
            //     ProcessStatements(((TSql.WhileStatement)statement).Statement);
            if (statement is TSql.SelectStatement)
                ProcessQueryExpression(((TSql.SelectStatement)statement).QueryExpression);
        }

        public void ProcessQueryExpression(QueryExpression exp)
        {
            if (exp is TSql.QuerySpecification)
            {
                // Actual Select Statement
                ProcessQuerySpecification(exp as TSql.QuerySpecification);
            }
            else if (exp is TSql.BinaryQueryExpression)
            {
                // Union
                ProcessBinaryQueryExpression(exp as TSql.BinaryQueryExpression);
            }
            if (exp is TSql.QueryParenthesisExpression)
            {
                // Select surrounded by paranthesis - sub-select
                throw new NotImplementedException("QueryParenthesisExpression not implemented.");
            }
        }

        public void ProcessQuerySpecification(QuerySpecification s)
        {
            if (s == null) return;
            // Select Expression
            Debug.WriteLine("SELECT");
            Debug.Indent();
            foreach (SelectScalarExpression exp in s.SelectElements)
            {
                ProcessSelectScalarExpression(exp);
            }
            Debug.Unindent();
            // From Expression
            Debug.WriteLine("FROM");
            Debug.Indent();
            foreach (NamedTableReference t in s.FromClause.TableReferences)
            {
                if (t.Alias != null)
                {
                    Debug.Write(t.Alias.Value + " ");
                }
                Debug.Write(t.SchemaObject.BaseIdentifier.Value);
                Debug.WriteLine(string.Empty);
            }
            Debug.Unindent();
            Debug.WriteLine("WHERE");
            Debug.Indent();
            // Where Expression - identify type to recurse appropriately
            var whereClauseExpression = s.WhereClause.SearchCondition;
            if (whereClauseExpression is BooleanBinaryExpression)
            {
                var whereClause = whereClauseExpression as BooleanBinaryExpression;
                ProcessBooleanBinaryExpression(whereClause);
            }
            else if (whereClauseExpression is BooleanComparisonExpression)
            {
                var whereClause = whereClauseExpression as BooleanComparisonExpression;
                ProcessBooleanComparisonExpression(whereClause);
            }
            else if (whereClauseExpression is BooleanIsNullExpression)
            {
                throw new NotImplementedException("BooleanIsNullExpression not implemented.");
            }
            else if (whereClauseExpression is BooleanNotExpression)
            {
                throw new NotImplementedException("BooleanNotExpression not implemented.");
            }
            else if (whereClauseExpression is BooleanParenthesisExpression)
            {
                throw new NotImplementedException("BooleanParenthesisExpression not implemented.");
            }
            else if (whereClauseExpression is ExistsPredicate)
            {
                throw new NotImplementedException("ExistsPredicate not implemented.");
            }
            else if (whereClauseExpression is InPredicate)
            {
                var whereClause = whereClauseExpression as InPredicate;
                var expression = whereClause.Expression as ColumnReferenceExpression;
                var subQuery = whereClause.Subquery as ScalarSubquery;
                ProcessColumnReferenceExpression(expression);
                Debug.WriteLine("IN ");
                Debug.WriteLine("(");
                Debug.Indent();
                if (subQuery.QueryExpression is BinaryQueryExpression)
                {
                    ProcessQueryExpression(subQuery.QueryExpression);
                }
                else if (subQuery.QueryExpression is QuerySpecification)
                {
                    ProcessQueryExpression(subQuery.QueryExpression);
                }
                else
                {
                    throw new Exception("Subquery type not handled.");
                }
                Debug.Unindent();
                Debug.WriteLine(")");
            }
            else if (whereClauseExpression is SubqueryComparisonPredicate)
            {
                throw new NotImplementedException("SubqueryComparisonPredicate not implemented.");
            }
            else
            {
                throw new NotImplementedException("WhereClause type not found.");
            }
            Debug.Unindent();

            // Group By Expression
            if (s.GroupByClause != null)
            {
                Debug.WriteLine("GROUP BY");
                Debug.Indent();
                var groupByClause = s.GroupByClause;
                foreach (ExpressionGroupingSpecification spec in groupByClause.GroupingSpecifications)
                {
                    if (spec.Expression is ColumnReferenceExpression)
                    {
                        ProcessColumnReferenceExpression(spec.Expression as ColumnReferenceExpression);
                    }
                }
                Debug.Unindent();
                Debug.WriteLine(string.Empty);
            }
            // Having Expression
            if (s.HavingClause != null)
            {
                Debug.WriteLine("HAVING ");
                Debug.Indent();
                var havingClauseExpression = s.HavingClause.SearchCondition;
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

            //s.Dump();
        }

        public void ProcessBinaryQueryExpression(BinaryQueryExpression exp)
        {
            Debug.WriteLine("(");
            if (exp.FirstQueryExpression is QueryParenthesisExpression)
            {
                var first = exp.FirstQueryExpression as QueryParenthesisExpression;
                ProcessQuerySpecification(first.QueryExpression as QuerySpecification);
            }
            else if (exp.FirstQueryExpression is QuerySpecification)
            {
                var first = exp.FirstQueryExpression as QuerySpecification;
                ProcessQuerySpecification(first);
            }
            else
            {
                throw new NotImplementedException("FirstQueryExpression of BinaryQueryExpression type not found.");
            }
            Debug.WriteLine(")");
            Debug.WriteLine(exp.BinaryQueryExpressionType);
            Debug.WriteLine("(");
            if (exp.SecondQueryExpression is QueryParenthesisExpression)
            {
                var second = exp.SecondQueryExpression as QueryParenthesisExpression;
                ProcessQuerySpecification(second.QueryExpression as QuerySpecification);
            }
            else if (exp.SecondQueryExpression is QuerySpecification)
            {
                ProcessQuerySpecification(exp.SecondQueryExpression as QuerySpecification);
            }
            else
            {
                throw new NotImplementedException("SecondQueryExpression of BinaryQueryExpression type not found.");
            }
            Debug.WriteLine(")");
        }

        public void ProcessColumnReferenceExpression(ColumnReferenceExpression exp)
        {
            if (exp.ColumnType == ColumnType.Regular)
            {
                foreach (var i in exp.MultiPartIdentifier.Identifiers)
                {
                    Debug.Write(i.Value + " ");
                }
            }
            else if (exp.ColumnType == ColumnType.Wildcard)
            {
                Debug.Write("*");
            }
        }

        public void ProcessBooleanBinaryExpression(BooleanBinaryExpression exp)
        {
            if (exp.FirstExpression is BooleanBinaryExpression)
            {
                var first = exp.FirstExpression as BooleanBinaryExpression;
                ProcessBooleanBinaryExpression(first);
            }
            else if (exp.FirstExpression is BooleanComparisonExpression)
            {
                var first = exp.FirstExpression as BooleanComparisonExpression;
                ProcessBooleanComparisonExpression(first);
            }
            else
            {

            }
            Debug.WriteLine(exp.BinaryExpressionType);
            if (exp.SecondExpression is BooleanBinaryExpression)
            {
                var second = exp.SecondExpression as BooleanBinaryExpression;
                ProcessBooleanBinaryExpression(second);
            }
            else if (exp.SecondExpression is BooleanComparisonExpression)
            {
                var second = exp.SecondExpression as BooleanComparisonExpression;
                ProcessBooleanComparisonExpression(second);
            }
            else
            {

            }
        }

        public void ProcessBooleanComparisonExpression(BooleanComparisonExpression exp)
        {
            if (exp == null) return;
            ProcessScalarExpression(exp.FirstExpression);
            Debug.Write(exp.ComparisonType + " ");
            ProcessScalarExpression(exp.SecondExpression);
            Debug.WriteLine(string.Empty);
        }

        public void ProcessScalarExpression(ScalarExpression exp)
        {
            if (exp is ColumnReferenceExpression)
            {
                ProcessColumnReferenceExpression(exp as ColumnReferenceExpression);
            }
            else if (exp is FunctionCall)
            {
                var func = exp as FunctionCall;
                Debug.Write(func.FunctionName.Value + "(");
                foreach (ScalarExpression p in func.Parameters)
                {
                    ProcessScalarExpression(p);
                }
                Debug.Write(") ");
            }
            else if (exp is StringLiteral)
            {
                var text = exp as StringLiteral;
                Debug.Write("\"" + text.Value + "\" ");
            }
            else if (exp is ScalarSubquery)
            {
                var subquery = exp as ScalarSubquery;
                Debug.WriteLine(string.Empty);
                Debug.WriteLine("(");
                ProcessQuerySpecification(subquery.QueryExpression as QuerySpecification);
                Debug.WriteLine(")");
            }
            else if (exp is IntegerLiteral)
            {
                var integer = exp as IntegerLiteral;
                Debug.Write(integer.Value);
            }
            else
            {
                Debug.WriteLine("Scalar expression not identified.");
            }
        }

        public void ProcessSelectScalarExpression(SelectScalarExpression exp)
        {
            ProcessScalarExpression(exp.Expression);
            if (exp.ColumnName != null)
            {
                Debug.Write(exp.ColumnName.Value);
            }
            Debug.WriteLine(string.Empty);
        }

        #endregion

        private void AssembleMultiQuery(MultiQuery result)
        {
            var queries = _lastQueryString.Split(_setOperators.ToArray(), StringSplitOptions.None).Select(q => q.Trim()).ToList();
            var tokens = _lastQueryString.Split(' ').ToList();
            var querySetOperators = tokens.Where(t => _setOperators.Contains(t)).ToList();

            foreach (var q in queries)
            {
                result.Queries.Add(new Query
                {
                    OriginalString = q
                });
            }

            if (queries.Count > 1)
            {
                for (var i = 1; i < result.Queries.Count; i++)
                {
                    var query1 = result.Queries[i - 1];
                    var query2 = result.Queries[i];
                    var normalizedOperator = querySetOperators[i - 1] + " ";
                    var op = MultiQuery.OperatorMap.Single(x => x.Value == normalizedOperator).Key;
                    result.Operators.Add(new Tuple<Query, Query>(query1, query2), op);
                }
            }
        }
        private void AssembleQuery(Query query)
        {
            var tokens = query.OriginalString.Split(' ').ToList();
            var fromIndex = 0;
            var whereIndex = 0;
            foreach (var t in tokens)
            {
                if(t == "from")
                {
                    fromIndex = tokens.IndexOf(t);
                }
                if (t == "where")
                {
                    whereIndex = tokens.IndexOf(t);
                    break;
                }
            }

            if(whereIndex > fromIndex)
            {
                query.From = GetFromStatement(tokens.GetRange(fromIndex, whereIndex - fromIndex));
                query.Select = GetSelectStatement(tokens.GetRange(0, fromIndex));
                query.Where = GetWhereStatement(tokens.GetRange(whereIndex, tokens.Count - whereIndex));
            }
            else
            {
                query.From = GetFromStatement(tokens.GetRange(fromIndex, tokens.Count - fromIndex));
                query.Select = GetSelectStatement(tokens.GetRange(0, fromIndex));
            }
        }
        private FromStatement GetFromStatement(ICollection<string> tokens)
        {
            tokens.Remove("from");
            var statement = new FromStatement();
            var statementString = new StringBuilder();
            foreach(var t in tokens)
            {
                statementString.Append(t + " ");
            }
            var relations = statementString.ToString().Contains(',') 
                ? statementString.ToString().Split(',').Select(t => t.Trim()).ToList() 
                : tokens.ToList();

            foreach (var r in relations)
            {
                Relation newRelation;
                var relationTokens = r.Split(' ').ToList();
                if(relationTokens.Contains("as"))
                {
                    newRelation = _schema.Relations.Single(rs =>
                        rs.Name == relationTokens[0]);
                    newRelation.Aliases.Add(relationTokens[2]);
                }
                else if (relationTokens.Count == 2)
                {
                    newRelation = _schema.Relations.Single(rs =>
                        rs.Name == relationTokens[0]);
                    newRelation.Aliases.Add(relationTokens[1]);
                }
                else
                {
                    newRelation = _schema.Relations.Single(rs =>
                        rs.Name == relationTokens[0]);
                    newRelation.Aliases.Add(relationTokens[0].Substring(0, 1));
                }

                statement.Relations.Add(newRelation);
            }
            statement.Relations = statement.Relations.OrderBy(r => r.Priority).ToList();
            return statement;
        }
        private QueryModel.SelectStatement GetSelectStatement(ICollection<string> tokens)
        {
            tokens.Remove("select");
            var statement = new QueryModel.SelectStatement();
            var statementString = String.Concat(tokens);
            var attributes = statementString.Contains(',') 
                ? statementString.Split(',').Select(t => t.Trim()).ToList() 
                : tokens.ToList();

            foreach(var a in attributes)
            {
                statement.Attributes.Add(GetAttribute(a));
            }

            return statement;
        }
        private WhereStatement GetWhereStatement(ICollection<string> tokens)
        {
            tokens.Remove("where");
            var statement = new WhereStatement();
            var statementString = new StringBuilder();
            foreach (var t in tokens)
            {
                statementString.Append(t + " ");
            }
            var conditions = tokens.Contains("and") || tokens.Contains("or")
                ? statementString.ToString()
                    .Split(WhereStatement.OperatorMap.Values.Select(v => " "+v.Trim()+" ").ToArray(), StringSplitOptions.None)
                    .Select(t => t.Trim()).ToList()
                : tokens.ToList();
            if(conditions.Count > 1)
            {
                var operators = tokens.Where(t => WhereStatement.OperatorMap.Values.Contains(t+" ")).ToList();
                for (var i = 1; i < conditions.Count; i++)
                {
                    if (i == 1)
                    {
                        List<string> leftSplit;
                        ConditionalOperator leftConditionalOperator;
                        dynamic condition1LeftSide;
                        dynamic condition1RightSide;
                        if (conditions[i - 1].Contains(" "))
                        {
                            leftSplit = conditions[i - 1].Split(' ').Select(t => t.Trim()).ToList();
                            condition1LeftSide = leftSplit[0].Contains(".") ? GetAttribute(leftSplit[0]) : GetConditionValue(leftSplit[0]);
                            leftConditionalOperator = Condition.OperatorMap.Single(x => x.Value == leftSplit[1] + " ").Key;
                            condition1RightSide = leftSplit[2].Contains(".") ? GetAttribute(leftSplit[2]) : GetConditionValue(leftSplit[2]);
                        }
                        else
                        {
                            leftSplit = conditions[i - 1]
                                .Split(Condition.OperatorMap.Values.Select(v => v.Trim()).ToArray(), StringSplitOptions.None)
                                .Select(t => t.Trim()).ToList();
                            var leftExtraction = conditions[i - 1].Replace(leftSplit[0], "").Replace(leftSplit[1], "").Trim();
                            condition1LeftSide = leftSplit[0].Contains(".") ? GetAttribute(leftSplit[0]) : GetConditionValue(leftSplit[0]);
                            leftConditionalOperator = Condition.OperatorMap.Single(x => x.Value == leftExtraction + " ").Key;
                            condition1RightSide = leftSplit[1].Contains(".") ? GetAttribute(leftSplit[1]) : GetConditionValue(leftSplit[1]);
                        }
                        statement.Conditions.Add(new Condition
                        {
                            LeftSide = condition1LeftSide,
                            Operator = leftConditionalOperator,
                            RightSide = condition1RightSide,
                            QueryNumber = _queryCounter
                        });
                    }

                    List<string> rightSplit;
                    ConditionalOperator rightConditionalOperator;
                    dynamic condition2LeftSide;
                    dynamic condition2RightSide;
                    if (conditions[i].Contains(" "))
                    {
                        rightSplit = conditions[i].Split(' ').Select(t => t.Trim()).ToList();
                        condition2LeftSide = rightSplit[0].Contains(".") ? GetAttribute(rightSplit[0]) : GetConditionValue(rightSplit[0]);
                        rightConditionalOperator = Condition.OperatorMap.Single(x => x.Value == rightSplit[1] + " ").Key;
                        condition2RightSide = rightSplit[1].Contains(".") ? GetAttribute(rightSplit[2]) : GetConditionValue(rightSplit[2]);
                    }
                    else
                    {
                        rightSplit = conditions[i]
                            .Split(Condition.OperatorMap.Values.Select(v => v.Trim()).ToArray(), StringSplitOptions.None)
                            .Select(t => t.Trim()).ToList();
                        condition2LeftSide = rightSplit[0].Contains(".") ? GetAttribute(rightSplit[0]) : GetConditionValue(rightSplit[0]);
                        var rightExtraction = conditions[i].Replace(rightSplit[0], "").Replace(rightSplit[1], "").Trim();
                        rightConditionalOperator = Condition.OperatorMap.Single(x => x.Value == rightExtraction + " ").Key;
                        condition2RightSide = rightSplit[1].Contains(".") ? GetAttribute(rightSplit[1]) : GetConditionValue(rightSplit[1]);
                    }
                    statement.Conditions.Add(new Condition
                    {
                        LeftSide = condition2LeftSide,
                        Operator = rightConditionalOperator,
                        RightSide = condition2RightSide,
                        QueryNumber = _queryCounter
                    });

                    var normalizedOperator = operators[i - 1] + " ";
                    var conditionsOperator = WhereStatement.OperatorMap.Single(x => x.Value == normalizedOperator).Key;
                    var leftCondition = statement.Conditions[i - 1];
                    var rightCondition = statement.Conditions[i];
                    statement.Operators.Add(new Tuple<Condition, Condition>(leftCondition, rightCondition), conditionsOperator);
                }
                return statement;
            }

            var split = conditions[0].Replace(" ", string.Empty)
                            .Split(Condition.OperatorMap.Values.Select(v => v.Trim()).ToArray(), StringSplitOptions.None)
                            .Select(t => t.Trim()).ToList();
            var op = Condition.OperatorMap.Single(x => x.Value == split[1] + " ").Key;
            statement.Conditions.Add(new Condition
            {
                LeftSide = split[0],
                Operator = op,
                RightSide = split[2]
            });

            return statement;
        }

        private dynamic GetConditionValue(string p)
        {
            double value;
            var isNumber = double.TryParse(p, out value);
            if (isNumber) return value;
            return p.Replace("'", "").Replace("’", "").Replace("`", "").Replace("‘", "");
        }

        private QueryModel.Attribute GetAttribute(string a)
        {
            var newAttribute = new QueryModel.Attribute
            {
                QueryNumber = _queryCounter
            };

            if (a.Contains("."))
            {
                var attributeTokens = a.Split('.');
                Relation relation;
                string alias;
                // Alias is relation name
                if (_schema.Relations.Any(t => t.Name == attributeTokens[0]))
                {
                    relation = _schema.Relations.Single(t => t.Name == attributeTokens[0]);
                    alias = relation.Aliases[_queryCounter];
                    newAttribute.Name = attributeTokens[1];
                }
                else
                {
                    relation = _schema.Relations.SingleOrDefault(r => 
                        r.Attributes.Any(t => 
                            t.Name == attributeTokens[1]) &&
                            r.Aliases.Contains(attributeTokens[0]));
                    if (relation == null)
                    {
                        throw new Exception("There is a conflict between your attribute aliases and the internal schema.");
                    }
                    alias = relation.Aliases[_queryCounter];
                }
                newAttribute.Alias = alias;
                newAttribute.Name = attributeTokens[1];
            }
            else if (a.Contains("as"))
            {
                var attributeTokens = a.Split(' ');
                var relation = _schema.Relations.SingleOrDefault(r => 
                    r.Attributes.Any(t => t.Name == attributeTokens[0]) &&
                    r.Aliases.Contains(attributeTokens[2]));
                if (relation == null)
                {
                    throw new Exception("There is a conflict between your attribute aliases and the internal schema.");
                }
                relation.Aliases.Add(attributeTokens[2]);
                newAttribute.Alias = attributeTokens[2];
                newAttribute.Name = attributeTokens[0];
            }
            else
            {
                var relation = _schema.Relations.SingleOrDefault(r => r.Attributes.Any(t => t.Name == a));
                if (relation == null)
                {
                    throw new Exception("There is a conflict between your attribute aliases and the internal schema.");
                }
                newAttribute.Alias = relation.Aliases[_queryCounter];
                newAttribute.Name = a;
            }

            return newAttribute;
        }
        
        private void PerformSimpleValidation(IEnumerable<Query> queries)
        {
            foreach (var q in queries)
            {
                var tokens = q.OriginalString.Split(' ');
                if (!tokens.Contains("select") || !tokens.Contains("from"))
                {
                    throw new Exception("Query is invalid.");
                }
            }
        }
    }
}

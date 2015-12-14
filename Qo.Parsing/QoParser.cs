namespace Qo.Parsing
{
    using QueryModel;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

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

        public MultiQuery Parse(string query)
        {
            _lastQueryString = query.ToLower().Replace("\r\n", " ");
            var result = new MultiQuery();

            try
            {
                AssembleMultiQuery(result);
                PerformSimpleValidation(result.Queries);

                foreach (var q in result.Queries)
                {
                    AssembleQuery(q);
                    _queryCounter++;
                }
            }
            catch (Exception e)
            {
                _console.WriteToConsole(e.Message);
                return null;
            }
            
            return result;
        }

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
        private SelectStatement GetSelectStatement(ICollection<string> tokens)
        {
            tokens.Remove("select");
            var statement = new SelectStatement();
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

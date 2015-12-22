namespace Qo.Testing
{
    using Qo.Parsing;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            var sqlQuery = @"
SELECT S.sname
FROM Sailors AS S
WHERE S.sid IN ((SELECT R.sid
FROM Reserves AS R, Boats AS B
WHERE R.bid = B.bid AND B.color = ‘red’)
INTERSECT
(SELECT R2.sid
FROM Reserves AS R2, Boats AS B2
WHERE R2.bid = B2.bid AND B2.color = ‘green’))
            ";
            
            var qoParser = new QoParser();
            var qoOptimizer = new QoOptimizer();
            var package = qoParser.Parse(sqlQuery.Trim());
            qoOptimizer.Optimize(package);

            Console.ReadKey();
        }
    }
}

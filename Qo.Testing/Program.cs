namespace Qo.Testing
{
    using Qo.Parsing;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            var sqlQuery = @"
SELECT sname
FROM Sailors, Boats, Reserves
WHERE Sailors.sid=Reserves.sid AND Reserves.bid=Boats.bid AND Boats.color=’red’
INTERSECT
SELECT sname
FROM Sailors, Boats, Reserves
WHERE Sailors.sid=Reserves.sid AND Reserves.bid=Boats.bid AND
Boats.color=’green’
            ";
            
            var qoParser = new QoParser();
            var qoOptimizer = new QoOptimizer();
            var package = qoParser.Parse(sqlQuery.Trim());
            qoOptimizer.Run(package);

            Console.ReadKey();
        }
    }
}

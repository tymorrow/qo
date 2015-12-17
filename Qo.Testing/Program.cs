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
WHERE S.age > (SELECT MAX (S2.age)
FROM Sailors S2
WHERE S2.rating = 10)
            ";
            
            var qoParser = new QoParser();
            qoParser.Parse(sqlQuery.Trim());

            Console.ReadKey();
        }
    }
}

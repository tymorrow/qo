namespace Qo.Testing
{
    using Qo.Parsing;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            var sqlQuery = @"
            (SELECT S.sid
            FROM Sailors AS S, Reserves AS R, Boats AS B
            WHERE S.sid=R.sid AND R.bid=B.bid AND B.color='red'
            EXCEPT
            SELECT S2.sid
            FROM Sailors AS S2, Reserves AS R2, Boats AS B2
            WHERE S2.sid=R2.sid AND R2.bid=B2.bid AND B2.color='green')
            ";
            
            var qoParser = new QoParser();
            qoParser.Parse(sqlQuery);

            Console.ReadKey();
        }
    }
}

# Qo

Qo is a naive, visualizer of query tree optimization.
I created it for a database class project.

The specifications of the project were as follows:
  - Parse an SQL query (string) to some intermediary representation
  - Convert it to its Relational Algebra (RA) equivalent
  - Convert the RA to a query tree
  - Apply numerous optimization rules to the query tree
    - Output the query tree after reach stage of optimization

Qo consists of a web application (`Qo.Web`) and a parsing library (`Qo.Parsing`). 

The web app allows me to utilize D3.js for query tree rendering while enjoying the benefits of C# on the backend.
I started Qo as a desktop app and tried rendering the query trees in the app and by outputing DOT files.
Neither method was as clean as doing it in the browser with D3.js.
Demoing this to my professor was also much easier because he didn't have to download an executable.

The parsing library contains all of the intermediary code for working with SQL, RA, query trees, and optimization.

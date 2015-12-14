(function () {
    // See if we have localStorage support
    // This is caching the last query in the browsers local storage - purely for convenience.
    // Basically if you refresh the page, you won't lose the query you just entered
    var storage, fail, uid
    try {
        uid = new Date;
        (storage = window.localStorage).setItem(uid, uid);
        fail = storage.getItem(uid) != uid;
        storage.removeItem(uid);
        fail && (storage = false);
    } catch (e) { }
    window.saveState = function () {
        if (storage) localStorage.setItem('sentence', $('#txtSQL').val());
    };
    if (storage && localStorage.getItem('sentence')) {
        $('#txtSQL').val(localStorage.getItem('sentence'));
    };

    // Function to parse SQL query
    window.parseAndFormat = function () {
        $('#alertContainer').hide();
        var settings = {
            identifierCase: $('#idCase').val(),
            functionCase: $('#fnCase').val(),
            tableCase: $('#tbCase').val()
        };

        var sqlQuery = $('#txtSQL').val();
        var formatted = SqlPrettyPrinter.format(sqlQuery, settings);
        if (typeof formatted == 'string') {

            console.log(sqlQuery);
            //var result = parser.parse(sqlQuery);
            $.ajax({
                type: "POST",
                url: '/home/submitquery',
                data: $('#txtSQL').val(),
                success: function (result) {
                    console.log(result);
                },
                error: function (xhr) {
                    console.log(xhr.responseText);
                }
            });

            $('#preFormatted').html(formatted);
            prettyPrint('#preFormatted');
            $('#preFormatted').removeClass('prettyprinted');
            window.APPDATA.formatted = formatted;

            $('#ra-expression').show();

            // BEGIN BUILDING TREE
            var width = $("#tree").parent().width();
            var height = $("#tree").parent().height();

            var cluster = d3.layout.cluster()
              .size([height - 500, width - 600]);

            var diagonal = d3.svg.diagonal()
              .projection(function (d) {
                  return [d.x, d.y];
              });

            // Set up the svg element where the drawing will go
            var svg = d3.select("#tree").append("svg")
              .attr("width", width)
              .attr("height", height)
              .append("g")
              .attr("transform", "translate(175,40)");

            // Load data from data file (requires simple http server or you'll get an error in the browser)
            d3.json("data.json", function (error, root) {
                if (error) throw error;

                var nodes = cluster.nodes(root);
                var links = cluster.links(nodes);

                // Add paths between nodes
                var link = svg.selectAll(".link")
                  .data(links)
                  .enter().append("path")
                  .attr("class", "link")
                  .attr("d", diagonal);

                // Add nodes to proper location
                var node = svg.selectAll(".node")
                  .data(nodes)
                  .enter().append("g")
                  .attr("class", "node")
                  .attr("transform", function (d) {
                      return "translate(" + d.x + "," + d.y + ")";
                  });

                // Append labels and subscripts to nodes
                node.append("text")
                  .attr("dy", function (d) {
                      return 0;
                  })
                  .style("text-anchor", function (d) {
                      return "middle";
                  })
                  .style("font-size", function (d) {
                      return "18";
                  })
                  .html(function (d) {
                      return d.name;
                  })
                  .append("tspan")
                  .attr("baseline-shift", function (d) {
                      return "sub";
                  })
                  .style("font-size", function (d) {
                      return "12";
                  })
                  .html(function (d) {
                      return d.subscript;
                  });
            });

            d3.select(self.frameElement).style("height", height + "px");

        } else if (typeof formatted == 'object') {
            var exceptionMsg = formatted.message
              .replace(/&/g, '&amp;')
              .replace(/</g, '&lt;')
              .replace(/>/g, '&gt;')
              .replace(/\n/g, '<br />');
            var alertHtml =
              ' <button type="button" class="close" data-dismiss="alert" aria-label="Close">' +
              '   <span aria-hidden="true">&times;</span>' +
              ' </button>' +
              ' <h4>Your query is invalid!</h4>' +
              ' <p>' + exceptionMsg + '</p>';
            $('#alertContainer').html(alertHtml);
            $('#alertContainer').show();
        }
    };

    window.clearSql = function () {
        $('#preFormatted').html('');
        $('#txtSQL').val('');
        $('#txtSQL').focus();
    };

    window.APPDATA = {};

})()

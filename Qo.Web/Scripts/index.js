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
        var settings = {
            identifierCase: $('#idCase').val(),
            functionCase: $('#fnCase').val(),
            tableCase: $('#tbCase').val()
        };

        var input = $('#txtSQL').val();
        var formatted = SqlPrettyPrinter.format(input, settings);
        
        $.ajax({
            type: "POST",
            url: '/home/submitquery',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            data: { sqlQuery: input },
            success: function (result) {
                console.log(result);
            },
            error: function (resp) {
                console.log(resp.responseJSON);
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
    };

    window.clearSql = function () {
        $('#preFormatted').html('');
        $('#txtSQL').val('');
        $('#txtSQL').focus();
    };

    window.addTable = function () {
        var tableName = $('#new-table').val();
        var tableHtml =
        '\
            <div class="list-group table" id="'+ tableName +'"> \
                <div class ="list-group-item name list-group-item-success"> \
                    '+ tableName +' \
                    <button class="btn btn-default btn-xs pull-right" type="button" onclick="removeTable(\''+ tableName +'\')" \
                            data-toggle="tooltip" data-placement="right" title="Delete Table"> \
                        <span class="glyphicon glyphicon-minus  "></span> \
                    </button> \
                </div> \
                <div class ="list-group-item" id="new-att"> \
                    <div class="input-group"> \
                        <input type="text" class="form-control" id="name" \
                               placeholder="Enter attribute name" /> \
                        <span class="input-group-btn"> \
                            <button class="btn btn-default" type="button" onclick="addTableAttribute(\''+ tableName +'\')" \
                                    data-toggle="tooltip" data-placement="right" title="Add Attribute"> \
                                <span class="glyphicon glyphicon-plus"></span> \
                            </button> \
                        </span> \
                    </div> \
                    <div class="input-group"> \
                        <input type="text" class="form-control" id="name" \
                               placeholder="Enter attribute name" /> \
                        <span class="input-group-btn"> \
                            <button class="btn btn-default" type="button" onclick="addTableAttribute(\''+ tableName + '\')" \
                                    data-toggle="tooltip" data-placement="right" title="Add Attribute"> \
                                <span class="glyphicon glyphicon-plus"></span> \
                            </button> \
                        </span> \
                    </div> \
                </div> \
            </div>\
        ';
        $('#schema').append(tableHtml);
        $('#new-table').val('');
    };
    
    window.addTableAttribute = function (tableId) {
        var table = $('#' + tableId).val().trim();
        var attName = $('#' + tableId + ' #new-att #name').val();
        var attId = tableId + '-' + attName;
        var attHtml =
        '\
            <div class="list-group att" id="' + attId + '"> \
                <div class ="list-group-item name"> \
                    ' + attName + ' \
                    <button class="btn btn-default btn-xs pull-right" type="button" onclick="removeTableAttribute(\'' + attId + '\')" \
                            data-toggle="tooltip" data-placement="right" title="Delete Attribute"> \
                        <span class="glyphicon glyphicon-minus"></span> \
                    </button> \
                </div> \
            </div> \
        ';
        $('#' + tableId).append(attHtml);
    };

    window.removeTable = function (id) {
        $('#'+id.trim()).remove();
    };
    window.removeTableAttribute = function (id) {
        $('#' + id.trim()).remove();
    };

    window.APPDATA = {};

})()

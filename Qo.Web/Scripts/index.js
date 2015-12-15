(function () {
    // See if we have localStorage support
    // This is caching the last query in the browsers local storage - purely for convenience.
    // Basically if you refresh the page, you won't lose the query or schema you just entered.
    var storage, fail, uid;
    try {
        uid = new Date;
        (storage = window.localStorage).setItem(uid, uid);
        fail = storage.getItem(uid) != uid;
        storage.removeItem(uid);
        fail && (storage = false);
    } catch (e) { }

    // Function to parse SQL query
    window.parseAndFormat = function () {
        var settings = {
            identifierCase: $('#idCase').val(),
            functionCase: $('#fnCase').val(),
            tableCase: $('#tbCase').val()
        };

        var input = $('#txtSQL').val();
        var formatted = SqlPrettyPrinter.format(input, settings);
        console.log(getSchema());
        $.ajax({
            type: "POST",
            url: '/home/submitquery',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            data: { SqlQuery: input, Tables: getSchema() },
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

        //$('#ra-expression').show();

        // BEGIN BUILDING TREE
        $('#tree').empty();
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
        saveState();
    };

    // Schema functions
    window.saveState = function () {
        if (storage) {
            localStorage.setItem('query', $('#txtSQL').val());
            localStorage.setItem('schema', JSON.stringify(getSchema()));
        }
    };

    window.getSchema = function () {
        var schema = [];
        $('.table-name').each(function (index, ele) {
            var tableName = ele.innerText;
            var table = { Name: tableName, Attributes: [] };
            $('.' + tableName + '-att-name').each(function (index, ele) {
                var attName = ele.innerText;
                var attType = $('#' + tableName + '-' + attName + '-type').text();
                var att = { Name: attName, Type: attType, IsFk: false };

                if ($('#' + tableName + '-' + attName + '-type').prev().is('u')) {
                    att.IsFk = true;
                }

                table.Attributes.push(att);
            });
            schema.push(table);
        });
        return schema;
    };

    window.setSchema = function (schema) {
        for (var t in schema) {
            var table = schema[t];
            $('#new-table').val(table.Name);
            addTable();
            for (var a in table.Attributes) {
                var att = table.Attributes[a];
                $('#' + table.Name + ' #new-att #name').val(att.Name);
                $('#' + table.Name + ' #new-att #type').val(att.Type);
                if (att.IsFk) {
                    $('#' + table.Name + ' #new-att #fk').prop('checked', true);
                } else {
                    $('#' + table.Name + ' #new-att #fk').prop('checked', false);
                }
                addTableAttribute(table.Name);
            }
            $('#' + table.Name + ' #new-att #name').val('');
            $('#' + table.Name + ' #new-att #type').val('int');
            $('#' + table.Name + ' #new-att #fk').prop('checked', false);
        }
        $('#new-table').val('');
    };

    window.addTable = function () {
        var tableName = $('#new-table').val();
        if (!tableName) {
            alert("Please enter a table name.");
            return;
        } else if (document.getElementById(tableName) !== null) {
            alert("This attribute name is already in use.  Please choose another.");
            return;
        }
        var tableHtml =
        '\
            <div class="list-group table" id="' + tableName + '"> \
                <div class ="list-group-item list-group-item-success"> \
                    <span class="table-name">' + tableName + '</span> \
                    <button class="btn btn-default btn-xs pull-right" type="button" onclick="removeTable(\''+ tableName + '\')" \
                            data-toggle="tooltip" data-placement="right" title="Delete Table"> \
                        <span class="glyphicon glyphicon-minus  "></span> \
                    </button> \
                </div> \
                <div class ="list-group-item" id="new-att"> \
                    <div class="input-group"> \
                        <input type="text" class="form-control" id="name" \
                               placeholder="Enter attribute name" /> \
                        <span class="input-group-addon"> \
                            <input type="checkbox" id="fk" /> fk \
                        </span> \
                    </div><br/> \
                    <div class="input-group"> \
                        <select class="form-control" id="type"> \
                            <option>int</option> \
                            <option>double</option> \
                            <option>string</option> \
                            <option>datetime</option> \
                        </select> \
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
        saveState();
    };

    window.addTableAttribute = function (tableId) {

        var attName = $('#' + tableId + ' #new-att #name').val();
        var attId = tableId + '-' + attName;
        if (document.getElementById(attId) !== null) {
            alert("This attribute name is already in use.  Please choose another.");
            return;
        }
        var attType = $('#' + tableId + ' #new-att #type').val();
        var attFk = $('#' + tableId + ' #new-att #fk').is(':checked');
        var attNameHtml = attFk
            ? '<u class="' + tableId + '-att-name" id="' + attId + '-name">' + attName + '</u>'
            : '<span class="' + tableId + '-att-name" id="' + attId + '-name">' + attName + '</span>';
        var attHtml =
        '\
            <div class ="list-group-item att" id="' + attId + '""> \
                ' + attNameHtml + ' <small class="type" id="' + attId + '-type">' + attType + '</small> \
                <button class="btn btn-default btn-xs pull-right" type="button" onclick="removeTableAttribute(\'' + attId + '\')" \
                        data-toggle="tooltip" data-placement="right" title="Delete Attribute"> \
                    <span class="glyphicon glyphicon-minus"></span> \
                </button> \
            </div> \
        ';
        $('#' + tableId).append(attHtml);
        saveState();
    };

    window.removeTable = function (id) {
        $('#' + id.trim()).remove();
        saveState();
    };

    window.removeTableAttribute = function (id) {
        $('#' + id.trim()).remove();
        saveState();
    };

    // If localStorage is enabled, populate the page
    if (storage) {
        // Get values from local storage
        var query = localStorage.getItem('query');
        var schema = localStorage.getItem('schema');
        // Set values if they exist
        if (query) $('#txtSQL').val(query);
        if (schema) {
            var parsedSchema = JSON.parse(schema);
            setSchema(parsedSchema);
        }
    }

    window.APPDATA = {};
})()

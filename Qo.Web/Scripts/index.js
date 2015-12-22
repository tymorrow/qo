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

    // Tree functions
    window.buildTree = function (ele, tree) {
        $(ele).empty();
        var width = $(ele).parent().width();
        var height = $(ele).parent().height();

        var cluster = d3.layout.cluster()
          .separation(function (a, b) { return (a.parent == b.parent ? 5 : 5) })
          .size([height - 500, width - 600]);

        var diagonal = d3.svg.diagonal().projection(function (d) {
            return [d.x, d.y];
        });

        // Set up the svg element where the drawing will go
        var svg = d3.select(ele).append("svg")
          .attr("width", width)
          .attr("height", height)
          .append("g")
          .attr("transform", "translate(175,40)");

        var nodes = cluster.nodes(tree);
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
                return d.name.trim();
            })
            .append("tspan")
            .style("baseline-shift", function (d) {
                return "sub";
            })
            .html(function (d) {
                var output = "";
                var offset = 0;
                if (d !== null && d.preSubscript !== null) {
                    output += '<tspan style="font-size: 10px" x="0" y="10" dy="0">';
                    output += '[group on: ' + d.preSubscript + ']';
                    output += '</tspan>';
                    offset = 2;
                }
                if (d !== null && d.subscript !== null) {
                    var splits = d.subscript.trim().split(/( \u2227 | \u2228 )/gi);
                    for (var s = 0; s < splits.length ; s++) {
                        output += '<tspan style="font-size: 10px" x="0" y="10" dy="' + ((s + offset) * 5) + '">';
                        if (s >= splits.length - 1) {
                            output += splits[s];
                        } else if (s < splits.length - 1) {
                            output += splits[s] + splits[s + 1];
                        } 
                        output += '</tspan>';
                        s++;
                    }
                }
                return output;
            });

        d3.select(self.frameElement).style("height", height + "px");
    }

    window.showTree = function (num) {
        $('.stage-info').each(function () { $(this).hide() });
        $('.tree-graph').each(function () { $(this).hide() });
        $('.btn').each(function () { $(this).removeClass('active') });
        $('#stage-' + num + '-info').show(); // Show info
        $('#tree-' + num).show(); // Show tree
        $('#btn-' + num).addClass('active');
    };

    // Function to parse SQL query
    window.parseAndFormat = function () {
        var settings = {
            identifierCase: $('#idCase').val(),
            functionCase: $('#fnCase').val(),
            tableCase: $('#tbCase').val()
        };

        $('output').html('');
        $('#result-area').hide();
        $('#result-area-loader').show();

        var input = $('#txtSQL').val().trim();
        if (!input) {
            alert("Empty SQL queries are not permitted.");
            $('#result-area-loader').hide();
            return;
        }
        input = input
            .replace("’", "'").replace("‘", "'")
            .replace("‘", "'").replace("’", "'")
            .replace("’", "'").replace("’", "'");
        $('#txtSQL').val(input);
        var formatted = SqlPrettyPrinter.format(input, settings);

        $.ajax({
            type: "POST",
            url: '/home/submitquery',
            contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
            data: { SqlQuery: input, Tables: getSchema() },
            success: function (result) {
                $('#result-area-loader').hide();
                console.log(result);
                $('#result-area').show();
                $('#output').html(formatted);
                prettyPrint('#output');
                $('#output').removeClass('prettyprinted');

                showTree(0);
                
                try{
                    $('#relational-algebra').html(result.RelationalAlgebra);
                    buildTree('#tree-0', result.InitialTree);
                    buildTree('#tree-1', result.Optimization1);
                    buildTree('#tree-2', result.Optimization2);
                    buildTree('#tree-3', result.Optimization3);
                    buildTree('#tree-4', result.Optimization4);
                    buildTree('#tree-5', result.Optimization5);
                }catch(e){}

                if (result.Error) {
                    $('#output').html('The server returned the following error: \n' + result.Error);
                }
            },
            error: function (resp) {
                console.log(resp.responseText);
                $('#output').text('The server had an unspecified error.  You may check the browser console output for details.');
                $('#result-area-loader').hide();
            }
        });

        window.APPDATA.formatted = formatted;
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
                var att = { Name: attName, Type: attType, IsPk: false };

                if ($('#' + tableName + '-' + attName + '-type').prev().is('u')) {
                    att.IsPk = true;
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
                if (att.IsPk) {
                    $('#' + table.Name + ' #new-att #pk').prop('checked', true);
                } else {
                    $('#' + table.Name + ' #new-att #pk').prop('checked', false);
                }
                addTableAttribute(table.Name);
            }
            $('#' + table.Name + ' #new-att #name').val('');
            $('#' + table.Name + ' #new-att #type').val('integer');
            $('#' + table.Name + ' #new-att #pk').prop('checked', false);
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
                <div class ="list-group-item list-group-item-info"> \
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
                            <input type="checkbox" id="pk" /> pk \
                        </span> \
                    </div><br/> \
                    <div class="input-group"> \
                        <select class="form-control" id="type"> \
                            <option>integer</option> \
                            <option>real</option> \
                            <option>string</option> \
                            <option>date</option> \
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
        if (!attName) {
            alert("Empty attribute names are not accepted.");
            return;
        } else if (document.getElementById(attId) !== null) {
            alert("This attribute name is already in use.  Please choose another.");
            return;
        }
        var attType = $('#' + tableId + ' #new-att #type').val();
        var attPk = $('#' + tableId + ' #new-att #pk').is(':checked');
        var attNameHtml = attPk
            ? '<u class="' + tableId + '-att-name" id="' + attId + '-name">' + attName + '</u>'
            : '<span class="' + tableId + '-att-name" id="' + attId + '-name">' + attName + '</span>';
        var attHtml =
        '\
            <div class ="list-group-item att" id="' + attId + '""> \
                ' + attNameHtml + ' <small class="type label label-info" id="' + attId + '-type" style="margin-left:5px">' + attType + '</small> \
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

    window.loadTableAttribute = function (tableId, name, pk, type) {

    };

    window.loadDefaultSchema = function () {
        $('.table').remove();
        var schema = [
            {
                Name: "Sailors",
                Attributes: [
                    { Name: "sid", IsPk: true, Type: "integer" },
                    { Name: "sname", IsPk: false, Type: "string" },
                    { Name: "rating", IsPk: false, Type: "integer" },
                    { Name: "age", IsPk: false, Type: "real" }
                ]
            },
            {
                Name: "Boats",
                Attributes: [
                    { Name: "bid", IsPk: true, Type: "integer" },
                    { Name: "bname", IsPk: false, Type: "string" },
                    { Name: "color", IsPk: false, Type: "string" }
                ]
            },
            {
                Name: "Reserves",
                Attributes: [
                    { Name: "sid", IsPk: true, Type: "integer" },
                    { Name: "bid", IsPk: true, Type: "integer" },
                    { Name: "day", IsPk: true, Type: "date" }
                ]
            }
        ];
        setSchema(schema);
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

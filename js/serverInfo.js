app.controller('ServerInfoController', ServerInfoController);

var DATA_LIMIT = 100;
var recordedData = [];

function ServerInfoController($scope, rconService, $routeParams, $interval) {
  $scope.useCharts = false;

  // TODO: move serverinfo to service
  $scope.serverinfo = {};

  $scope.playersChart = {
    options: {
      chart: {
        type: 'pieChart',
        height: 250,
        margin: {
          top: 0,
          right: 75,
          bottom: 0,
          left: 75
        },
        donut: true,
        x: function(d) {
          return d.key;
        },
        y: function(d) {
          return d.y;
        },
        yAxis: {
          axisLabel: 'Slots',
          tickFormat: function(d) {
            return d3.format('.d')(d);
          }
        },
        showLabels: true,
        pie: {
          startAngle: function(d) {
            return d.startAngle / 2 - Math.PI / 2
          },
          endAngle: function(d) {
            return d.endAngle / 2 - Math.PI / 2
          }
        },
        duration: 500
      }
    },
    data: []
  };

  $scope.performanceChart = {
    options: {
      chart: {
        type: 'multiChart',
        height: 200,
        margin: {
          top: 30,
          right: 75,
          bottom: 40,
          left: 75
        },
        color: [
          'darkred', 'yellowgreen'
        ],
        duration: 0,
        lines1: {
          duration: 0
        },
        lines2: {
          duration: 0
        },
        useInteractiveGuideline: true,
        yAxis1: {
          axisLabel: 'Framerate',
          tickFormat: function(d) {
            return d3.format(',d')(d);
          }
        },
        yAxis2: {
          axisLabel: 'Entities',
          tickFormat: function(d) {
            return d3.format(',d')(d);
          },
          // axisLabelDistance: 12
        },
        xAxis: {
          axisLabel: "Time",
          tickFormat: function(d) {
            return d3.time.format('%H:%M:%S')(new Date(d))
          }
        }
      }
    },
    data: [
      {
        key: 'Framerate',
        type: "line",
        duration: 0,
        yAxis: 1,
        values: []
      }, {
        key: 'Entities',
        type: 'line',
        duration: 0,
        yAxis: 2,
        values: []
      }
    ]
  };

  $scope.netChart = {
    options: {
      chart: {
        type: 'stackedAreaChart',
        height: 250,
        margin: {
          top: 0,
          right: 75,
          bottom: 40,
          left: 75
        },
        useVoronoi: false,
        duration: 0,
        useInteractiveGuideline: true,
        yAxis: {
          axisLabel: 'Network',
          tickFormat: function(bytes) {
            var fmt = d3.format('.0f');
            if (bytes < 1024) {
              return fmt(bytes) + 'B';
            } else if (bytes < 1024 * 1024) {
              return fmt(bytes / 1024) + 'kB';
            } else if (bytes < 1024 * 1024 * 1024) {
              return fmt(bytes / 1024 / 1024) + 'MB';
            } else {
              return fmt(bytes / 1024 / 1024 / 1024) + 'GB';
            }
          }
        },
        xAxis: {
          axisLabel: "Time",
          tickFormat: function(d) {
            return d3.time.format('%H:%M:%S')(new Date(d))
          }
        }
      }
    },
    data: [
      {
        key: 'IN',
        values: []
      }, {
        key: 'OUT',
        values: []
      }
    ]
  };

  rconService.InstallService($scope, _refresh);

  // TODO: move updateinterval to service
  var timer = $interval(_refresh, 1000);
  $scope.$on("$destroy", function() {
    $interval.cancel(timer);
    $scope.serverinfo = {};
  });

  function _refresh() {
    rconService.Request('serverinfo', $scope, function(msg) {
      _updateData(JSON.parse(msg.Message));
    });
  }

  function _updateData(data) {
    $scope.serverinfo = data;

    if ($scope.useCharts) {
      _collectChartData(data);
      _generateChartData();
    }
  }

  function _collectChartData(data) {
    // player chart
    $scope.playersChart.data = [
      {
        key: 'Queued',
        y: data.Queued
      }, {
        key: 'Joining',
        y: data.Joining
      }, {
        key: 'Players',
        y: data.Players
      }, {
        key: 'Free',
        y: (data.MaxPlayers - (data.Joining + data.Players))
      }
    ];

    recordedData.push({ts: Date.now(), data: data});
    if (recordedData.length > DATA_LIMIT) {
      recordedData = recordedData.slice(Math.max(recordedData.length - DATA_LIMIT, 1));
    }
  }

  function _generateChartData() {

    var fpsChartValues = [];
    var entChartValues = [];

    var netInChartValues = [];
    var netOutChartValues = [];

    for (var i = 0; i < recordedData.length; i++) {
      var record = recordedData[i];
      fpsChartValues.push({x: record.ts, y: record.data.Framerate});
      entChartValues.push({x: record.ts, y: record.data.EntityCount});

      netInChartValues.push({x: record.ts, y: record.data.NetworkIn});
      netOutChartValues.push({x: record.ts, y: record.data.NetworkOut});
    }

    $scope.performanceChart.data[0].values = fpsChartValues;
    $scope.performanceChart.data[1].values = entChartValues;

    $scope.netChart.data[0].values = netInChartValues;
    $scope.netChart.data[1].values = netOutChartValues;
  }

}

var app = angular.module('RconApp', ['ngRoute', 'nvd3']);

app.service('rconService', [RconService]);

app.config(function($routeProvider) {
  $routeProvider.when("/home", {Title: "Home"});
  $routeProvider.when("/:address/info", {
    Title: "Server",
    templateUrl: "html/serverInfo.html",
    Nav: true
  });
  $routeProvider.when("/:address/console", {
    Title: "Console",
    templateUrl: "html/console.html",
    Nav: true
  });
  $routeProvider.when("/:address/radiocontroller", {
    Title: "Radio",
    templateUrl: "html/radiocontroller.html",
    Nav: true
  });
  $routeProvider.when("/:address/roomslist", {
    Title: "Rooms List",
    templateUrl: "html/roomslist.html",
    Nav: true
  });
  $routeProvider.when("/:address/serversettings", {
    Title: "Settings",
    templateUrl: "html/serversettings.html",
    Nav: true
  });
  $routeProvider.when("/:address/accessSettings", {
    Title: "Access Settings",
    templateUrl: "html/accessSettings.html",
    Nav: true
  });
  $routeProvider.when("/:address/player/:userid", {
    Title: "Player Info",
    templateUrl: "html/playerInfo.html"
  });
  $routeProvider.when("/:address/room/:roomId", {
    Title: "Room Info",
    templateUrl: "html/roomInfo.html"
  });
  $routeProvider.otherwise({redirectTo: '/home'});
});

app.directive('errSrc', function() {
  return {
    link: function (scope, element, attrs) {
      var defaultSrc = attrs.src;
      var alreadyChanged = false;
      element.bind('error', function () {
        if (!alreadyChanged) {
          if (attrs.errSrc) {
            element.attr('src', attrs.errSrc);
            alreadyChanged = true;
          }
          else if (attrs.src) {
            element.attr('src', defaultSrc);
            alreadyChanged = true;
          }
        }
      });
    }
  }
});

app.controller('RconController', RconController);

function RconController($scope, $rootScope, rconService, $timeout, $route) {
  $scope.$route = $route;

  $scope.pages = $.map($route.routes, function(value, index) {
    if (value.Nav) {
      return [value];
    }
  });

  $scope.OpenLeftMenu = function() {
    $mdSidenav('left').toggle();
  };

  $scope.IsConnected = function() {
    return rconService.IsConnected();
  }

  $rootScope.Nav = function(url) {
    return url.replace(":address", rconService.Address);
  }

  $rootScope.$on('$stateChangeStart', function(next, current) {
    console.log(next);
  });

  rconService.OnOpen = function() {
    $scope.Connected = true;
    $scope.$broadcast("OnConnected");
    $scope.$digest();
    $scope.address = rconService.Address;
  }

  rconService.OnClose = function(ev) {
    $scope.$broadcast("OnDisconnected", ev);
    $scope.$digest();
  }

  rconService.OnError = function(ev) {
    $scope.$broadcast("OnConnectionError", ev);
    $scope.$digest();
  }

  rconService.OnMessage = function(msg) {
    $scope.$apply(function() {
      $scope.$broadcast("OnMessage", msg);
    });
  }

  $scope.Disconnect = function() {
    if (confirm('Do you really want to disconnect?')) {
      rconService.Disconnect();
      $scope.Connected = false;

      $scope.address = '#/home';
    }
  }
}

app.filter('SecondsToDuration', [SecondsToDuration]);

function SecondsToDuration() {
  return function(input) {
    input = parseInt(input);

    var out = "";
    var hours = Math.floor(input / 3600);
    if (input > 3600)
      out += hours + "h";

    var minutes = Math.floor(input / 60) % 60;
    if (input > 60)
      out += minutes + "m";

    var seconds = input % 60;
    out += seconds + "s";

    return out;
  }
}

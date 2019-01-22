
app.controller( 'ConnectionController', ConnectionController );

function ConnectionController( $scope, rconService, $routeParams, $timeout, $location )
{
	$scope.Address = "";
	$scope.Password = "";
	$scope.SaveConnection = true;

	$scope.connectionsLimit = 5;

	function _loadFromLocalStorage() {
		var connections = [];

		// load and parse from local storage
		if ( localStorage && localStorage.previousConnections ) {
			connections = angular.fromJson( localStorage.previousConnections );
		}

		// double check
		if ( !connections ) {
			connections = [];
		}

		return connections;
	}

	function _addWithoutDuplicates(connections, connection) {
		// prepare new array
		var filteredConnections = [];

		for(var i in connections) {
			if(connections[i].Address !== connection.Address && connections[i].Password !== connection.Password) {
				// add old connection info to our new array
				filteredConnections.push(connections[i]);
			}
		}

		// add new connection
		filteredConnections.push(connection);

		return filteredConnections;
	}

	$scope.toggleConnectionsLimit = function ()
	{
		// toggle limit between undefined and 5
		// undefined sets limit to max
		if($scope.connectionsLimit === undefined) {
			$scope.connectionsLimit = 5;
		} else {
			$scope.connectionsLimit = undefined;
		}
	}

	$scope.Connect = function ()
	{
		$scope.Address = $scope.Address.trim();
		$scope.Password = $scope.Password.trim();

		$scope.LastErrorMessage = null;
		rconService.Connect( $scope.Address, $scope.Password );

		$location.path('/' + $scope.Address + '/info');
	}

	$scope.ConnectTo = function ( c )
	{
		$scope.SaveConnection = false;
		$scope.LastErrorMessage = null;
		rconService.Connect( c.Address, c.Password );
	}

	$scope.$on( "OnDisconnected", function ( x, ev )
	{
		console.log( ev );
		$scope.LastErrorMessage = "Connection was closed - Error " + ev.code;
		$scope.$digest();
	} );

	$scope.$on( "OnConnected", function ( x, ev )
	{
		if ( $scope.SaveConnection )
		{
			// new connection to add
			var connection = { Address: $scope.Address, Password: $scope.Password, date: new Date() };

			// remove old entries and add our new connection info
			var connections = _addWithoutDuplicates(_loadFromLocalStorage(), connection);

			// push to scope and save data
			$scope.PreviousConnects = connections;
			localStorage.previousConnections = angular.toJson( $scope.PreviousConnects );
		}
	} );

	$scope.PreviousConnects = _loadFromLocalStorage();

	//
	// If a server address is passed in.. try to connect if we have a saved entry
	//
	$timeout( function ()
	{
		$scope.Address = $routeParams.address;

		//
		// If a password was passed as a search param, use that
		//
		var pw = $location.search().password;
		if ( pw )
		{
			$scope.Password = pw;
			$location.search( "password", null );
		}

		if ( $scope.Address != null )
		{
			// If we have a password (passed as a search param) then connect using that
			if ( $scope.Password != "" )
			{
				$scope.Connect();
				return;
			}

			var foundAddress = Enumerable.From( $scope.PreviousConnects ).Where( function ( x ) { return x.Address == $scope.Address } ).First();
			if ( foundAddress != null )
			{
				$scope.ConnectTo( foundAddress );
			}

		}

	}, 20 );
	
}

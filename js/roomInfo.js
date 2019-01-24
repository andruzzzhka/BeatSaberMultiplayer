
app.controller( 'RoomInfoController', RoomInfoController );

function RoomInfoController( $scope, rconService, $routeParams )
{
	$scope.roomId = $routeParams.roomId;

	$scope.info = null;
	$scope.Players = null;

	$scope.refresh = function ()
	{
		rconService.getRooms($scope, function(rooms) {

			for(var i in rooms) {
				if(rooms[i].roomId == $scope.roomId){

					// set room data
					$scope.info = rooms[i];

					return;
				}
			}

			// room not found
			// reset data to null
			$scope.info = null;
		});

		rconService.getPlayers($scope, function(players) {
			$scope.Players = players;
		}, $scope.roomId);
	}

	$scope.getRoomName = function ()
	{
		// try to find rooms name in info
		if($scope.info && $scope.info.name) {
			return $scope.info.name;
		}

		// otherwise show the id
		return "Room not found!";
	}

	rconService.InstallService( $scope, $scope.refresh )
}
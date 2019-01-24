
app.controller( 'RoomsListController',  RoomsListController );

function RoomsListController( $scope, rconService, $interval )
{
	$scope.Output = [];
	$scope.OrderBy = '-roomId';

	$scope.Refresh = function ()
	{
		rconService.getRooms($scope, function ( rooms )
		{
			$scope.Rooms = rooms;
		});
	}

	$scope.Order = function( field )
	{
		if ( $scope.OrderBy === field )
		{
			field = '-' + field;
		}

		$scope.OrderBy = field;
	}

	$scope.SortClass = function( field )
	{
		if ( $scope.OrderBy === field ) return "sorting";
		if ( $scope.OrderBy === "-" + field ) return "sorting descending";

		return null;
	}

	$scope.DestroyRoom = function ( id )
	{
		rconService.Command( 'destroyroom ' + id );

		$scope.Refresh();
	}

	$scope.CloneRoom = function ( id )
	{
		rconService.Command( 'cloneroom ' + id );

		$scope.Refresh();
	}

	$scope.ShowPresetNameModal = function ( roomId )
	{
		$scope.selectedRoom = roomId;
		$('#roomPresetNameInputModal').modal('show');
	}

	$scope.SaveRoom = function ()
	{
		presetName = $("#presetNameInput").val();
		$('#roomPresetNameInputModal').modal('hide');

		  
		rconService.Command( 'saveroom ' + $scope.selectedRoom + ' '+presetName);
		$scope.selectedRoom = 0;
	}

	$scope.ShowModalSendMessage = function ( roomId )
	{
		$scope.selectedRoom = roomId;
		$('#messageInputModal').modal('show');
	}

	$scope.SendMessage = function ()
	{
		displayTime = $("#displayTimeInput").val();
		fontSize = $("#fontSizeInput").val();
		messageText = $("#messageTextInput").val();
		$('#messageInputModal').modal('hide');

		  
		rconService.Command( 'message ' + $scope.selectedRoom+' '+displayTime+' '+fontSize+' "'+messageText+'"');
		$scope.selectedRoom = 0;
		$("#messageTextInput").val('');
	}

	rconService.InstallService( $scope, $scope.Refresh )

}
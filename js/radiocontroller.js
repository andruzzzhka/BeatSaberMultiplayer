
app.controller( 'RadioController',  RadioController );

function RadioController( $scope, rconService, $interval )
{

	$scope.songsCache = [];

	$scope.Output = [];
	$scope.QueueOrderBy = '-ConnectedSeconds';

	$scope.Refresh = function ()
	{
		rconService.getRadioInfo($scope, function ( info )
		{
			$scope.Players = info.radioClients;
			$scope.Queue = info.queuedSongs;
			$scope.CurrentSong = info.currentSong;
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

	$scope.getJpgCoverUrl = function ( key )
	{
		return "https://beatsaver.com/storage/songs/"+key.split("-")[0]+"/"+key+".jpg";
	}
	
	$scope.getPngCoverUrl = function ( key )
	{
		return "https://beatsaver.com/storage/songs/"+key.split("-")[0]+"/"+key+".png";
	}

	$scope.RemoveFromQueue = function ( id )
	{
		rconService.Command( 'radio queue remove ' + id );

		$scope.Refresh();
	}

	$scope.ClearQueue = function ()
	{
		rconService.Command('radio queue clear');

		$scope.Refresh();
	}

	$scope.AddToQueue = function ()
	{
		if($("#queueInputText").val() != "")
		{
			rconService.Command( 'radio queue add ' + $("#queueInputText").val() );

			$scope.Refresh();
		}
	}

	rconService.InstallService( $scope, $scope.Refresh )

	// var timer = $interval( function ()
	// {
	// 	//$scope.Refresh();
	// }.bind( this ), 1000 );

	//$scope.$on( '$destroy', function () { $interval.cancel( timer ) } )
}
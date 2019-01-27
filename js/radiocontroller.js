
app.controller( 'RadioController',  RadioController );

function RadioController( $scope, rconService, $routeParams )
{
	$scope.channelId = $routeParams.channelId;
	$scope.songsCache = [];

	$scope.Output = [];
	$scope.QueueOrderBy = '-ConnectedSeconds';

	$scope.Refresh = function ()
	{
		rconService.getChannelInfo($scope, $scope.channelId, function ( info )
		{
			$scope.Players = info.radioClients;
			$scope.Queue = info.queuedSongs;
			$scope.CurrentSong = info.currentSong;
			$scope.ChannelInfo = info.channelInfo;
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
		rconService.Command( 'radio '+$scope.channelId+' queue remove ' + id );

		$scope.Refresh();
	}

	$scope.ClearQueue = function ()
	{
		rconService.Command('radio '+$scope.channelId+' queue clear');

		$scope.Refresh();
	}

	$scope.AddToQueue = function ()
	{
		if($("#queueInputText").val() != "")
		{
			rconService.Command( 'radio '+$scope.channelId+' queue add ' + $("#queueInputText").val() );

			$scope.Refresh();
		}
	}

	$scope.Save = function ()
	{
		rconService.Command( 'radio '+$scope.channelId+' set name ' + $("#ChannelNameTextInput").val() );
		rconService.Command( 'radio '+$scope.channelId+' set iconurl ' + $("#ChannelIconTextInput").val() );
		rconService.Command( 'radio '+$scope.channelId+' set difficulty ' + $("#DifficultyTextInput").val() );

		$scope.Refresh();
	}

	rconService.InstallService( $scope, $scope.Refresh )
}
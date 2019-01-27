
app.controller( 'ChannelsListController',  ChannelsListController );

function ChannelsListController( $scope, rconService, $interval )
{
	$scope.Output = [];
	$scope.OrderBy = '-channelId';

	$scope.Refresh = function ()
	{
		rconService.getChannelsList($scope, function ( channels )
		{
			$scope.Channels = channels;
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

	rconService.InstallService( $scope, $scope.Refresh )

}
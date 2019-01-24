
app.controller( 'AccessSettingsController',  AccessSettingsController );

function AccessSettingsController( $scope, rconService, $interval )
{
	$scope.Output = [];

	$scope.Refresh = function ()
	{
		rconService.getAccessList($scope, function ( list )
		{
			$scope.Blacklist = list.Blacklist;
			$scope.Whitelist = list.Whitelist;			
		});
	}

	$scope.BanPlayer = function ()
	{
		player = $("#blacklistInputText").val();
		rconService.Command('blacklist add '+player);

		$scope.Refresh();
	}

	$scope.UnbanPlayer = function (player)
	{
		rconService.Command('blacklist remove '+player);

		$scope.Refresh();
	}

	$scope.WhitelistPlayer = function ()
	{
		player = $("#whitelistInputText").val();
		rconService.Command('whitelist add '+player);

		$scope.Refresh();
	}

	$scope.UnwhitelistPlayer = function (player)
	{
		rconService.Command('whitelist remove '+player);

		$scope.Refresh();
	}

	rconService.InstallService( $scope, $scope.Refresh )

}

app.controller('ServerSettingsController', ServerSettingsController);

function ServerSettingsController($scope, rconService, $interval) {
	$scope.Output = [];

	$scope.Refresh = function () {
		rconService.getSettings($scope, function (settings) {
			$scope.Server = [];
			$scope.Radio = [];
			$scope.Access = [];

			for (key in settings.Server) {
				var option = {
					name: key,
					value: settings.Server[key]
				}
				$scope.Server.push(option);
			}

			for (key in settings.Radio) {
				var option = {
					name: key,
					value: settings.Radio[key]
				}
				$scope.Radio.push(option);
			}

			for (key in settings.Access) {
				if (!Array.isArray(settings.Access[key])) {
					var option = {
						name: key,
						value: settings.Access[key]
					}
					$scope.Access.push(option);
				}
			}
		});
	}

	$scope.Save = function () {
		settings = {
			Server: {},
			Radio: {},
			Access: {}
		};

		for (key in $scope.Server) {
			settings.Server[$scope.Server[key].name] = $("#OptionServer" + $scope.Server[key].name).val();
		}

		for (key in $scope.Radio) {
			settings.Radio[$scope.Radio[key].name] = $("#OptionRadio" + $scope.Radio[key].name).val();
		}

		for (key in $scope.Access) {
			settings.Access[$scope.Access[key].name] = $("#OptionAccess" + $scope.Access[key].name).val();
		}

		rconService.Command('setsettings "' + JSON.stringify(settings) + '"');
	}

	rconService.InstallService($scope, $scope.Refresh)

}
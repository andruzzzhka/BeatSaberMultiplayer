
app.controller( 'ConsoleController', ConsoleController );

function ConsoleController( $scope, rconService, $timeout )
{
	$scope.Output = [];
	$scope.commandHistory = [];
	$scope.commandHistoryIndex = 0;

	$scope.KeyUp = function (event)
	{
		switch(event.keyCode) {
			
			// Arrow Key Up
			case 38:

				// rotate through commandHistory
				$scope.commandHistoryIndex--;
				if($scope.commandHistoryIndex < 0) {
					$scope.commandHistoryIndex = $scope.commandHistory.length;
				}

				// set command from history 
				if($scope.commandHistory[$scope.commandHistoryIndex]) {
					$scope.Command = $scope.commandHistory[$scope.commandHistoryIndex];
				}
				
				break;

			// Arrow Key Down
			case 40:
			
				// rotate through commandHistory
				$scope.commandHistoryIndex++;
				if($scope.commandHistoryIndex >= $scope.commandHistory.length) {
					$scope.commandHistoryIndex = 0;
				}

				// set command from history 
				if($scope.commandHistory[$scope.commandHistoryIndex]) {
					$scope.Command = $scope.commandHistory[$scope.commandHistoryIndex];
				}
				
				break;

			default:
				// reset command history index
				$scope.commandHistoryIndex = $scope.commandHistory.length;
				break;
		}
	}

	$scope.SubmitCommand = function ()
	{
		$scope.OnMessage( { Message: $scope.Command, Type: 'Command' } );

		$scope.commandHistory.push($scope.Command);

		rconService.Command( $scope.Command, 1 );
		$scope.Command = "";
		$scope.commandHistoryIndex = 0;
	}
	$scope.$on( "OnMessage", function ( event, msg ) { $scope.OnMessage( msg ); } );

	$scope.OnMessage = function( msg )
	{
		
		if ( msg.Message.startsWith( "[rcon] " ) ) {
			return;	
		}

		switch(msg.Type) {
			case 'Generic':
			case 'Log':
			case 'Error':
			case 'Warning':
				$scope.addOutput(msg);
				break;

			default: 
				console.log( msg );
				return;
		}
	}

	$scope.ScrollToBottom = function()
	{
		var element = $( "#ConsoleController .Output" );

		$timeout( function() {
			element.scrollTop( element.prop('scrollHeight') );
		}, 50 );
	}

	$scope.isOnBottom = function()
	{
		// get jquery element
		var element = $( "#ConsoleController .Output" );

		// height of the element
		var height = element.height();

		// scroll position from top position
		var scrollTop = element.scrollTop();

		//  full height of the element
		var scrollHeight = element.prop('scrollHeight');

		if((scrollTop + height) > (scrollHeight - 10)) {
			return true;
		}

		return false;
	}

	//
	// Calls console.tail - which returns the last 128 entries from the console.
	// This is then added to the console
	//
	$scope.GetHistory = function ()
	{
		rconService.Request( "console.tail 128", $scope, function ( msg )
		{
			var messages = JSON.parse( msg.Message );

			messages.forEach( function ( msg ) {
			 $scope.OnMessage( msg ); 
			});

			$scope.ScrollToBottom();
		} );
	}

	$scope.addOutput = function (msg)
	{
		msg.Class = msg.Type;
		$scope.Output.push( msg );

		if($scope.isOnBottom()) {
			$scope.ScrollToBottom();
		}
	}

	rconService.InstallService( $scope, $scope.GetHistory )
}

app.controller( 'ChatController', ChatController );

function ChatController( $scope, rconService, $timeout )
{
	$scope.Output = [];

	$scope.SubmitCommand = function ()
	{
		rconService.Command( "say " + $scope.Command, 1 );
		$scope.Command = "";
	}

	$scope.$on( "OnMessage", function ( event, msg )
	{
		if ( msg.Type !== "Chat" ) return;

		$scope.OnMessage( JSON.parse( msg.Message ) );
	});

	$scope.OnMessage = function( msg )
	{
		msg.Message = stripHtml(msg.Message);
		$scope.Output.push( msg );
		
		if($scope.isOnBottom()) {
			$scope.ScrollToBottom();
		}
	}

	$scope.ScrollToBottom = function()
	{
		var element = $( "#ChatController .Output" );

		$timeout( function() {
			element.scrollTop( element.prop('scrollHeight') );
		}, 50 );
	}

	$scope.isOnBottom = function()
	{
		// get jquery element
		var element = $( "#ChatController .Output" );

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
	// Calls console.tail - which returns the last 256 entries from the console.
	// This is then added to the console
	//
	$scope.GetHistory = function ()
	{
		rconService.Request( "chat.tail 512", $scope, function ( msg )
		{
			var messages = JSON.parse( msg.Message );

			messages.forEach( function ( message ) {
			 $scope.OnMessage( message ); 
			});

			$scope.ScrollToBottom();
		} );
	}

	rconService.InstallService( $scope, $scope.GetHistory )
}

function stripHtml(html)
{
	if (html == null) return "";
	var tmp = document.createElement("div");
	tmp.innerHTML = html;
	return tmp.textContent || tmp.innerText || "";
}
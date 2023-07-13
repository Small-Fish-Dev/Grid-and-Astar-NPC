using System.Threading;

namespace GridAStar;

public partial class Grid
{

	/*
	[ConCmd.Server( "RegenerateMainGrid" )]
	public async static void RegenerateMainGrid()
	{
		BroadcastMainGrid();
		await Grid.Create( Vector3.Zero, Game.PhysicsWorld.Body.GetBounds(), new Rotation() ); // Initialize the main grid
	}

	[ClientRpc]
	public async static void BroadcastMainGrid()
	{
		await Grid.Create( Vector3.Zero, Game.PhysicsWorld.Body.GetBounds(), new Rotation() ); // Initialize the main grid
	}

	[ConCmd.Server( "CreateGrid" )]
	public async static void CreateGrid( string identifier )
	{
		var caller = ConsoleSystem.Caller;
		await Grid.Create( caller.Position, new BBox( -200f, 200f ), new Rotation(), identifier );
	}

	[ConCmd.Server( "LoadGrid" )]
	public async static void LoadGrid( string identifier = "main" )
	{
		await Grid.Load( identifier );
	}

	[ConCmd.Server( "DeleteGrid" )]
	public static void DeleteGrid( string identifier = "main" )
	{
		DeleteSave( identifier );
	}

	[ConCmd.Server( "TestOccupancy" )]
	public static void OccupancyTest()
	{
		foreach ( var grid in Grid.Grids )
			grid.Value.CheckOccupancy( "BlockGrid" );
	}*/

	[ConCmd.Server( "TestPath" )]
	public async static void TestPath()
	{
		var caller = ConsoleSystem.Caller.Pawn as ModelEntity;

		var builder = AStarPathBuilder.From( Grid.Main );

		var computedPath = await builder.RunInParallel( Grid.Main.GetCellInArea( caller.Position, Grid.Main.WidthClearance ), Grid.Main.GetCell( Vector3.Zero, false ), new CancellationTokenSource() );

		for ( int i = 0; i < computedPath.Nodes.Count(); i++ )
		{
			var node = computedPath.Nodes[i];
			node.Current.Draw( Color.Red, 3f, false );
			DebugOverlay.Text( i.ToString(), node.EndPosition, duration: 3 );

			if ( i < computedPath.Nodes.Count() - 1 )
				DebugOverlay.Line( node.EndPosition, computedPath.Nodes[i + 1].EndPosition, 3f );
		}
	}


	[ConCmd.Server( "TestLOS" )]
	public static void TestLOS()
	{
		var caller = ConsoleSystem.Caller.Pawn as ModelEntity;

		var standingCell = Grid.Main.GetNearestCell( caller.Position );
		var forwardCell = Grid.Main.GetNearestCell( caller.Position + caller.Rotation.Forward * Grid.Main.CellSize * 6f );

		Log.Error( Grid.Main.LineOfSight( standingCell, forwardCell, debugShow: true ) );
	}
	/*
	[ConCmd.Server( "StressPath" )]
	public static async void StressPath( int runs = 100, int seed = 42069 )
	{
		var firstPlayer = Game.Clients.FirstOrDefault();
		if ( firstPlayer is null )
		{
			Log.Warning( "There needs to be at least one player in the server for this to be used" );
			return;
		}

		var random = new Random( seed );
		var times = new double[runs];
		var paths = new ImmutableArray<Cell>[runs];

		var startingPosition = firstPlayer.Position;
		for ( var i = 0; i < runs; i++ )
		{
			var targetPosition = new Vector3( random.Float( -1, 1 ), random.Float( -1, 1 ), random.Float( -1, 1 ) ) * 3000;

			var startingCell = Grid.Main.GetCell( startingPosition, false );
			var targetCell = Grid.Main.GetCell( targetPosition, false );

			var timestamp = Stopwatch.GetTimestamp();
			var path = await Grid.Main.ComputePathParallel( startingCell, targetCell );
			var elapsed = Stopwatch.GetElapsedTime( timestamp );

			times[i] = elapsed.TotalMilliseconds;
			paths[i] = path;
			Log.Info( $"Finished run #{i + 1}" );
		}

		Log.Info( $"-- {runs} Runs --" );
		Log.Info( $"Fastest Run: {times.Min()}ms" );
		Log.Info( $"Slowest Run: {times.Max()}ms" );
		Log.Info( $"Average: {times.Average()}ms" );
		Log.Info( $"Total Time: {times.Sum()}ms" );

		foreach ( var path in paths )
		{
			for ( var i = 0; i < path.Length; i++ )
			{
				path[i].Draw( Color.Red, 10, false );
				DebugOverlay.Text( i.ToString(), path[i].Position, duration: 10 );
			}
		}
	}*/

	[ConCmd.Server( "gridastar_regenerate" )]
	public static void RegenerateGrids( bool save = false, bool compress = false )
	{
		RegenerateOnClient( To.Everyone );

		GameTask.RunInThreadAsync( async () =>
		{
			var allGrids = Entity.All.OfType<HammerGrid>().ToList();

			foreach ( var grid in allGrids )
			{
				var newGrid = await grid.CreateFromSettings();
				if ( save )
					await newGrid.Save( compress );
			}

			Event.Run( Grid.LoadedAll );
		} );
	}

	[ClientRpc]
	public static void RegenerateOnClient()
	{
		GameTask.RunInThreadAsync( async () =>
		{
			var allGrids = Entity.All.OfType<HammerGrid>().ToList();

			foreach ( var grid in allGrids )
			{
				await grid.CreateFromSettings();
			}

			Event.Run( Grid.LoadedAll );
		} );
	}

	static TimeUntil nextDraw = 0f;

	[Event.Debug.Overlay( "displaygridclient", "[Client] Display Grid", "grid_on" )]
	public static void GridOverlayClient()
	{
		if ( !Game.IsClient ) return;

		if ( nextDraw )
		{
			foreach ( var grid in Grids )
				foreach ( var cell in grid.Value.AllCells )
				{
					var position = cell.Position.ToScreen();
					if ( position.z < 0f ) continue;
					if ( position.x < 0f || position.x > 1f ) continue;
					if ( position.y < 0f || position.y > 1f ) continue;

					cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.1f, true, false, cell.Occupied );

					foreach ( var connection in cell.CellConnections.Select( ( value, index ) => new { index, value } ) )
					{
						var offset = Vector3.Up * 3f * ( connection.index + 1 );
						DebugOverlay.Text( $"{connection.value.MovementTag}", cell.Position + offset, 1.1f, 500f );

						DebugOverlay.Line( cell.Position + offset, connection.value.EndPosition, 1.1f );
					}
				}

			nextDraw = 1f;
		}
	}
	/*
	[Event.Debug.Overlay( "displaygridclientnodepth", "[Client] Display Grid (No depth)", "grid_on" )]
	public static void GridOverlayClientNoDepth()
	{
		if ( !Game.IsClient ) return;

		if ( nextDraw )
		{
			foreach ( var grid in Grids )
				foreach ( var cellStack in grid.Value.CellStacks )
					foreach ( var cell in cellStack.Value )
					{
						var position = cell.Position.ToScreen();
						if ( position.z < 0f ) continue;
						if ( position.x < 0f || position.x > 1f ) continue;
						if ( position.y < 0f || position.y > 1f ) continue;

						cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.1f, false, false, cell.Occupied );
					}

			nextDraw = 1f;
		}
	}

	[Event.Debug.Overlay( "displaygridserver", "[Server] Display Grid", "grid_on" )]
	public static void GridOverlayServer()
	{
		if ( !Game.IsServer ) return;

		if ( nextDraw )
		{
			foreach ( var grid in Grids )
				foreach ( var cellStack in grid.Value.CellStacks )
					foreach ( var cell in cellStack.Value )
						cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.1f, true, false, cell.Occupied );

			nextDraw = 1f;
		}
	}

	[Event.Debug.Overlay( "displaygridservernodepth", "[Server] Display Grid (No depth)", "grid_on" )]
	public static void GridOverlayServerNoDepth()
	{
		if ( !Game.IsServer ) return;

		if ( nextDraw )
		{
			foreach ( var grid in Grids )
				foreach ( var cellStack in grid.Value.CellStacks )
					foreach ( var cell in cellStack.Value )
						cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.1f, false, false, cell.Occupied );

			nextDraw = 1f;
		}
	}*/
}



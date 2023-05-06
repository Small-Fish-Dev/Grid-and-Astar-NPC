using System.Collections.Immutable;

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
	}

	[ConCmd.Server( "TestPath" )]
	public async static void TestPath()
	{
		foreach ( var client in Game.Clients )
		{
			var cells = await Grid.Main.ComputePathParallel( client.Pawn.Position, new Vector3( -1577.57f, - 758.34f, 128.00f ), true );

			for ( int i = 0; i < cells.Length; i++ )
			{
				cells[i].Draw( Color.Red, 15f, false );
				DebugOverlay.Text( i.ToString(), cells[i].Position, duration: 3 );
			}
		}
	}

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

	[Event.Debug.Overlay( "displaygrid", "Display Grid", "grid_on" )]
	public static void GridOverlay()
	{
		if ( !Game.IsServer ) return;

		if ( Time.Tick % 60 == 0 )
			foreach ( var grid in Grids )
				foreach ( var cellStack in grid.Value.Cells )
					foreach ( var cell in cellStack.Value )
					{
						cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.5f, true, false, cell.Occupied );
					}
	}
}



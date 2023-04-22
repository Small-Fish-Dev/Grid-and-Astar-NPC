namespace GridAStar;

public partial class Grid
{
	[ConCmd.Server( "RegenerateMainGrid" )]
	public async static void RegenerateMainGrid()
	{
		BroadcastMainGrid();
		await Grid.Create( Game.PhysicsWorld.Body.GetBounds() ); // Initialize the main grid
	}

	[ClientRpc]
	public async static void BroadcastMainGrid()
	{
		await Grid.Create( Game.PhysicsWorld.Body.GetBounds() ); // Initialize the main grid
	}

	[ConCmd.Server( "CreateGrid" )]
	public async static void CreateGrid( string identifier )
	{
		var caller = ConsoleSystem.Caller;
		await Grid.Create( new BBox( caller.Position - 200f, caller.Position + 200f ), identifier );
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

	[Event.Debug.Overlay( "displaygrid", "Display Grid", "grid_on" )]
	public static void GridOverlay()
	{
		if ( !Game.IsClient ) return;

		if ( Time.Tick % 10 == 0 )
		{
			foreach ( var grid in Grids )
			{
				foreach ( var cellStack in grid.Value.Cells )
				{
					foreach ( var cell in cellStack.Value )
					{
						if ( cell.Position.DistanceSquared( Game.LocalPawn.Position ) < 500000f )
							cell.Draw( cell.Occupied ? Color.Red : Color.White, 1f, true );
					}
				}
			}
		}
	}

	[ConCmd.Server( "TestPath" )]
	public async static void TestPath()
	{
		foreach ( var client in Game.Clients )
		{
			var cells = await Grid.Main.ComputePathParallel( Grid.Main, client.Pawn.Position, Vector3.Random * 3000f, true );

			for ( int i = 0; i < cells.Count; i++ )
			{
				cells[i].Draw( Color.Red, 3, false );
				DebugOverlay.Text( i.ToString(), cells[i].Position, duration: 3 );
			}
		}
	}
}



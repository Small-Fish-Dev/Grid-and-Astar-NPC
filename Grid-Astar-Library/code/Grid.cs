global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Threading.Tasks;

namespace GridAStar;

public static partial class GridSettings
{
	public const float DEFAULT_STANDABLE_ANGLE = 45f;	// How steep the terrain can be on a cell before it gets discarded
	public const float DEFAULT_CELL_SIZE = 16f;         // How large each cell will be in hammer units
	public const float DEFAULT_HEIGHT_CLEARANCE = 72f;	// How much vertical space there should be
	public const bool DEFAULT_WORLD_ONLY = true;		// Will it only hit the world or also entities
}

public partial class Grid
{
	public static Grid Main { get; set; }           // The default world grid

	public string Identifier { get; set; }
	public Dictionary<IntVector2, List<Cell>> Cells { get; internal set; } = new();
	public BBox Bounds { get; set; }
	public float StandableAngle { get; set; }
	public float CellSize { get; set; }
	public float HeightClearance { get; set; }

	public Grid() { }

	public Grid( string identifier ) : this()
	{
		Identifier = identifier;
		Event.Register( this );
	}

	~Grid()
	{
		Event.Unregister( this );
	}

	/// <summary>
	/// Find the cell below the position given, findNearest = true will loop through all cells so make sure to only use it for finding a destination
	/// </summary>
	/// <param name="position"></param>
	/// <param name="findNearest"></param>
	/// <returns></returns>
	public Cell GetCell( Vector3 position, bool findNearest = false )
	{
		var coordinates2D = position.ToIntVector2( CellSize );
		var cellsAtCoordinates = Cells.GetValueOrDefault( coordinates2D );

		if ( cellsAtCoordinates == null )
		{
			if ( !findNearest )
				return null; // Escape if there's no cell and we don't want to find the closest one

			var minDistance = float.MaxValue;
			Cell currentCell = null;

			foreach ( var cellStacks in Cells )
			{
				foreach ( var cell in cellStacks.Value )
				{
					var curDistance = cell.Position.Distance( position );
					if ( curDistance < minDistance )
					{
						currentCell = cell;
						minDistance = curDistance;
					}
				}
			}

			return currentCell; // Loop through all cells and return the closest if findNearest is true
		}
		if ( cellsAtCoordinates.Count == 1 ) return cellsAtCoordinates[0];

		// Get the nearest cell which is under the given coordinates (Even if a cell above is closer, it's not useable)
		var nearestCell = cellsAtCoordinates.Where( x => x.Position.z - CellSize / 2 < position.z )
			.OrderBy( x => x.Position.z )
			.FirstOrDefault();

		return nearestCell;
	}

	/// <summary>
	/// Find exact cell with the coordinates provided
	/// </summary>
	/// <param name="coordinates"></param>
	/// <param name ="height"></param>
	/// <returns></returns>
	public Cell GetCell( IntVector2 coordinates, float height )
	{
		var cellsAtCoordinates = Cells.GetValueOrDefault( coordinates );

		if ( cellsAtCoordinates == null ) return null;

		float maxHeight = CellSize * MathF.Tan( MathX.DegreeToRadian( StandableAngle ) );

		foreach ( var cell in cellsAtCoordinates )
			if ( cell.Position.z - maxHeight <= height )
				return cell;

		return null;
	}

	public void AddCell( Cell cell )
	{
		if ( cell == null ) return;
		var coordinates = cell.GridPosition;
		if ( !Cells.ContainsKey( coordinates ) )
			Cells.Add( coordinates, new List<Cell>() { cell } );
		else
			Cells[coordinates].Add( cell );
	}

	/// <summary>
	/// Creates a new grid and generates cells within the bounds given
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="bounds"></param>
	/// <param name="standableAngle"></param>
	/// <param name="cellSize"></param>
	/// <param name="heightClearance"></param>
	/// <param name="worldOnly"></param>
	/// <returns></returns>
	public static Grid Initialize( BBox bounds, string identifier = "main", float standableAngle = GridSettings.DEFAULT_STANDABLE_ANGLE, float cellSize = GridSettings.DEFAULT_CELL_SIZE, float heightClearance = GridSettings.DEFAULT_HEIGHT_CLEARANCE, bool worldOnly = GridSettings.DEFAULT_WORLD_ONLY )
	{
		Stopwatch traceDownWatch = new Stopwatch();
		Stopwatch totalWatch = new Stopwatch();
		traceDownWatch.Start();
		totalWatch.Start();

		Log.Info( "Initializing grid..." );

		var currentGrid = new Grid( identifier );
		currentGrid.Bounds = bounds;
		currentGrid.StandableAngle = standableAngle;
		currentGrid.CellSize = cellSize;
		currentGrid.HeightClearance = heightClearance;

		var minimumGrid = bounds.Mins.ToIntVector2( cellSize );
		var maximumGrid = bounds.Maxs.ToIntVector2( cellSize );
		var minHeight = bounds.Mins.z;
		var maxHeight = bounds.Maxs.z;

		Log.Info( $"Casting {(maximumGrid.y - minimumGrid.y) * (maximumGrid.x - minimumGrid.x)} cells. [{maximumGrid.x - minimumGrid.x}x{maximumGrid.y - minimumGrid.y}]" );

		for ( int column = minimumGrid.y; column <= maximumGrid.y; column++ )
		{
			for ( int row = minimumGrid.x; row <= maximumGrid.x; row++ )
			{
				var startPosition = new Vector3( row * cellSize, column * cellSize, maxHeight + heightClearance + cellSize);
				var endPosition = new Vector3( row * cellSize, column * cellSize, minHeight - heightClearance - cellSize );
				var checkBBox = new BBox( new Vector3( -cellSize / 2f, -cellSize / 2f, 0f ), new Vector3( cellSize / 2f, cellSize / 2f, 1f ) );
				var positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition );

				if ( worldOnly )
					positionTrace.WorldOnly();
				else
					positionTrace.WorldAndEntities();

				var positionResult = positionTrace.Run();

				while ( positionResult.Hit )
				{
					if ( Vector3.GetAngle( Vector3.Up, positionResult.Normal ) <= standableAngle )
					{
						var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition, worldOnly );

						if ( newCell != null )
							currentGrid.AddCell( newCell );
					}

					var checkPosition = positionResult.HitPosition + Vector3.Down * heightClearance;

					while ( Sandbox.Trace.TestPoint( checkPosition, radius: cellSize / 2f ) )
						checkPosition += Vector3.Down * heightClearance;

					positionTrace = Sandbox.Trace.Box( checkBBox, checkPosition, endPosition );

					if ( worldOnly )
						positionTrace.WorldOnly();
					else
						positionTrace.WorldAndEntities();

					positionResult = positionTrace.Run();
				}
			}
		}

		traceDownWatch.Stop();
		Log.Info( $"TraceDown completed in {traceDownWatch.ElapsedMilliseconds}ms" );

		totalWatch.Stop();
		Log.Info( $"Grid initialized in {totalWatch.ElapsedMilliseconds}ms" );

		return currentGrid;
	}

	/// <summary>
	/// Return a list of all cells in this grid that do not have 8 neighbours
	/// </summary>
	/// <returns></returns>
	public List<Cell> FindOuterCells()
	{
		var outerCells = new List<Cell>();

		foreach ( var cellStack in Cells )
			foreach ( var cell in cellStack.Value )
				if ( cell.GetNeighbours().Count < 8 )
					outerCells.Add( cell );

		return outerCells;
	}

	[ConCmd.Server( "RegenerateMainGrid" )]
	public static void RegenerateMainGrid()
	{
		Main = Grid.Initialize( Game.PhysicsWorld.Body.GetBounds() );
		Main.Save();
	}

	[ConCmd.Server( "LoadGrid" )]
	public static void LoadGrid( string identifier = "main" )
	{
		if ( identifier == "main" )
		{
			Main = Grid.Load( "main" );
		}
	}

	[ConCmd.Server( "DisplayGrid" )]
	public static void DisplayGrid( float time )
	{
		foreach ( var cellStack in Main.Cells )
		{
			foreach ( var cell in cellStack.Value )
			{
				cell.Draw( cell.Occupied ? Color.Red : Color.White, time, true, false );
			}
		}
	}

	[Event.Debug.Overlay( "displaygrid", "Display Grid", "grid_on" )]
	public static void GridOverlay()
	{
		if ( !Game.IsServer ) return;
		if ( Grid.Main == null ) return;

		foreach( var client in Game.Clients )
		{
			if ( client.Pawn != null )
			{
				if ( Time.Tick % 10 == 0 )
				{
					foreach ( var cellStack in Main.Cells )
					{
						foreach ( var cell in cellStack.Value )
						{
							if ( cell.Position.DistanceSquared( client.Pawn.Position ) < 500000f )
								cell.Draw( cell.Occupied ? Color.Red : Color.White, 1f, false );
						}
					}
				}
			}
		}
	}

}



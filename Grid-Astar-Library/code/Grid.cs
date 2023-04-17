global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;

namespace GridAStar;

public static class GridSettings
{
	public const float DEFAULT_STANDABLE_ANGLE = 45f;	// How steep the terrain can be on a cell before it gets discarded
	public const float DEFAULT_CELL_SIZE = 16f;         // How large each cell will be in hammer units
}

public partial class Grid
{
	public static Grid Main { get; set; }           // The default world grid

	public Dictionary<IntVector2, List<Cell>> Cells { get; internal set; } = new();
	public float StandableAngle { get; set; }
	public float CellSize { get; set; }

	public Grid()
	{
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
		{
			if ( cell.Position.z - maxHeight <= height )
				return cell;
		}

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
	/// <param name="bounds"></param>
	/// <param name="standableAngle"></param>
	/// <param name="cellSize"></param>
	/// <param name="expandSearch"></param>
	/// <param name="worldOnly"></param>
	/// <returns></returns>
	public static Grid InitializeGrid( BBox bounds, float standableAngle = GridSettings.DEFAULT_STANDABLE_ANGLE, float cellSize = GridSettings.DEFAULT_CELL_SIZE, bool expandSearch = true, bool worldOnly = true )
	{
		Stopwatch traceDownWatch = new Stopwatch();
		Stopwatch totalWatch = new Stopwatch();
		traceDownWatch.Start();
		totalWatch.Start();

		Log.Info( "Initializing grid..." );

		var currentGrid = new Grid();
		currentGrid.StandableAngle = standableAngle;
		currentGrid.CellSize = cellSize;

		var minimumGrid = bounds.Mins.ToIntVector2( cellSize );
		var maximumGrid = bounds.Maxs.ToIntVector2( cellSize );
		var minHeight = bounds.Mins.z;
		var maxHeight = bounds.Maxs.z;

		Log.Info( $"Casting {(maximumGrid.y - minimumGrid.y) * (maximumGrid.x - minimumGrid.x)} cells. [{maximumGrid.x - minimumGrid.x}x{maximumGrid.y - minimumGrid.y}]" );

		for ( int column = minimumGrid.y; column <= maximumGrid.y; column++ )
		{
			for ( int row = minimumGrid.x; row <= maximumGrid.x; row++ )
			{
				var startPosition = new Vector3( row * cellSize, column * cellSize, maxHeight );
				var endPosition = new Vector3( row * cellSize, column * cellSize, minHeight );
				var positionTrace = Sandbox.Trace.Box( new BBox( new Vector3( -cellSize / 2f, -cellSize / 2f, 0f ), new Vector3( cellSize / 2f, cellSize / 2f, 1f ) ), startPosition, endPosition );

				if ( worldOnly )
					positionTrace.WorldOnly();
				else
					positionTrace.WorldAndEntities();

				var positionResult = positionTrace.Run();

				if ( positionResult.Hit )
				{
					if ( Vector3.GetAngle( Vector3.Up, positionResult.Normal ) <= standableAngle )
					{
						var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition, worldOnly );

						if ( newCell != null )
							currentGrid.AddCell( newCell );
					}
				}
			}
		}

		traceDownWatch.Stop();
		Log.Info( $"TraceDown completed in {traceDownWatch.ElapsedMilliseconds}ms" );

		if ( expandSearch )
		{
			Log.Info( "Searching for internal cells..." );

			Stopwatch internalWatch = new Stopwatch();
			internalWatch.Start();

			var cellsFound = currentGrid.SearchBorders();

			internalWatch.Stop();
			Log.Info( $"Found {cellsFound} internal cells in {internalWatch.ElapsedMilliseconds}ms" );
		}

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

	/// <summary>
	/// Finds and adds all the cells missing from the initial search due to overhang terrain or confined spaces
	/// </summary>
	public int SearchBorders()
	{
		int cellsFound = 0;
		Stopwatch outerWatch = new Stopwatch();
		outerWatch.Start();
		var cellsToCheck = FindOuterCells();
		outerWatch.Stop();
		Log.Info( $"Finished finding outer cells in {outerWatch.ElapsedMilliseconds}ms" );
		while ( cellsToCheck.Count > 0 )
		{
			var newCellsToCheck = new List<Cell>();

			foreach ( var currentCell in cellsToCheck )
			{
				for ( int y = -1; y <= 1; y++ )
				{
					for ( int x = -1; x <= 1; x++ )
					{
						if ( x == 0 && y == 0 ) continue;

						var cellFound = GetCell( new IntVector2( currentCell.GridPosition.x + x, currentCell.GridPosition.y + y ), currentCell.Position.z );

						if ( cellFound != null ) continue;

						var testPosition = currentCell.Position + Vector3.Forward * x * CellSize + Vector3.Left * y * CellSize;
						var newCell = Cell.TryCreate( this, testPosition );

						if ( newCell != null )
						{
							AddCell( newCell );
							newCellsToCheck.Add( newCell );
							cellsFound++;
						}
					}
				}
			}

			cellsToCheck.Clear();
			cellsToCheck = new List<Cell>( newCellsToCheck );
		}

		return cellsFound;
	}

	[ConCmd.Server( "RegenerateMainGrid" )]
	public static void RegenerateMainGrid()
	{
		Main = Grid.InitializeGrid( Game.PhysicsWorld.Body.GetBounds(), expandSearch: false );
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

		if ( Time.Tick % 60 == 0 )
		{
			foreach ( var cellStack in Main.Cells )
			{
				foreach ( var cell in cellStack.Value )
				{
					cell.Draw( cell.Occupied ? Color.Red : Color.White, 1.5f, true );
				}
			}
		}

	}

}



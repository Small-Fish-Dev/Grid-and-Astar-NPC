﻿global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Threading.Tasks;

namespace GridAStar;

// Set STEP_SIZE or WIDTH_CLEARANCE to 0 to disable them ( Faster grid generation )
public static partial class GridSettings
{
	public const float DEFAULT_STANDABLE_ANGLE = 40f;   // How steep the terrain can be on a cell before it gets discarded
	public const float DEFAULT_STEP_SIZE = 12f;			// How big steps can be on a cell before it gets discarded
	public const float DEFAULT_CELL_SIZE = 16f;         // How large each cell will be in hammer units
	public const float DEFAULT_HEIGHT_CLEARANCE = 72f;  // How much vertical space there should be
	public const float DEFAULT_WIDTH_CLEARANCE = 24f;	// How much horizontal space there should be
	public const bool DEFAULT_WORLD_ONLY = true;		// Will it only hit the world or also static entities
}

public partial class Grid
{
	public static Grid Main
	{
		get 
		{
			if ( Grids.ContainsKey( "main" ) )
				return Grids["main"];
			else
				return null;
		}
		set 
		{
			if ( Grids.ContainsKey( "main" ) )
				Grids["main"] = value;
			else
				Grids.Add( "main", value );
		}
	}

	public static Dictionary<string, Grid> Grids { get; set; } = new();

	public string Identifier { get; set; }
	public Dictionary<IntVector2, List<Cell>> Cells { get; internal set; } = new();
	public Vector3 Position { get; set; }
	public BBox Bounds { get; set; }
	public Rotation Rotation { get; set; }
	public float StandableAngle { get; set; }
	public float StepSize { get; set; }
	public float CellSize { get; set; }
	public float HeightClearance { get; set; }
	public float WidthClearance { get; set; }
	public bool WorldOnly { get; set; }

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
	/// Find the cell below the position given
	/// </summary>
	/// <param name="position"></param>
	/// <param name="onlyBelow"></param>
	/// <returns></returns>
	public Cell GetCell( Vector3 position, bool onlyBelow = true )
	{
		var coordinates2D = position.ToIntVector2( CellSize );
		var cellsAtCoordinates = Cells.GetValueOrDefault( coordinates2D );

		// If no cells were found at the coordinates
		if ( cellsAtCoordinates == null )
		{
			var closestCoordinates = new IntVector2( int.MaxValue, int.MaxValue );
			var closestDistance = float.MaxValue;

			foreach ( var cellStacks in Cells )
			{
				var distance = cellStacks.Key.DistanceSquared( coordinates2D );

				if ( distance < closestDistance )
				{
					closestCoordinates = cellStacks.Key;
					closestDistance = distance;
				}
			}

			cellsAtCoordinates = Cells.GetValueOrDefault( closestCoordinates );
		}

		if ( cellsAtCoordinates == null ) return null; // Guess there were no cells at all??
		if ( cellsAtCoordinates.Count == 1 ) return cellsAtCoordinates.First(); // If it's only one cell return it instantly

		if ( onlyBelow )
		{
			// Get the nearest cell which is under the given coordinates
			var nearestCell = cellsAtCoordinates.Where( x => x.Bottom.z - StepSize < position.z )
				.OrderByDescending( x => x.Position.z )
				.FirstOrDefault();

			return nearestCell;
		}
		else
		{
			// Get the nearest cell
			var nearestCell = cellsAtCoordinates
				.OrderBy( x => x.Position.DistanceSquared( position ) )
				.FirstOrDefault();

			return nearestCell;
		}
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
	/// <param name="position"></param>
	/// <param name="bounds"></param>
	/// <param name="rotation"></param>
	/// <param name="identifier"></param>
	/// <param name="standableAngle"></param>
	/// <param name="stepSize"></param>
	/// <param name="cellSize"></param>
	/// <param name="heightClearance"></param>
	/// <param name="widthClearance"></param>
	/// <param name="worldOnly"></param>
	/// <returns></returns>
	public async static Task<Grid> Create( Vector3 position, BBox bounds, Rotation rotation, string identifier = "main", float standableAngle = GridSettings.DEFAULT_STANDABLE_ANGLE, float stepSize = GridSettings.DEFAULT_STEP_SIZE, float cellSize = GridSettings.DEFAULT_CELL_SIZE, float heightClearance = GridSettings.DEFAULT_HEIGHT_CLEARANCE, float widthClearance = GridSettings.DEFAULT_WIDTH_CLEARANCE, bool worldOnly = GridSettings.DEFAULT_WORLD_ONLY )
	{
		Stopwatch totalWatch = new Stopwatch();
		totalWatch.Start();

		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Creating grid {identifier}" );

		var currentGrid = new Grid( identifier );
		currentGrid.Position = position;
		currentGrid.Bounds = bounds;
		currentGrid.Rotation = rotation;
		currentGrid.StandableAngle = standableAngle;
		currentGrid.StepSize = stepSize;
		currentGrid.CellSize = cellSize;
		currentGrid.HeightClearance = heightClearance;
		currentGrid.WidthClearance = widthClearance;
		currentGrid.WorldOnly = worldOnly;

		var minimumGrid = bounds.Mins.ToIntVector2( cellSize );
		var maximumGrid = bounds.Maxs.ToIntVector2( cellSize );
		var minHeight = bounds.Mins.z;
		var maxHeight = bounds.Maxs.z;

		await GameTask.RunInThreadAsync( () =>
		{

			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Casting {(maximumGrid.y - minimumGrid.y) * (maximumGrid.x - minimumGrid.x)} cells. [{maximumGrid.x - minimumGrid.x}x{maximumGrid.y - minimumGrid.y}]" );

			for ( int column = minimumGrid.y; column <= maximumGrid.y; column++ )
			{
				for ( int row = minimumGrid.x; row <= maximumGrid.x; row++ )
				{
					var startPosition = position + new Vector3( row * cellSize, column * cellSize, maxHeight + heightClearance + cellSize ) * rotation;
					var endPosition = position + new Vector3( row * cellSize, column * cellSize, minHeight - heightClearance - cellSize ) * rotation;
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
							var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition );

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
		} );

		totalWatch.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} created in {totalWatch.ElapsedMilliseconds}ms" );

		await currentGrid.Initialize();

		return currentGrid;
	}

	public async Task<bool> Initialize( bool save = true )
	{
		if ( Grids.ContainsKey( Identifier ) )
			Grids[Identifier] = this;
		else
			Grids.Add( Identifier, this );

		if ( save )
			await this.Save();

		return true;
	}

	public void Delete( bool deleteSave = false )
	{
		Event.Unregister( this );

		if ( Grids.ContainsKey( Identifier ) )
			Grids[Identifier] = null;

		if ( deleteSave )
			DeleteSave();

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
				if ( cell.GetNeighbours().Count() < 8 )
					outerCells.Add( cell );

		return outerCells;
	}
}



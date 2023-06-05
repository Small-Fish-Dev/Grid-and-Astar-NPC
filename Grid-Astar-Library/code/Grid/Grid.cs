global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Threading.Tasks;
using Sandbox.Internal;
using System.Runtime.InteropServices;

namespace GridAStar;

// Set STEP_SIZE or WIDTH_CLEARANCE to 0 to disable them ( Faster grid generation )
public static partial class GridSettings
{
	public const float DEFAULT_STANDABLE_ANGLE = 40f;   // How steep the terrain can be on a cell before it gets discarded
	public const float DEFAULT_STEP_SIZE = 12f;			// How big steps can be on a cell before it gets discarded
	public const float DEFAULT_CELL_SIZE = 16f;         // How large each cell will be in hammer units
	public const float DEFAULT_HEIGHT_CLEARANCE = 72f;  // How much vertical space there should be
	public const float DEFAULT_WIDTH_CLEARANCE = 24f;   // How much horizontal space there should be
	public const bool DEFAULT_GRID_PERFECT = false;		// For grid-perfect terrain, if true it will not be checking for steps, so use ramps instead
	public const bool DEFAULT_WORLD_ONLY = true;		// Will it only hit the world or also static entities
}

public struct GridBuilder
{
	public string Identifier { get; private set; } = "main";
	public float StandableAngle { get; private set; } = GridSettings.DEFAULT_STANDABLE_ANGLE;
	public float StepSize { get; private set; } = GridSettings.DEFAULT_STEP_SIZE;
	public float CellSize { get; private set; } = GridSettings.DEFAULT_CELL_SIZE;
	public float HeightClearance { get; private set; } = GridSettings.DEFAULT_HEIGHT_CLEARANCE;
	public float WidthClearance { get; private set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;
	public bool GridPerfect { get; private set; } = GridSettings.DEFAULT_GRID_PERFECT;
	public bool WorldOnly { get; private set; } = GridSettings.DEFAULT_WORLD_ONLY;
	public List<string> TagsToInclude { get; private set; } = new() { "solid" };
	public List<string> TagsToExclude { get; private set; } = new() { "player" };

	public GridBuilder() { }
	/// <summary>
	///  By default the identifier is "main", which makes it useable with Grid.Main
	/// </summary>
	/// <param name="identifier"></param>
	public GridBuilder( string identifier ) 
	{
		Identifier = identifier;
	}

	/// <summary>
	/// How steep the terrain can be before a cell doesn't get generated on it
	/// </summary>
	/// <param name="standableAngle"></param>
	/// <returns></returns>
	public GridBuilder WithStandableAngle( float standableAngle )
	{
		StandableAngle = standableAngle;
		return this;
	}

	/// <summary>
	/// How tall steps can be to be considered walkable (Doesn't work if GridPerfect)
	/// </summary>
	/// <param name="stepSize"></param>
	/// <returns></returns>
	public GridBuilder WithStepSize( float stepSize )
	{
		if ( !GridPerfect )
			StepSize = stepSize;
		return this;
	}

	/// <summary>
	/// How big the cells are generated
	/// </summary>
	/// <param name="cellSize"></param>
	/// <returns></returns>
	public GridBuilder WithCellSize( float cellSize )
	{
		CellSize = cellSize;
		return this;
	}

	/// <summary>
	/// Minimum vertical space for a cell to be generated
	/// </summary>
	/// <param name="heightClearance"></param>
	/// <returns></returns>
	public GridBuilder WithHeightClearance( float heightClearance )
	{
		HeightClearance = heightClearance;
		return this;
	}

	/// <summary>
	/// Minimum horizontal space for a cell to be generate
	/// </summary>
	/// <param name="widthClearance"></param>
	/// <returns></returns>
	public GridBuilder WithWidthClearance( float widthClearance )
	{
		WidthClearance = widthClearance;
		return this;
	}

	/// <summary>
	/// Cells will be generated with some clearance for grid-perfect terrain (Think of voxels), this isn't compatible with stairs so use sloped geometry instead.
	/// </summary>
	/// <returns></returns>
	public GridBuilder WithGridPerfect()
	{
		GridPerfect = true;
		StepSize = 0f;
		return this;
	}

	/// <summary>
	/// Ignore entities, only hit create cells on the world
	/// </summary>
	/// <returns></returns>
	public GridBuilder WithWorldOnly()
	{
		WorldOnly = true;
		return this;
	}

	/// <summary>
	/// Only hit entities with the following tags ("solid" is included by default, you can exclude it with WithoutTags)
	/// </summary>
	/// <returns></returns>
	public GridBuilder WithTags( params string[] tags )
	{
		foreach ( var tag in tags )
		{
			if ( !TagsToInclude.Contains( tag ) )
				TagsToInclude.Add( tag );
			if ( TagsToExclude.Contains( tag ) )
				TagsToExclude.Remove( tag );
		}
		return this;
	}

	/// <summary>
	/// Only hit entities without the following tags ("player" is included by default, you can include it with WithTags)
	/// </summary>
	/// <returns></returns>
	public GridBuilder WithoutTags( params string[] tags )
	{
		foreach ( var tag in tags )
		{
			if ( !TagsToExclude.Contains( tag ) )
				TagsToExclude.Add( tag );
			if ( TagsToInclude.Contains( tag ) )
				TagsToInclude.Remove( tag );
		}
		return this;
	}

}

public partial class Grid : IValid
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

	public GridBuilder Settings { get; internal set; }
	public string Identifier => Settings.Identifier;
	public string SaveIdentifier => $"{Game.Server.MapIdent}-{Identifier}";
	public Dictionary<IntVector2, List<Cell>> Cells { get; internal set; } = new();
	public Vector3 Position { get; set; }
	public BBox Bounds { get; set; }
	public BBox RotatedBounds => Bounds.GetRotatedBounds( Rotation );
	public BBox WorldBounds => RotatedBounds.Translate( Position );
	public Rotation Rotation { get; set; }
	public bool AxisAligned { get; set; }
	public float StandableAngle { get; set; }
	public float StepSize { get; set; }
	public float CellSize { get; set; }
	public float HeightClearance { get; set; }
	public float WidthClearance { get; set; }
	public bool GridPerfect { get; set; }
	public bool WorldOnly { get; set; }
	public float RealStepSize => GridPerfect ? 0.1f : Math.Max( 0.1f, StepSize );
	public float Tolerance => GridPerfect ? 0.001f : 0f;
	public Rotation AxisRotation => AxisAligned ? new Rotation() : Rotation;
	bool IValid.IsValid { get; }

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
	/// Get the local coordinate in a grid from a 3D world position
	/// </summary>
	/// <param name="position"></param>
	/// <returns></returns>
	public IntVector2 PositionToCoordinates( Vector3 position ) => (position - WorldBounds.Mins - CellSize / 2).ToIntVector2( CellSize );

	/// <summary>
	/// Find the nearest cell from a position even if the position is outside of the grid (This is expensive! Don't use it much)
	/// </summary>
	/// <param name="position"></param>
	/// <param name="onlyBelow"></param>
	/// <param name="unoccupiedOnly"></param>
	/// <returns></returns>
	public Cell GetNearestCell( Vector3 position, bool onlyBelow = true, bool unoccupiedOnly = false )
	{
		var validCells = Cells.Values.SelectMany( x => x );

		if ( unoccupiedOnly )
			validCells = validCells.Where( x => !x.Occupied );
		if ( onlyBelow )
			validCells = validCells.Where( x => x.Vertices.Min() - StepSize <= position.z );

		return validCells.OrderBy( x => x.Position.DistanceSquared( position ) )
			.FirstOrDefault();
	}

	/// <summary>
	/// Find exact cell on the position provided
	/// </summary>
	/// <param name="position"></param>
	/// <param name ="onlyBelow"></param>
	/// <returns></returns>
	public Cell GetCell( Vector3 position, bool onlyBelow = true ) => GetCell( PositionToCoordinates( position ), onlyBelow ? position.z : WorldBounds.Maxs.z );

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

		foreach ( var cell in cellsAtCoordinates )
			if ( cell.Vertices.Min() - StepSize < height )
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
	/// Returns the nearest cell in any direction.
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="direction"></param>
	/// <param name="numOfCellsInDirection"></param>
	/// <returns></returns>
	public Cell GetCellInDirection( Cell startingCell, Vector3 direction, int numOfCellsInDirection = 1 )
	{
		return GetCell( startingCell.Position + direction * CellSize * numOfCellsInDirection );
	}

	/// <summary>
	/// Returns if there's a valid, unoccupied, and direct path from a cell to another
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="endingCell"></param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns></returns>
	public bool LineOfSight( Cell startingCell, Cell endingCell, Entity pathCreator = null )
	{
		var startingPosition = startingCell.Position;
		var endingPosition = endingCell.Position;
		var direction = ( endingPosition - startingPosition ).Normal;
		var distanceInSteps = (int)Math.Ceiling( startingPosition.Distance( endingPosition ) / CellSize);

		if ( pathCreator == null && startingCell.Occupied ) return false;
		if ( pathCreator != null && startingCell.Occupied && startingCell.OccupyingEntity != pathCreator ) return false;

		if ( pathCreator == null && endingCell.Occupied ) return false;
		if ( pathCreator != null && endingCell.Occupied && endingCell.OccupyingEntity != pathCreator ) return false;

		Cell lastCell = startingCell;

		for ( int i = 1; i < distanceInSteps; i++ )
		{
			var cellToCheck = GetCellInDirection( startingCell, direction, i );

			if ( cellToCheck == null ) return false;
			if ( pathCreator == null && cellToCheck.Occupied ) return false;
			if ( pathCreator != null && cellToCheck.Occupied && cellToCheck.OccupyingEntity != pathCreator ) return false;
			if ( !cellToCheck.IsNeighbour( lastCell ) ) return false;

			lastCell = cellToCheck;
		}

		return true;
	}

	public bool IsInsideBounds( Vector3 point ) => Bounds.IsRotatedPointWithinBounds( Position, point, Rotation );
	public bool IsInsideCylinder( Vector3 point ) => Bounds.IsInsideSquishedRotatedCylinder( Position, point, Rotation );

	/// <summary>
	/// Creates a new grid and generates cells within the bounds given
	/// </summary>
	/// <param name="position"></param>
	/// <param name="bounds"></param>
	/// <param name="rotation"></param>
	/// <param name="axisAligned"></param>
	/// <param name="identifier"></param>
	/// <param name="standableAngle"></param>
	/// <param name="stepSize"></param>
	/// <param name="cellSize"></param>
	/// <param name="heightClearance"></param>
	/// <param name="widthClearance"></param>
	/// <param name="gridPerfect"></param>
	/// <param name="worldOnly"></param>
	/// <param name="cylinder"></param>
	/// <param name="save"></param>
	/// <returns></returns>
	public async static Task<Grid> Create( Vector3 position, BBox bounds, Rotation rotation, string identifier = "main", bool axisAligned = true, float standableAngle = GridSettings.DEFAULT_STANDABLE_ANGLE, float stepSize = GridSettings.DEFAULT_STEP_SIZE, float cellSize = GridSettings.DEFAULT_CELL_SIZE, float heightClearance = GridSettings.DEFAULT_HEIGHT_CLEARANCE, float widthClearance = GridSettings.DEFAULT_WIDTH_CLEARANCE, bool gridPerfect = GridSettings.DEFAULT_GRID_PERFECT, bool worldOnly = GridSettings.DEFAULT_WORLD_ONLY, bool cylinder = false, bool save = true )
	{
		Stopwatch totalWatch = new Stopwatch();
		totalWatch.Start();

		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Creating grid {identifier}" );

		var currentGrid = new Grid( identifier );
		currentGrid.Position = position;
		currentGrid.Bounds = bounds;
		currentGrid.Rotation = rotation;
		currentGrid.AxisAligned = axisAligned;
		currentGrid.StandableAngle = standableAngle;
		currentGrid.StepSize = stepSize;
		currentGrid.CellSize = cellSize;
		currentGrid.HeightClearance = heightClearance;
		currentGrid.WidthClearance = widthClearance;
		currentGrid.GridPerfect = gridPerfect;
		currentGrid.WorldOnly = worldOnly;

		var rotatedBounds = bounds.GetRotatedBounds( rotation );

		var minimumGrid = rotatedBounds.Mins.ToIntVector2( cellSize );
		var maximumGrid = rotatedBounds.Maxs.ToIntVector2( cellSize );
		var totalColumns = maximumGrid.y - minimumGrid.y;
		var totalRows = maximumGrid.x - minimumGrid.x;
		var minHeight = rotatedBounds.Mins.z;
		var maxHeight = rotatedBounds.Maxs.z;

		var box = new BBox( position + rotatedBounds.Mins, position + rotatedBounds.Maxs );
		await GameTask.RunInThreadAsync( () =>
		{
			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Casting {(maximumGrid.y - minimumGrid.y) * (maximumGrid.x - minimumGrid.x)} cells. [{maximumGrid.x - minimumGrid.x}x{maximumGrid.y - minimumGrid.y}]" );

			for ( int column = 0; column < totalColumns; column++ )
			{
				for ( int row = 0; row < totalRows; row++ )
				{
					var startPosition = box.Mins.WithZ( box.Maxs.z ) + new Vector3( row * cellSize + cellSize / 2f, column * cellSize + cellSize / 2f, currentGrid.Tolerance * 2f ) * currentGrid.AxisRotation;
					var endPosition = box.Mins + new Vector3( row * cellSize + cellSize / 2f, column * cellSize + cellSize / 2f, -currentGrid.Tolerance ) * currentGrid.AxisRotation;
					var checkBBox = new BBox( new Vector3( -cellSize / 2f + currentGrid.Tolerance, -cellSize / 2f + currentGrid.Tolerance, 0f ), new Vector3( cellSize / 2f - currentGrid.Tolerance, cellSize / 2f - currentGrid.Tolerance, 0.001f ) );
					var positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition );

					if ( worldOnly )
						positionTrace.WorldOnly();
					else
						positionTrace.WorldAndEntities();

					var positionResult = positionTrace.Run();

					while ( positionResult.Hit && startPosition.z >= endPosition.z )
					{
						if ( currentGrid.IsInsideBounds( positionResult.HitPosition ) )
						{
							if ( !cylinder || currentGrid.IsInsideCylinder( positionResult.HitPosition ) )
							{
								var angle = Vector3.GetAngle( Vector3.Up, positionResult.Normal );
								if ( angle <= standableAngle )
								{
									var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition );

									if ( newCell != null )
										currentGrid.AddCell( newCell );
								}
							}
						}

						startPosition = positionResult.HitPosition + Vector3.Down * heightClearance;

						while ( Sandbox.Trace.TestPoint( startPosition, radius: cellSize / 2f - currentGrid.Tolerance ) )
							startPosition += Vector3.Down * heightClearance;

						positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition );

						if ( worldOnly )
							positionTrace.WorldOnly();
						else
							positionTrace.WorldAndEntities();

						positionResult = positionTrace.Run();
					}
				}
			}
		} );

		Stopwatch edgeCells = new Stopwatch();
		edgeCells.Start();
		currentGrid.AssignEdgeCells();
		edgeCells.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} assigned edge cells in {edgeCells.ElapsedMilliseconds}ms" );

		totalWatch.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} created in {totalWatch.ElapsedMilliseconds}ms" );

		await currentGrid.Initialize( save );

		return currentGrid;
	}

	public async Task<bool> Initialize( bool save = true )
	{
		if ( Grids.ContainsKey( Identifier ) )
		{
			if ( Grids[Identifier] != null )
				Grids[Identifier].Delete( true );

			Grids[Identifier] = this;
		}
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

	public override int GetHashCode()
	{
		var identifierHashCode = Identifier.GetHashCode();
		var positionHashCode = Position.GetHashCode();
		var boundsHashCode = Bounds.GetHashCode();
		var rotationHashCode = Rotation.GetHashCode();
		var axisAlignedHashCode = AxisAligned.GetHashCode();
		var standableAngleHashCode = StandableAngle.GetHashCode();
		var stepSizeHashCode = StepSize.GetHashCode();
		var cellSizeHashCode = CellSize.GetHashCode();
		var heightClearanceHashCode = HeightClearance.GetHashCode();
		var widthClearanceHashCode = WidthClearance.GetHashCode();
		var gridPerfectHashCode = GridPerfect.GetHashCode();
		var worldOnlyHashCode = WorldOnly.GetHashCode();

		var hashCodeFirst = HashCode.Combine( identifierHashCode, positionHashCode, boundsHashCode, rotationHashCode, axisAlignedHashCode, standableAngleHashCode, stepSizeHashCode, cellSizeHashCode );
		var hashCodeSecond = HashCode.Combine( cellSizeHashCode, heightClearanceHashCode, widthClearanceHashCode, gridPerfectHashCode, worldOnlyHashCode );

		return HashCode.Combine( hashCodeFirst, hashCodeSecond );
	}

	/// <summary>
	/// Gives the edge tag to all cells with less than 8 neighbours
	/// </summary>
	/// <returns></returns>
	public void AssignEdgeCells()
	{
		foreach ( var cellStack in Cells )
			foreach ( var cell in cellStack.Value )
				if ( cell.GetNeighbours().Count() < 8 )
					cell.Tags.Add( "edge" );
	}

	/// <summary>
	/// Loop through cells and set them as occupied if an entity is inside of their clearance zone
	/// </summary>
	/// <param name="tag"></param>
	public void CheckOccupancy( string tag )
	{
		foreach ( var cellStack in Cells )
			foreach ( var cell in cellStack.Value )
				cell.Occupied = cell.TestForOccupancy( tag );
	}
}



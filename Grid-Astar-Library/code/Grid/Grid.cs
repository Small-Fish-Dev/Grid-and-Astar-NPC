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

public partial class Grid : IValid
{
	public static Grid Main
	{
		get 
		{
			if ( Grids.ContainsKey( $"{Game.Server.MapIdent}-main" ) )
				return Grids[$"{Game.Server.MapIdent}-main"];
			else
				return null;
		}
		set 
		{
			if ( Grids.ContainsKey( $"{Game.Server.MapIdent}-main" ) )
				Grids[$"{Game.Server.MapIdent}-main"] = value;
			else
				Grids.Add( $"{Game.Server.MapIdent}-main", value );
		}
	}

	public static Dictionary<string, Grid> Grids { get; set; } = new();

	public string Identifier { get; set; }
	public string SaveIdentifier => $"{Game.Server.MapIdent}-{Identifier}";
	public Dictionary<IntVector2, List<Cell>> Cells { get; internal set; } = new();
	public Vector3 Position { get; set; }
	public BBox Bounds { get; set; }
	public Rotation Rotation { get; set; }
	public bool AxisAligned { get; set; }
	public float StandableAngle { get; set; }
	public float StepSize { get; set; }
	public float CellSize { get; set; }
	public float HeightClearance { get; set; }
	public float WidthClearance { get; set; }
	public bool WorldOnly { get; set; }
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
	/// <param name="axisAligned"></param>
	/// <param name="identifier"></param>
	/// <param name="standableAngle"></param>
	/// <param name="stepSize"></param>
	/// <param name="cellSize"></param>
	/// <param name="heightClearance"></param>
	/// <param name="widthClearance"></param>
	/// <param name="worldOnly"></param>
	/// <returns></returns>
	public async static Task<Grid> Create( Vector3 position, BBox bounds, Rotation rotation, string identifier = "main", bool axisAligned = true, float standableAngle = GridSettings.DEFAULT_STANDABLE_ANGLE, float stepSize = GridSettings.DEFAULT_STEP_SIZE, float cellSize = GridSettings.DEFAULT_CELL_SIZE, float heightClearance = GridSettings.DEFAULT_HEIGHT_CLEARANCE, float widthClearance = GridSettings.DEFAULT_WIDTH_CLEARANCE, bool worldOnly = GridSettings.DEFAULT_WORLD_ONLY )
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
		currentGrid.WorldOnly = worldOnly;

		if ( worldOnly )
			currentGrid.SetGridIgnoreTags();

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
					var startPosition = box.Mins.WithZ( box.Maxs.z ) + new Vector3( row * cellSize + cellSize / 2f, column * cellSize + cellSize / 2f, 0.002f ) * currentGrid.AxisRotation;
					var endPosition = box.Mins + new Vector3( row * cellSize + cellSize / 2f, column * cellSize + cellSize / 2f, -0.001f ) * currentGrid.AxisRotation;
					var checkBBox = new BBox( new Vector3( -cellSize / 2f + 0.001f, -cellSize / 2f + 0.001f, 0f ), new Vector3( cellSize / 2f - 0.001f, cellSize / 2f - 0.001f, 0.001f ) );
					var positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition );

					if ( worldOnly )
						positionTrace.WorldOnly();
					else
					{
						positionTrace.WorldAndEntities()
							.WithoutTags( $"{currentGrid.Identifier}GridIgnore" );
					}

					var positionResult = positionTrace.Run();


					while ( positionResult.Hit && startPosition.z >= endPosition.z )
					{
						if ( bounds.IsRotatedPointWithinBounds( position, positionResult.HitPosition, rotation ) )
						{
							if ( Vector3.GetAngle( Vector3.Up, positionResult.Normal ) <= standableAngle )
							{
								var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition );

								if ( newCell != null )
									currentGrid.AddCell( newCell );
							}
						}

						startPosition = positionResult.HitPosition + Vector3.Down * heightClearance;

						while ( Sandbox.Trace.TestPoint( startPosition, radius: cellSize / 2f - 0.001f ) )
							startPosition += Vector3.Down * heightClearance;

						positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition );

						if ( worldOnly )
							positionTrace.WorldOnly();
						else
						{
							positionTrace.WorldAndEntities()
							.WithoutTags( $"{currentGrid.Identifier}GridIgnore" );
						}

						positionResult = positionTrace.Run();
					}
				}
			}
		} );

		if ( worldOnly )
			currentGrid.RemoveGridIgnoreTags();

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

	public void SetGridIgnoreTags()
	{
		var allEntities = Entity.All
			.OfType<ModelEntity>();

		foreach( var entity in allEntities )
			if ( entity.PhysicsEnabled && entity.PhysicsBody.IsValid() )
				if ( entity.PhysicsBody.BodyType != PhysicsBodyType.Static )
					entity.Tags.Add( $"{Identifier}GridIgnore" );
	}

	public void RemoveGridIgnoreTags()
	{
		var allEntities = Entity.All
			.OfType<ModelEntity>();

		foreach ( var entity in allEntities )
			if ( entity.PhysicsEnabled && entity.PhysicsBody.IsValid() )
				if ( entity.PhysicsBody.BodyType != PhysicsBodyType.Static )
					entity.Tags.Remove( $"{Identifier}GridIgnore" );
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



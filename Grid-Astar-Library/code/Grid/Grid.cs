global using Sandbox;
global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Linq;
global using System.Threading.Tasks;

namespace GridAStar;

// Set STEP_SIZE or WIDTH_CLEARANCE to 0 to disable them ( Faster grid generation )
public static partial class GridSettings
{
	public const float DEFAULT_STANDABLE_ANGLE = 40f;   // How steep the terrain can be on a cell before it gets discarded
	public const float DEFAULT_STEP_SIZE = 12f;         // How big steps can be on a cell before it gets discarded
	public const float DEFAULT_CELL_SIZE = 16f;         // How large each cell will be in hammer units
	public const float DEFAULT_HEIGHT_CLEARANCE = 72f;  // How much vertical space there should be
	public const float DEFAULT_WIDTH_CLEARANCE = 24f;   // How much horizontal space there should be
	public const float DEFAULT_DROP_HEIGHT = 400f;      // How high you can drop down from
	public const bool DEFAULT_GRID_PERFECT = false;     // For grid-perfect terrain, if true it will not be checking for steps, so use ramps instead
	public const bool DEFAULT_WORLD_ONLY = true;        // Will it only hit the world or also static entities
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
	public Dictionary<IntVector2, List<Cell>> CellStacks { get; internal set; } = new();
	public IEnumerable<Cell> AllCells => CellStacks.Values.SelectMany( list => list );
	public Vector3 Position => Settings.Position;
	public BBox Bounds => Settings.Bounds;
	public BBox RotatedBounds => Bounds.GetRotatedBounds( Rotation );
	public BBox WorldBounds => RotatedBounds.Translate( Position );
	public Transform Transform => new Transform( WorldBounds.Center, AxisRotation );
	public BBox ToWorld( BBox bounds ) => bounds.GetRotatedBounds( AxisRotation ).Translate( WorldBounds.Center );
	public BBox ToLocal( BBox bounds ) => bounds.GetRotatedBounds( AxisRotation.Inverse ).Translate( -WorldBounds.Center );
	public Rotation Rotation => Settings.Rotation;
	public bool AxisAligned => Settings.AxisAligned;
	public float StandableAngle => Settings.StandableAngle;
	public float StepSize => Settings.StepSize;
	public float CellSize => Settings.CellSize;
	public float HeightClearance => Settings.HeightClearance;
	public float WidthClearance => Settings.WidthClearance;
	public bool GridPerfect => Settings.GridPerfect;
	public bool WorldOnly => Settings.WorldOnly;
	public float MaxDropHeight => Settings.MaxDropHeight;
	public bool CylinderShaped => Settings.CylinderShaped;
	public float RealStepSize => GridPerfect ? 0.1f : Math.Max( 0.1f, StepSize );
	public float Tolerance => GridPerfect ? 0.001f : 0f;
	public Rotation AxisRotation => AxisAligned ? new Rotation() : Rotation;
	public int MinimumColumn => WorldBounds.Mins.ToIntVector2( CellSize ).y;
	public int MaximumColumn => WorldBounds.Maxs.ToIntVector2( CellSize ).y;
	public int Columns => MaximumColumn - MinimumColumn;
	public int MinimumRow => WorldBounds.Mins.ToIntVector2( CellSize ).x;
	public int MaximumRow => WorldBounds.Maxs.ToIntVector2( CellSize ).x;
	public int Rows => MaximumRow - MinimumRow;
	bool IValid.IsValid { get; }

	public Grid()
	{
		Settings = new GridBuilder();
		Event.Register( this );
	}

	public Grid( GridBuilder settings ) : this()
	{
		Settings = settings;
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
		var validCells = AllCells;

		if ( unoccupiedOnly )
			validCells = validCells.Where( x => !x.Occupied );
		if ( onlyBelow )
			validCells = validCells.Where( x => x.Vertices.Min() - Math.Max( HeightClearance, RealStepSize ) <= position.z );

		return validCells.OrderBy( x => x.Position.DistanceSquared( position ) )
			.FirstOrDefault();
	}

	public Cell GetCellInArea( Vector3 position, float width, bool onlyBelow = true, bool withinStepRange = true )
	{
		var cellsToCheck = (int)Math.Ceiling( width / CellSize ) * 2;
		for ( int y = 0; y <= cellsToCheck; y++ )
		{
			var spiralY = MathAStar.SpiralPattern( y );
			for ( int x = 0; x <= cellsToCheck; x++ )
			{
				var spiralX = MathAStar.SpiralPattern( x );
				var cellFound = GetCell( position + AxisRotation.Forward * spiralX * CellSize + AxisRotation.Right * spiralY * CellSize + Vector3.Up * RealStepSize, onlyBelow );

				if ( cellFound == null ) continue;

				if ( withinStepRange )
					if ( position.z - cellFound.Position.z <= Math.Max( HeightClearance, RealStepSize ) ) return cellFound; else continue;

				return cellFound;
			}
		}

		return null;
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
		var cellsAtCoordinates = CellStacks.GetValueOrDefault( coordinates );

		if ( cellsAtCoordinates == null ) return null;

		foreach ( var cell in cellsAtCoordinates )
			if ( cell.Vertices.Min() - Math.Max( HeightClearance, RealStepSize ) < height )
				return cell;

		return null;
	}

	/// <summary>
	/// Add a cell to the grid using the cell's GridPosition, doesn't get added if there's already a cell there
	/// </summary>
	/// <param name="cell"></param>
	public void AddCell( Cell cell )
	{
		if ( cell == null ) return;
		var coordinates = cell.GridPosition;
		if ( !CellStacks.ContainsKey( coordinates ) )
			CellStacks.Add( coordinates, new List<Cell>() { cell } );
		else
			if ( !CellStacks[coordinates].Any( x => Math.Abs( x.Position.z - cell.Position.z ) < Math.Max( HeightClearance, RealStepSize ) ) )
				CellStacks[coordinates].Add( cell );
	}

	/// <summary>
	/// Returns the nearest cell in any direction.
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="direction"></param>
	/// <param name="numOfCellsInDirection"></param>
	/// <returns></returns>
	public Cell GetCellInDirection( Cell startingCell, Vector3 direction, int numOfCellsInDirection = 1 ) => GetCell( startingCell.Position + direction * CellSize * numOfCellsInDirection );

	/// <summary>
	/// Returns the neighbour in that direction
	/// </summary>
	/// <param name="cell"></param>
	/// <param name="direction"></param>
	/// <returns></returns>
	public Cell GetNeighbourInDirection( Cell cell, Vector3 direction )
	{
		var horizontalDirection = direction.WithZ( 0 ).Normal;
		var localCoordinates = horizontalDirection.ToIntVector2();
		var coordinatesToCheck = cell.GridPosition + localCoordinates;

		var cellsAtCoordinates = CellStacks.GetValueOrDefault( coordinatesToCheck );

		if ( cellsAtCoordinates == null ) return null;

		foreach ( var cellAtCoordinate in cellsAtCoordinates )
			if ( cell.IsNeighbour( cellAtCoordinate ) && cell != cellAtCoordinate )
				return cellAtCoordinate;

		return null;
	}

	/// <summary>
	/// Returns if there's a valid, unoccupied, and direct path from a cell to another
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="endingCell"></param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <param name="debugShow"></param>
	/// <returns></returns>
	public bool LineOfSight( Cell startingCell, Cell endingCell, Entity pathCreator = null, bool debugShow = false )
	{
		var startingPosition = startingCell.Position;
		var endingPosition = endingCell.Position;
		var distanceInSteps = (int)Math.Ceiling( startingPosition.Distance( endingPosition ) / CellSize );

		if ( pathCreator == null && startingCell.Occupied ) return false;
		if ( pathCreator != null && startingCell.Occupied && startingCell.OccupyingEntity != pathCreator ) return false;

		if ( pathCreator == null && endingCell.Occupied ) return false;
		if ( pathCreator != null && endingCell.Occupied && endingCell.OccupyingEntity != pathCreator ) return false;

		Cell lastCell = startingCell;
		for ( int i = 0; i <= distanceInSteps; i++ )
		{
			var direction = (endingPosition - lastCell.Position).Normal;
			var cellToCheck = GetNeighbourInDirection( lastCell, direction );

			if ( cellToCheck == null ) return false;
			if ( cellToCheck == endingCell ) return true;
			if ( cellToCheck == lastCell ) continue;
			if ( pathCreator == null && cellToCheck.Occupied ) return false;
			if ( pathCreator != null && cellToCheck.Occupied && cellToCheck.OccupyingEntity != pathCreator ) return false;
			if ( !cellToCheck.IsNeighbour( lastCell ) ) return false;

			lastCell = cellToCheck;

			if ( debugShow )
				lastCell.Draw( 2f, false, false, false );
		}

		return true;
	}

	/// <summary>
	/// Can you roughly walk towards the cell without it being a direct line of sight
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="endingCell"></param>
	/// <param name="maxDistanceFromDirectPath"></param>
	/// <param name="pathCreator"></param>
	/// <param name="withConnections"></param>
	/// <param name="debugShow"></param>
	/// <returns></returns>
	public bool IsDirectlyWalkable( Cell startingCell, Cell endingCell, float maxDistanceFromDirectPath = 150f, Entity pathCreator = null, bool withConnections = true,  bool debugShow = false )
	{
		if ( startingCell == null || endingCell == null ) return false;

		var currentCell = startingCell;
		var directPath = new Line( startingCell.Position.WithZ(0), endingCell.Position.WithZ(0) );
		List<Cell> cellsChecked = new();

		if ( debugShow )
		{
			startingCell.Draw( 3f, false, false, false );
			endingCell.Draw( 3f, false, false, false );
		}

		if ( pathCreator == null && startingCell.Occupied ) return false;
		if ( pathCreator != null && startingCell.Occupied && startingCell.OccupyingEntity != pathCreator ) return false;

		if ( pathCreator == null && endingCell.Occupied ) return false;
		if ( pathCreator != null && endingCell.Occupied && endingCell.OccupyingEntity != pathCreator ) return false;

		while ( currentCell != endingCell && directPath.Distance( currentCell.Position.WithZ(0) ) <= maxDistanceFromDirectPath )
		{
			var cellToCheck = withConnections ? currentCell.GetClosestNeighbourAndConnection( endingCell.Position ) : currentCell.GetClosestNeighbour( endingCell.Position );

			if ( debugShow )
				currentCell.Draw( 2f, false, false, false );

			if ( cellToCheck == null ) return false;
			if ( cellsChecked.Contains( cellToCheck ) ) return false;
			if ( pathCreator == null && cellToCheck.Occupied ) return false;
			if ( pathCreator != null && cellToCheck.Occupied && cellToCheck.OccupyingEntity != pathCreator ) return false;

			if ( cellToCheck == endingCell ) return true;

			cellsChecked.Add( currentCell );
			currentCell = cellToCheck;
		}

		return false;
	}

	public bool IsInsideBounds( Vector3 point ) => Bounds.IsRotatedPointWithinBounds( Position, point, Rotation );
	public bool IsInsideCylinder( Vector3 point ) => Bounds.IsInsideSquishedRotatedCylinder( Position, point, Rotation );

	public void Initialize()
	{
		if ( Grids.ContainsKey( Identifier ) )
		{
			if ( Grids[Identifier] != null )
				Grids[Identifier].Delete( true );

			Grids[Identifier] = this;
		}
		else
			Grids.Add( Identifier, this );
	}

	public void Delete( bool deleteSave = false )
	{
		Event.Unregister( this );

		if ( Grids.ContainsKey( Identifier ) )
			Grids[Identifier] = null;

		if ( deleteSave )
			DeleteSave();
	}

	public override int GetHashCode() => Settings.GetHashCode();

	/// <summary>
	/// Gives the edge tag to all cells with less than 8 neighbours
	/// </summary>
	/// <param name="maxNeighourCount">How many neighbours a cell needs to have to not be considered an edge</param>
	/// <returns></returns>
	public void AssignEdgeCells( int maxNeighourCount = 8 )
	{
		foreach ( var cell in AllCells )
			if ( cell.GetNeighbours().Count() < maxNeighourCount )
				cell.Tags.Add( "edge" );
	}

	/// <summary>
	/// Adds the droppable connection to cells you can drop from
	/// </summary>
	/// <returns></returns>
	public void AssignDroppableCells()
	{
		foreach ( var cell in CellsWithTag( "edge" ) )
		{
			var droppableCell = cell.GetFirstValidDroppable( maxHeightDistance: MaxDropHeight );
			if ( droppableCell != null )
				cell.AddConnection( droppableCell, "drop" );
		}
	}

	/// <summary>
	/// Create a new definition for connections between jumpable cells. This method is slow right now on bigger maps, use <paramref name="generateFraction"/> for better performance
	/// </summary>
	/// <param name="movementTag"></param>
	/// <param name="horizontalSpeed"></param>
	/// <param name="verticalSpeed"></param>
	/// <param name="gravity"></param>
	/// <param name="generateFraction">0.1 = Generate a connection only 10% of the times</param>
	/// <param name="maxPerCell">How many jump connections of this type can a cell have</param>
	/// <param name="threadsToUse"></param>
	public async Task AssignJumpableCells( string movementTag, float horizontalSpeed, float verticalSpeed, float gravity, float generateFraction = 0.3f, int maxPerCell = 2, int threadsToUse = 16 )
	{
		var allCells = CellsWithTag( "edge" );
		var cellsCount = allCells.Count();
		var cellsEachThread = (int)(cellsCount / threadsToUse);
		var lastThreadCount = cellsCount - (cellsEachThread * (threadsToUse - 1));
		List<Task> tasks = new();

		for ( int i = 0; i < threadsToUse; i++ )
		{
			var curentThread = i;

			tasks.Add( GameTask.RunInThreadAsync( () =>
			{
				var totalFraction = 0f;
				var cellsRange = curentThread == threadsToUse - 1 ? cellsEachThread : lastThreadCount;
				var cellsToCheck = allCells.Skip( cellsEachThread * curentThread ).Take( cellsRange );

				foreach ( var cell in cellsToCheck )
				{
					if ( totalFraction >= 1f )
					{
						List<Cell> connectedCells = new();

						foreach ( var jumpableCell in cell.GetValidJumpables( horizontalSpeed, verticalSpeed, gravity, 8, MaxDropHeight, maxPerCell ) )
							if ( jumpableCell != null )
							{
								cell.AddConnection( jumpableCell, movementTag );
								connectedCells.Add( jumpableCell );
							}

						foreach ( var jumpableConnection in connectedCells ) // Check if you can jump back onto the cell
						{
							var direction = (cell.Position - jumpableConnection.Position).WithZ( 0 ).Normal;
							var jumpbackCell = jumpableConnection.GetValidJumpable( horizontalSpeed, verticalSpeed, gravity, direction, MaxDropHeight );

							if ( jumpbackCell != null )
								jumpableConnection.AddConnection( jumpbackCell, movementTag );
						}

						totalFraction = 0f;
					}

					totalFraction += generateFraction;
				}
			} ));
		}

		await GameTask.WhenAll( tasks );
	}

	public Vector3 TraceParabola( Vector3 startingPosition, Vector3 horizontalVelocity, float verticalSpeed, float gravity, float maxDropHeight, int subSteps = 2 )
	{
		var horizontalDirection = horizontalVelocity.WithZ( 0 ).Normal;
		var horizontalSpeed = horizontalVelocity.WithZ( 0 ).Length;
		var maxHeight = startingPosition.z + MathAStar.ParabolaMaxHeight( verticalSpeed, gravity );
		var minHeight = maxHeight - maxDropHeight;
		var currentDistance = 1;
		var lastPositionChecked = startingPosition;

		while ( lastPositionChecked.z >= minHeight )
		{
			var horizontalOffset = CellSize * currentDistance / subSteps;
			var verticalOffset = MathAStar.ParabolaHeight( horizontalOffset, horizontalSpeed, verticalSpeed, gravity );
			var nextPositionToCheck = startingPosition + horizontalDirection * horizontalOffset + Vector3.Up * verticalOffset;

			var clearanceBBox = new BBox( new Vector3( -WidthClearance / 2f, -WidthClearance / 2f, RealStepSize ), new Vector3( WidthClearance / 2f, WidthClearance / 2f, HeightClearance ) );
			var jumpTrace = Sandbox.Trace.Box( clearanceBBox, lastPositionChecked, nextPositionToCheck )
				.WithGridSettings( Settings )
				.Run();
			//DebugOverlay.Sphere( nextPositionToCheck, CellSize / 2f, Color.Red, 5f );
			//DebugOverlay.Box( clearanceBBox.Translate( lastPositionChecked ), Color.Red, 5f );
			//DebugOverlay.TraceResult( jumpTrace, 5f );

			if ( jumpTrace.Hit )
			{
				//DebugOverlay.Box( clearanceBBox.Translate( jumpTrace.EndPosition ), Color.Blue, 5f );
				//var cell = Grid.Main.GetCellInArea( jumpTrace.EndPosition, WidthClearance );
				//if ( cell != null )
					//cell.Draw( Color.Blue, 3f, false, false, true );
				return jumpTrace.EndPosition;
			}

			lastPositionChecked = nextPositionToCheck;
			currentDistance++;
		}

		return lastPositionChecked;
	}

	public void RemoveCells( BBox bounds, bool printInfo = false, bool broadcastToClients = false )
	{
		List<Cell> cellsToRemove = new();

		foreach ( var cell in AllCells )
			if ( bounds.Contains( cell.Position ) )
				cellsToRemove.Add( cell );

		var count = cellsToRemove.Count();

		foreach ( var cell in cellsToRemove )
			cell.Delete();

		if ( printInfo )
			Print( $"Removed {count} cells" );

		if ( broadcastToClients )
			if ( Game.IsServer )
				Grid.removeCellsClient( Identifier, bounds, printInfo );
	}

	/// <summary>
	/// Create cells in that local bbox
	/// </summary>
	/// <param name="bounds">Local bounds</param>
	/// <param name="printInfo"></param>
	/// <param name="broadcastToClients"></param>
	public List<Cell> CreateCells( BBox bounds, bool printInfo = true, bool broadcastToClients = false )
	{
		var worldBounds = ToWorld( bounds );
		var generatedCells = new List<Cell>();

		var minimumGrid = bounds.Mins.ToIntVector2( CellSize );
		var maximumGrid = bounds.Maxs.ToIntVector2( CellSize );
		var startingColumn = minimumGrid.y - MinimumColumn;
		var totalColumns = maximumGrid.y - minimumGrid.y;
		var endingColumn = startingColumn + totalColumns;
		var startingRow = minimumGrid.x - MinimumRow;
		var totalRows = maximumGrid.x - minimumGrid.x;
		var endingRow = startingRow + totalRows;

		if ( printInfo )
			Print( $"Casting {totalRows * totalColumns} cells. [{totalRows}x{totalColumns}]" );

		for ( int column = startingColumn; column < endingColumn; column++ )
		{
			for ( int row = startingRow; row < endingRow; row++ )
			{
				var startPosition = WorldBounds.Mins.WithZ( worldBounds.Maxs.z ) + new Vector3( row * CellSize + CellSize / 2f, column * CellSize + CellSize / 2f, Tolerance * 2f ) * AxisRotation;
				var endPosition = WorldBounds.Mins.WithZ( worldBounds.Mins.z ) + new Vector3( row * CellSize + CellSize / 2f, column * CellSize + CellSize / 2f, -Tolerance ) * AxisRotation;
				var checkBBox = new BBox( new Vector3( -CellSize / 2f + Tolerance, -CellSize / 2f + Tolerance, 0f ), new Vector3( CellSize / 2f - Tolerance, CellSize / 2f - Tolerance, 0.001f ) );
				var positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition )
					.WithGridSettings( Settings );

				var positionResult = positionTrace.Run();

				while ( positionResult.Hit && startPosition.z >= endPosition.z )
				{
					if ( IsInsideBounds( positionResult.HitPosition ) )
					{
						if ( !CylinderShaped || IsInsideCylinder( positionResult.HitPosition ) )
						{
							var angle = Vector3.GetAngle( Vector3.Up, positionResult.Normal );
							if ( angle <= StandableAngle )
							{
								var newCell = Cell.TryCreate( this, positionResult.HitPosition );

								if ( newCell != null )
									generatedCells.Add( newCell );
							}
						}
					}

					startPosition = positionResult.HitPosition + Vector3.Down * HeightClearance;

					while ( Sandbox.Trace.TestPoint( startPosition, radius: CellSize / 2f - Tolerance ) )
						startPosition += Vector3.Down * HeightClearance;

					positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition )
						.WithGridSettings( Settings );

					positionResult = positionTrace.Run();
				}
			}
		}

		if ( broadcastToClients )
			if ( Game.IsServer )
				Grid.createCellsClient( Identifier, bounds, printInfo );

		if ( printInfo )
			Print( $"Generated {generatedCells.Count()} valid cells" );

		return generatedCells;
	}

	[ClientRpc]
	internal static void removeCellsClient( string identifier, BBox bounds, bool printInfo = false )
	{
		var grid = Grids[identifier];

		if ( grid != null )
			grid.RemoveCells( bounds, printInfo );
	}

	[ClientRpc]
	internal static void createCellsClient( string identifier, BBox bounds, bool printInfo = false )
	{
		var grid = Grids[identifier];

		if ( grid != null )
			grid.CreateCells( bounds, printInfo );
	}

	/// <summary>
	/// Returns all cells with that tag
	/// </summary>
	/// <param name="tag"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTag( string tag ) => AllCells.Where( cell => cell.Tags.Has( tag ) );

	/// <summary>
	/// Returns all cells with those tags
	/// </summary>
	/// <param name="tags"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTags( params string[] tags ) => AllCells.Where( cell => cell.Tags.Has( tags ) );

	/// <summary>
	/// Returns all cells with those tags
	/// </summary>
	/// <param name="tags"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTags( List<string> tags ) => AllCells.Where( cell => cell.Tags.Has( tags ) );

	/// <summary>
	/// Loop through cells and set them as occupied if an entity is inside of their clearance zone
	/// </summary>
	/// <param name="tag"></param>
	public void CheckOccupancy( string tag )
	{
		foreach ( var cell in AllCells )
			cell.Occupied = cell.TestForOccupancy( tag );
	}
}



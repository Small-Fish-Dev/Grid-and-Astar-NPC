global using Sandbox;
global using System;
global using System.Collections.Generic;
global using System.Linq;

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
	public const bool DEFAULT_GRID_PERFECT = false;     // For grid-perfect terrain like voxel
	public const bool DEFAULT_AXIS_ALIGNED = true;     // True = Cells get generated following the scene's transform, so all grids will match. False = Follow its own transform
}

public class Grid : Component, Component.ExecuteInEditor
{
	[Property]
	public BBox Bounds { get; private set; } = new();

	/// <summary>
	/// How steep the terrain can be on a cell before it gets discarded
	/// </summary>
	[Property]
	[Range( 1f, 89f, 1f )]
	public float StandableAngle { get; private set; } = GridSettings.DEFAULT_STANDABLE_ANGLE;

	/// <summary>
	/// How big steps can be on a cell before it gets discarded
	/// </summary>
	[Property]
	[Range( 0f, 128f, 1f )]
	public float StepSize { get; private set; } = GridSettings.DEFAULT_STEP_SIZE;

	/// <summary>
	/// How large each cell will be in hammer units
	/// </summary>
	[Property]
	[Range( 1f, 128f, 1f )]
	public float CellSize { get; private set; } = GridSettings.DEFAULT_CELL_SIZE;

	/// <summary>
	/// How much vertical space there should be
	/// </summary>
	[Property]
	[Range( 0f, 256f, 1f )]
	public float HeightClearance { get; private set; } = GridSettings.DEFAULT_HEIGHT_CLEARANCE;

	/// <summary>
	/// How much horizontal space there should be
	/// </summary>
	[Property]
	[Range( 0f, 128f, 1f )]
	public float WidthClearance { get; private set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;

	/// <summary>
	///  How high you can drop down from
	/// </summary>
	[Property]
	[Range( 0f, 9999f, 1f )]
	public float MaxDropHeight { get; private set; } = GridSettings.DEFAULT_DROP_HEIGHT;

	/// <summary>
	/// For grid-perfect terrain like voxel
	/// </summary>
	[Property]
	public bool GridPerfect { get; private set; } = GridSettings.DEFAULT_GRID_PERFECT;

	/// <summary>
	/// True = Cells get generated following the scene's transform, so all grids will match. False = Follow its own transform
	/// </summary>
	[Property]
	public bool AxisAligned { get; private set; } = GridSettings.DEFAULT_AXIS_ALIGNED;

	/// <summary>
	/// How many neighboring cells a cell needs to have to not be considered an edge cell. (For 2D vertical grids this would be 2, for anything else 8)
	/// </summary>
	[Property]
	[Range( 0, 8, 1 )]
	public int MinNeighbourCount { get; private set; } = 8;

	[Property]
	public TagSet TagsToInclude { get; private set; } = new();

	[Property]
	public TagSet TagsToExclude { get; private set; } = new();
	//public List<JumpDefinition> JumpDefinitions { get; private set; } = new(); // TODO: Add to persistance

	public Dictionary<IntVector2, List<Cell>> CellStacks { get; internal set; } = new();
	public IEnumerable<Cell> AllCells => CellStacks.Values.SelectMany( list => list );
	public BBox WorldBounds => Bounds.Translate( Transform.Position );

	public float Tolerance => GridPerfect ? 0.001f : 0f;
	public Rotation AxisRotation => AxisAligned ? Scene.Transform.Rotation : Transform.Rotation;
	public int MinimumColumn => WorldBounds.Mins.ToIntVector2( CellSize ).y;
	public int MaximumColumn => WorldBounds.Maxs.ToIntVector2( CellSize ).y;
	public int Columns => MaximumColumn - MinimumColumn;
	public int MinimumRow => WorldBounds.Mins.ToIntVector2( CellSize ).x;
	public int MaximumRow => WorldBounds.Maxs.ToIntVector2( CellSize ).x;
	public int Rows => MaximumRow - MinimumRow;

	/*[Property]
	public bool IgnoreConnectionsForJumps { get; private set; } = false; // TODO: Add to persistance

	[Property]
	public bool IgnoreLOSForJumps { get; private set; } = false; // TODO: Add to persistance*/

	private Model previewModel;

	protected override void DrawGizmos()
	{
		Gizmo.GizmoDraw draw = Gizmo.Draw;

		draw.LineBBox( Bounds );

		// Test performance

		if ( previewModel == null )
			previewModel = CreateModel();

		draw.Model( previewModel );


	}

	public Model CreateModel()
	{
		var gridSize = 1000;
		var squareSize = 10f;
		var totVertices = gridSize * gridSize * 6;

		Mesh mesh = new Mesh( Material.Load( "materials/dev/debug_wireframe.vmat" ) );
		mesh.SetVertexRange( 0, totVertices );

		List<Vertex> vertices = new();

		for ( int x = 0; x < gridSize; x++ )
		{
			for ( int z = 0; z < gridSize; z++ )
			{
				float xPos = x * squareSize * 1.5f;
				float zPos = z * squareSize * 1.5f;

				// First triangle
				Vector3 v1 = new Vector3( xPos, 0, zPos );
				Vector3 v2 = new Vector3( xPos + squareSize, 0, zPos );
				Vector3 v3 = new Vector3( xPos, 0, zPos + squareSize );

				// Second triangle
				Vector3 v4 = new Vector3( xPos + squareSize, 0, zPos + squareSize );
				Vector3 v5 = new Vector3( xPos, 0, zPos + squareSize );
				Vector3 v6 = new Vector3( xPos + squareSize, 0, zPos );

				vertices.Add( new Vertex( v1 ) );
				vertices.Add( new Vertex( v2 ) );
				vertices.Add( new Vertex( v3 ) );
				vertices.Add( new Vertex( v4 ) );
				vertices.Add( new Vertex( v5 ) );
				vertices.Add( new Vertex( v6 ) );
			}
		}

		mesh.CreateVertexBuffer<Vertex>( totVertices, Vertex.Layout, vertices );

		return Model.Builder.AddMesh( mesh ).Create();
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
	/// <returns></returns>
	public Cell GetNearestCell( Vector3 position, bool onlyBelow = true )
	{
		var validCells = AllCells;

		if ( onlyBelow )
			validCells = validCells.Where( x => x.Vertices.Min() - Math.Max( HeightClearance, StepSize ) <= position.z );

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
				var cellFound = GetCell( position + AxisRotation.Forward * spiralX * CellSize + AxisRotation.Right * spiralY * CellSize + Vector3.Up * StepSize, onlyBelow );

				if ( cellFound == null ) continue;

				if ( withinStepRange )
					if ( position.z - cellFound.Position.z <= Math.Max( HeightClearance, StepSize ) ) return cellFound; else continue;

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
			if ( cell.Vertices.Min() - Math.Max( HeightClearance, StepSize ) < height )
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
			if ( !CellStacks[coordinates].Any( x => Math.Abs( x.Position.z - cell.Position.z ) < Math.Max( HeightClearance, StepSize ) ) )
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
	/// <param name="debugShow"></param>
	/// <returns></returns>
	public bool LineOfSight( Cell startingCell, Cell endingCell, bool debugShow = false )
	{
		var startingPosition = startingCell.Position;
		var endingPosition = endingCell.Position;
		var distanceInSteps = (int)Math.Ceiling( startingPosition.Distance( endingPosition ) / CellSize );

		Cell lastCell = startingCell;
		for ( int i = 0; i <= distanceInSteps; i++ )
		{
			var direction = (endingPosition - lastCell.Position).Normal;
			var cellToCheck = GetNeighbourInDirection( lastCell, direction );

			if ( cellToCheck == null ) return false;
			if ( cellToCheck == endingCell ) return true;
			if ( cellToCheck == lastCell ) continue;
			if ( !cellToCheck.IsNeighbour( lastCell ) ) return false;

			lastCell = cellToCheck;
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
	/// <returns></returns>
	public bool IsDirectlyWalkable( Cell startingCell, Cell endingCell, float maxDistanceFromDirectPath = 150f, bool withConnections = true )
	{
		if ( startingCell == null || endingCell == null ) return false;

		var currentCell = startingCell;
		var directPath = new Line( startingCell.Position.WithZ( 0 ), endingCell.Position.WithZ( 0 ) );
		List<Cell> cellsChecked = new();

		while ( currentCell != endingCell && directPath.Distance( currentCell.Position.WithZ( 0 ) ) <= maxDistanceFromDirectPath )
		{
			var cellToCheck = withConnections ? currentCell.GetClosestNeighbourAndConnection( endingCell.Position ) : currentCell.GetClosestNeighbour( endingCell.Position );

			if ( cellToCheck == null ) return false;
			if ( cellsChecked.Contains( cellToCheck ) ) return false;

			if ( cellToCheck == endingCell ) return true;

			cellsChecked.Add( currentCell );
			currentCell = cellToCheck;
		}

		return false;
	}

	public bool IsInsideBounds( Vector3 point ) => Bounds.IsRotatedPointWithinBounds( Transform.Position, point, Transform.Rotation );

	public List<Cell> GetCellsInBBox( BBox bbox )
	{
		var cells = new List<Cell>();

		foreach ( var cell in AllCells )
			if ( bbox.Contains( cell.Position ) )
				cells.Add( cell );

		return cells;
	}

}
/*
public partial class Grid : IValid
{
	public List<JumpDefinition> JumpDefinitions => Settings.JumpDefinitions;
	public int MinNeighbourCount => Settings.MinNeighbourCount;
	public bool IgnoreConnectionsForJumps => Settings.IgnoreConnectionsForJumps;
	public bool IgnoreLOSForJumps => Settings.IgnoreLOSForJumps;
	public bool CylinderShaped => Settings.CylinderShaped;
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

	public BBox ToWorld( BBox bounds ) => bounds.GetRotatedBounds( AxisRotation ).Translate( WorldBounds.Center );
	public BBox ToLocal( BBox bounds ) => bounds.GetRotatedBounds( AxisRotation.Inverse ).Translate( -WorldBounds.Center );

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
			validCells = validCells.Where( x => x.Vertices.Min() - Math.Max( HeightClearance, StepSize ) <= position.z );

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
				var cellFound = GetCell( position + AxisRotation.Forward * spiralX * CellSize + AxisRotation.Right * spiralY * CellSize + Vector3.Up * StepSize, onlyBelow );

				if ( cellFound == null ) continue;

				if ( withinStepRange )
					if ( position.z - cellFound.Position.z <= Math.Max( HeightClearance, StepSize ) ) return cellFound; else continue;

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
			if ( cell.Vertices.Min() - Math.Max( HeightClearance, StepSize ) < height )
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
			if ( !CellStacks[coordinates].Any( x => Math.Abs( x.Position.z - cell.Position.z ) < Math.Max( HeightClearance, StepSize ) ) )
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
	/// <returns></returns>
	public bool IsDirectlyWalkable( Cell startingCell, Cell endingCell, float maxDistanceFromDirectPath = 150f, Entity pathCreator = null, bool withConnections = true )
	{
		if ( startingCell == null || endingCell == null ) return false;

		var currentCell = startingCell;
		var directPath = new Line( startingCell.Position.WithZ(0), endingCell.Position.WithZ(0) );
		List<Cell> cellsChecked = new();

		if ( pathCreator == null && startingCell.Occupied ) return false;
		if ( pathCreator != null && startingCell.Occupied && startingCell.OccupyingEntity != pathCreator ) return false;

		if ( pathCreator == null && endingCell.Occupied ) return false;
		if ( pathCreator != null && endingCell.Occupied && endingCell.OccupyingEntity != pathCreator ) return false;

		while ( currentCell != endingCell && directPath.Distance( currentCell.Position.WithZ(0) ) <= maxDistanceFromDirectPath )
		{
			var cellToCheck = withConnections ? currentCell.GetClosestNeighbourAndConnection( endingCell.Position ) : currentCell.GetClosestNeighbour( endingCell.Position );

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
		{
			Grids[Identifier] = null;
			Grids.Remove( Identifier );
		}

		if ( deleteSave )
			DeleteSave();
	}

	public List<Cell> GetCellsInBBox( BBox bbox )
	{
		var cells = new List<Cell>();

		foreach ( var cell in AllCells )
			if ( bbox.Contains( cell.Position ) )
				cells.Add( cell );

		return cells;
	}

	public override int GetHashCode() => Settings.GetHashCode();

	/// <summary>
	/// Gives the edge tag to all cells with less than 8 neighbours
	/// </summary>
	/// <param name="maxNeighourCount">How many neighbours a cell needs to have to not be considered an edge</param>
	/// <param name="threadsToUse">How many threads to use</param>
	/// <param name="clearTags">Clear previous edge tags, for regenerating terrain and not first creating</param>
	/// <param name="tagToExclude">Cells with this tag won't get counted, you can for example do "edge" to exclude previous edge cells and expand the edge towards the center</param>
	/// <param name="tagToAssign">Name of the tag that gets assigned</param>
	/// <returns></returns>
	public async Task AssignEdgeCells( int maxNeighourCount = 8, int threadsToUse = 1, bool clearTags = false, string tagToExclude = "", string tagToAssign = "edge" ) => await assignEdgeCellsInternal( AllCells.ToList(), maxNeighourCount, threadsToUse, clearTags, tagToExclude, tagToAssign );

	public async Task AssignEdgeCells( BBox bounds, int maxNeighourCount = 8, int threadsToUse = 1, bool clearTags = false, string tagToExclude = "", string tagToAssign = "edge" ) => await assignEdgeCellsInternal( GetCellsInBBox( bounds ), maxNeighourCount, threadsToUse, clearTags, tagToExclude, tagToAssign );

	internal async Task assignEdgeCellsInternal( List<Cell> cells, int maxNeighourCount = 8, int threadsToUse = 1, bool clearTags = false, string tagToExclude = "", string tagToAssign = "edge" )
	{
		var cellsCount = cells.Count();
		var cellsEachThread = (int)(cellsCount / threadsToUse);
		var lastThreadCount = cellsCount - (cellsEachThread * (threadsToUse - 1));
		List<Task> tasks = new();

		for ( int i = 0; i < threadsToUse; i++ )
		{
			var curentThread = i;

			tasks.Add( GameTask.RunInThreadAsync( () =>
			{
				var cellsRange = curentThread == threadsToUse - 1 ? cellsEachThread : lastThreadCount;
				var cellsToCheck = cells.Skip( cellsEachThread * curentThread ).Take( cellsRange );

				foreach ( var cell in cellsToCheck )
				{
					if ( clearTags )
						cell.Tags.Remove( tagToAssign );

					var neighbours = cell.GetNeighbours();

					if ( tagToExclude != "" )
						neighbours = neighbours.Where( x => !x.Tags.Has( tagToExclude ) );

					if ( neighbours.Count() < maxNeighourCount )
						cell.Tags.Add( tagToAssign );
				}
			} ) );
		}

		await GameTask.WhenAll( tasks );
	}

	/// <summary>
	/// Adds the droppable connection to cells you can drop from
	/// </summary>
	/// <returns></returns>
	public async Task AssignDroppableCells( int threadsToUse = 1 ) => await internalAssignDroppableCells( CellsWithTag( "edge" ).ToList(), threadsToUse );

	public async Task AssignDroppableCells( BBox bounds, int threadsToUse = 1 ) => await internalAssignDroppableCells( CellsWithTag( bounds, "edge" ).ToList(), threadsToUse );

	internal async Task internalAssignDroppableCells( List<Cell> cells, int threadsToUse = 1 )
	{
		var allCells = cells;
		var cellsCount = allCells.Count();
		var cellsEachThread = (int)(cellsCount / threadsToUse);
		var lastThreadCount = cellsCount - (cellsEachThread * (threadsToUse - 1));
		List<Task> tasks = new();

		for ( int i = 0; i < threadsToUse; i++ )
		{
			var curentThread = i;

			tasks.Add( GameTask.RunInThreadAsync( () =>
			{
				var cellsRange = curentThread == threadsToUse - 1 ? cellsEachThread : lastThreadCount;
				var cellsToCheck = allCells.Skip( cellsEachThread * curentThread ).Take( cellsRange );

				foreach ( var cell in cellsToCheck )
				{
					var droppableCell = cell.GetFirstValidDroppable( maxHeightDistance: MaxDropHeight );
					if ( droppableCell != null )
						cell.AddConnection( droppableCell, "drop" );
				}
			} ) );
		}

		await GameTask.WhenAll( tasks );
	}
	public IEnumerable<Cell> JumpableCandidates()
	{
		var droppedCells = CellsWithConnection( "drop" ).SelectMany( cell => cell.GetConnections( "drop" ).Select( connection => connection.Current ) );
		return CellsWithTag( "edge" ).Concat( droppedCells );
	}

	public IEnumerable<Cell> JumpableCandidates( BBox bounds )
	{
		var droppedCells = CellsWithConnection( bounds, "drop" ).SelectMany( cell => cell.GetConnections( "drop" ).Select( connection => connection.Current ) );
		return CellsWithTag( bounds, "edge" ).Concat( droppedCells );
	}

	/// <summary>
	/// Create a new definition for connections between jumpable cells. This method is slow right now on bigger maps
	/// </summary>
	/// <param name="definition"></param>
	/// <param name="threadsToUse"></param>
	public async Task AssignJumpableCells( JumpDefinition definition, int threadsToUse = 16 ) => await internalAssignJumpableCells( JumpableCandidates().ToList(), definition, threadsToUse );
	public async Task AssignJumpableCells( BBox bounds, JumpDefinition definition, int threadsToUse = 16 ) => await internalAssignJumpableCells( JumpableCandidates( bounds ).ToList(), definition, threadsToUse );

	internal async Task internalAssignJumpableCells( List<Cell> cells, JumpDefinition definition, int threadsToUse = 16 )
	{
		var allCells = cells;
		var cellsCount = allCells.Count();
		var cellsEachThread = (int)(cellsCount / threadsToUse);
		var lastThreadCount = cellsCount - (cellsEachThread * (threadsToUse - 1));
		List<Task> tasks = new();

		for ( int i = 0; i < threadsToUse; i++ )
		{
			var curentThread = i;

			tasks.Add( GameTask.RunInThreadAsync( () =>
			{
				var totalFraction = 1f;
				var cellsRange = curentThread == threadsToUse - 1 ? cellsEachThread : lastThreadCount;
				var cellsToCheck = allCells.Skip( cellsEachThread * curentThread ).Take( cellsRange );

				foreach ( var cell in cellsToCheck )
				{
					if ( totalFraction >= 1f )
					{
						List<Cell> connectedCells = new();
						List<AStarNode> jumpConnections = new();

						foreach ( var jumpableCell in cell.GetValidJumpables( definition, MaxDropHeight, IgnoreConnectionsForJumps, IgnoreLOSForJumps ) )
							if ( jumpableCell != null )
							{
								jumpConnections.Add( cell.AddConnection( jumpableCell, definition.Name ) );
								connectedCells.Add( jumpableCell );
							}

						foreach ( var jumpableConnection in connectedCells ) // Check if you can jump back onto the cell
						{
							var direction = (cell.Position - jumpableConnection.Position).WithZ( 0 ).Normal;
							var jumpbackCell = jumpableConnection.GetValidJumpable( definition, direction, MaxDropHeight, IgnoreConnectionsForJumps, IgnoreLOSForJumps );

							if ( jumpbackCell != null )
								if ( !IsDirectlyWalkable( jumpbackCell, cell ) )
								{
									var duplicate = false;
									foreach ( var connection in jumpConnections )
										if ( connection.Parent.Current == jumpbackCell && connection.MovementTag == definition.Name )
											duplicate = true;
									if ( !duplicate )
										jumpConnections.Add( jumpableConnection.AddConnection( jumpbackCell, definition.Name ) );
								}
						}

						foreach ( var connection in jumpConnections )
							if ( LineOfSight( connection.Parent.Current, connection.Current ) )
								connection.Parent.Current.RemoveConnection( connection );

						totalFraction = 0f;
					}

					totalFraction += definition.GenerateFraction;
				}
			} ) );
		}

		await GameTask.WhenAll( tasks );
	}

	public Vector3 TraceParabola( Vector3 startingPosition, Vector3 horizontalVelocity, float verticalSpeed, float gravity, float maxDropHeight, int subSteps = 2, bool draw = false )
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

			//Log.Info( $"Offset is: {horizontalOffset} and currentDistance: {currentDistance} Last position height was: {lastPositionChecked.z} and min height is: {minHeight} and the current offset is: {verticalOffset} so next position to check is: {nextPositionToCheck}" );

			var clearanceBBox = new BBox( new Vector3( -WidthClearance / 2f, -WidthClearance / 2f, StepSize ), new Vector3( WidthClearance / 2f, WidthClearance / 2f, HeightClearance ) );
			var jumpTrace = grid.Scene.Trace.Box( clearanceBBox, lastPositionChecked, nextPositionToCheck )
				.WithGridSettings( Settings )
				.Run();

			if ( draw )
			{
				DebugOverlay.Box( clearanceBBox.Translate( lastPositionChecked ), Color.Red, 5f );
				DebugOverlay.TraceResult( jumpTrace, 5f );
			}

			if ( jumpTrace.Hit )
			{
				//DebugOverlay.Box( clearanceBBox.Translate( jumpTrace.EndPosition ), Color.Blue, 5f );
				//var cell = Grid.Main.GetCellInArea( jumpTrace.EndPosition, WidthClearance );
				//if ( cell != null )
				//	cell.Draw( Color.Blue, 3f, false, false, true );
				return jumpTrace.EndPosition;
			}

			lastPositionChecked = nextPositionToCheck;
			currentDistance++;
		}

		return lastPositionChecked;
	}

	public void RemoveCells( BBox bounds, bool printInfo = false, bool broadcastToClients = false )
	{
		var cellsToRemove = GetCellsInBBox( bounds );
		var count = cellsToRemove.Count();

		foreach ( var cell in cellsToRemove )
			cell.Delete();

		if ( printInfo )
			Print( $"Removed {count} cells" );

		if ( broadcastToClients )
			if ( Game.IsServer )
				Grid.removeCellsClient( Identifier, bounds, printInfo );
	}

	public async Task GenerateCells( BBox bounds, int threadedChunkSides = 1, bool printInfo = true, bool broadcastToClients = false )
	{
		List<Task<List<Cell>>> tasks = new();
		var totalMins = bounds.Mins;
		var totalMaxs = bounds.Maxs;
		var totalSize = bounds.Size;

		if ( broadcastToClients )
			if ( Game.IsServer )
				Grid.generateCellsClient( Identifier, threadedChunkSides, bounds, printInfo );

		for ( int x = 1; x <= threadedChunkSides; x++ )
		{
			for ( int y = 1; y <= threadedChunkSides; y++ )
			{
				var xOffset = totalSize.x / threadedChunkSides * x - totalSize.x / threadedChunkSides / 2;
				var yOffset = totalSize.y / threadedChunkSides * y - totalSize.y / threadedChunkSides / 2;
				var offset = new Vector3( xOffset, yOffset );
				var chunkSize = totalSize / threadedChunkSides;
				var chunkMins = totalMins + offset - chunkSize / 2;
				var chunkMaxs = totalMins + offset + chunkSize / 2;
				var dividedBounds = new BBox( chunkMins.WithZ( totalMins.z ), chunkMaxs.WithZ( totalMaxs.z ) );

				tasks.Add( GameTask.RunInThreadAsync( () => createCells( dividedBounds, printInfo ) ) );
			}
		}

		await GameTask.WhenAll( tasks );

		foreach ( var task in tasks )
			foreach ( var cell in task.Result )
				AddCell( cell );
	}

	public async Task GenerateConnections( BBox bounds, float expandBoundsCheck = GridSettings.DEFAULT_DROP_HEIGHT, int threadedChunkSides = 4, bool printInfo = true, bool broadcastToClients = false )
	{
		bounds = new BBox( bounds.Mins - expandBoundsCheck - StepSize, bounds.Maxs + expandBoundsCheck + StepSize );

		await AssignEdgeCells( bounds, threadsToUse: threadedChunkSides * threadedChunkSides );

		if ( MaxDropHeight > 0 )
			await AssignDroppableCells( bounds, threadsToUse: threadedChunkSides * threadedChunkSides );

		if ( JumpDefinitions.Count() > 0 )
			foreach ( var definition in JumpDefinitions )
				await AssignJumpableCells( bounds, definition, threadsToUse: threadedChunkSides * threadedChunkSides );

		if ( broadcastToClients )
			if ( Game.IsServer )
				Grid.regenerateConnectionsClient( Identifier, bounds, expandBoundsCheck, threadedChunkSides, printInfo );
	}

	[ClientRpc]
	internal async static void regenerateConnectionsClient( string identifier, BBox bounds, float expandBoundsCheck = GridSettings.DEFAULT_DROP_HEIGHT, int threadedChunkSides = 4, bool printInfo = true )
	{
		var grid = Grids[identifier];

		if ( grid != null )
			await grid.GenerateConnections( bounds, expandBoundsCheck, threadedChunkSides, printInfo );
	}

	/// <summary>
	/// Create cells in that local bbox (Doesn't add them)
	/// </summary>
	/// <param name="bounds">Local bounds</param>
	/// <param name="printInfo"></param>
	private List<Cell> createCells( BBox bounds, bool printInfo = true )
	{
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
				var startPosition = WorldBounds.Mins.WithZ( WorldBounds.Maxs.z ) + new Vector3( row * CellSize + CellSize / 2f, column * CellSize + CellSize / 2f, Tolerance * 2f ) * AxisRotation;
				var endPosition = WorldBounds.Mins + new Vector3( row * CellSize + CellSize / 2f, column * CellSize + CellSize / 2f, -Tolerance ) * AxisRotation;
				var checkBBox = new BBox( new Vector3( -CellSize / 2f + Tolerance, -CellSize / 2f + Tolerance, 0f ), new Vector3( CellSize / 2f - Tolerance, CellSize / 2f - Tolerance, 0.001f ) );
				var positionTrace = grid.Scene.Trace.Box( checkBBox, startPosition, endPosition )
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

					while ( grid.Scene.Trace.TestPoint( startPosition, radius: CellSize / 2f - Tolerance ) )
						startPosition += Vector3.Down * HeightClearance;

					positionTrace = grid.Scene.Trace.Box( checkBBox, startPosition, endPosition )
						.WithGridSettings( Settings );

					positionResult = positionTrace.Run();
				}
			}
		}

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
	internal async static void generateCellsClient( string identifier, int threadedChunkSides, BBox bounds, bool printInfo = false )
	{
		var grid = Grids[identifier];

		if ( grid != null )
			await grid.GenerateCells( bounds, threadedChunkSides, printInfo );
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
	/// Returns all cells with that tag
	/// </summary>
	/// <param name="bounds"></param>
	/// <param name="tag"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTag( BBox bounds, string tag ) => GetCellsInBBox(bounds).Where( cell => cell.Tags.Has( tag ) );

	/// <summary>
	/// Returns all cells with those tags
	/// </summary>
	/// <param name="bounds"></param>
	/// <param name="tags"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTags( BBox bounds, params string[] tags ) => GetCellsInBBox( bounds ).Where( cell => cell.Tags.Has( tags ) );

	/// <summary>
	/// Returns all cells with those tags
	/// </summary>
	/// <param name="bounds"></param>
	/// <param name="tags"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithTags( BBox bounds, List<string> tags ) => GetCellsInBBox( bounds ).Where( cell => cell.Tags.Has( tags ) );

	/// <summary>
	/// Returns all connections with the movementTag
	/// </summary>
	/// <param name="movementTag"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithConnection( string movementTag ) => AllCells.Where( cell => cell.GetConnections( movementTag ).Count() > 0 );

	/// <summary>
	/// Returns all connections with the movementTag
	/// </summary>
	/// <param name="bounds"></param>
	/// <param name="movementTag"></param>
	/// <returns></returns>
	public IEnumerable<Cell> CellsWithConnection( BBox bounds, string movementTag ) => GetCellsInBBox( bounds ).Where( cell => cell.GetConnections( movementTag ).Count() > 0 );

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


*/

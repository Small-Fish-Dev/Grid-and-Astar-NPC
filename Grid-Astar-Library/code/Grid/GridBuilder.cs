using System.Collections.Generic;
using System.ComponentModel.Design;

namespace GridAStar;

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
	public float MaxDropHeight { get; private set; } = GridSettings.DEFAULT_DROP_HEIGHT;
	public bool AxisAligned { get; private set; } = false;
	public bool CylinderShaped { get; private set; } = false;
	public List<string> TagsToInclude { get; private set; } = new() { "solid" };
	public List<string> TagsToExclude { get; private set; } = new() { "player" };
	public Vector3 Position { get; private set; } = new();
	public BBox Bounds { get; private set; } = new();
	public Rotation Rotation { get; set; } = new();

	public GridBuilder()
	{
		var map = Game.PhysicsWorld.IsValid() ? Game.PhysicsWorld : null;
		var mapBounds = map == null ? new BBox( 0 ) : Game.PhysicsWorld.Body.GetBounds();
		Position = mapBounds.Center;
		Bounds = mapBounds;
	}

	/// <summary>
	///  By default the identifier is "main", which makes it useable with Grid.Main
	/// </summary>
	/// <param name="identifier"></param>
	public GridBuilder( string identifier ) : this()
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
	/// <param name="gridPerfect"></param>
	/// <returns></returns>
	public GridBuilder WithGridPerfect( bool gridPerfect )
	{
		GridPerfect = gridPerfect;
		if ( GridPerfect )
			StepSize = 0f;
		return this;
	}

	/// <summary>
	/// Ignore entities, only hit create cells on the world
	/// </summary>
	/// <param name="worldOnly"></param>
	/// <returns></returns>
	public GridBuilder WithWorldOnly( bool worldOnly )
	{
		WorldOnly = worldOnly;
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

	/// <summary>
	/// Set the position and bounds of the grid, by default it covers the entire map
	/// </summary>
	/// <param name="position"></param>
	/// <param name="bounds"></param>
	/// <returns></returns>
	public GridBuilder WithBounds( Vector3 position, BBox bounds )
	{
		Position = position;
		Bounds = bounds;
		return this;
	}

	/// <summary>
	/// Set the position, bounds, and rotation of the grid, by default it covers the entire map and is unrotated
	/// </summary>
	/// <param name="position"></param>
	/// <param name="bounds"></param>
	/// <param name="rotation"></param>
	/// <returns></returns>
	public GridBuilder WithBounds( Vector3 position, BBox bounds, Rotation rotation )
	{
		Position = position;
		Bounds = bounds;
		Rotation = rotation;
		return this;
	}

	/// <summary>
	/// Rotated the grid's bounds
	/// </summary>
	/// <param name="rotation"></param>
	/// <returns></returns>
	public GridBuilder WithRotation( Rotation rotation )
	{
		Rotation = rotation;
		return this;
	}

	/// <summary>
	/// The cells will be generated following this grid's rotation instead of the world's
	/// </summary>
	/// <param name="axisAligned"></param>
	/// <returns></returns>
	public GridBuilder WithAxisAligned( bool axisAligned )
	{
		AxisAligned = axisAligned;
		return this;
	}

	/// <summary>
	/// Generate the cells inside of a cylinder that fits the bounds instead of a cube (Squished if the bounds are)
	/// </summary>
	/// <param name="cylinderShaped"></param>
	/// <returns></returns>
	public GridBuilder WithCylinderShaped( bool cylinderShaped )
	{
		CylinderShaped = cylinderShaped;
		return this;
	}

	/// <summary>
	/// Maximum dropping height, you can select any dropping height in the pathbuilder
	/// </summary>
	/// <param name="maxDropHeight"></param>
	/// <returns></returns>
	public GridBuilder WithMaxDropHeight( float maxDropHeight )
	{
		MaxDropHeight = maxDropHeight;
		return this;
	}

	/// <summary>
	/// Creates a new grid with the settings given
	/// <returns></returns>
	public async Task<Grid> Create( bool printInfo = true )
	{
		Stopwatch totalWatch = new Stopwatch();
		totalWatch.Start();
		Stopwatch cellsWatch = new Stopwatch();
		cellsWatch.Start();

		var currentGrid = new Grid( this );

		var sides = 4;
		List<Task<List<Cell>>> tasks = new();
		var totalBounds = currentGrid.RotatedBounds;
		var totalMins = totalBounds.Mins;
		var totalMaxs = totalBounds.Maxs;
		var totalSize = totalBounds.Size;

		for ( int x = 1; x <= sides; x++ )
		{
			for ( int y = 1; y <= sides; y++ )
			{
				var xOffset = totalSize.x / sides * x - totalSize.x / sides / 2;
				var yOffset = totalSize.y / sides * y - totalSize.y / sides / 2;
				var offset = new Vector3( xOffset, yOffset );
				var chunkSize = totalSize / sides;
				var chunkMins = totalMins + offset - chunkSize / 2;
				var chunkMaxs = totalMins + offset + chunkSize / 2;
				var dividedBounds = new BBox( chunkMins.WithZ( totalMins.z ), chunkMaxs.WithZ( totalMaxs.z ) );

				tasks.Add( GameTask.RunInThreadAsync( () => currentGrid.CreateCells( dividedBounds, true ).ToList() ) ); // I gotta cast it into a list or else it doesn't get called??
			}
		}

		await GameTask.WhenAll( tasks );
		
		foreach ( var task in tasks )
			foreach ( var cell in task.Result )
				currentGrid.AddCell( cell );
		
		//await GameTask.RunInThreadAsync( () => currentGrid.CreateCells( currentGrid.RotatedBounds, printInfo ) );

		if ( printInfo )
			currentGrid.Print( "Creating grid" );

		cellsWatch.Stop();
		if ( printInfo )
			currentGrid.Print( $"Generated terrain cells in {cellsWatch.ElapsedMilliseconds}ms" );

		Stopwatch edgeCells = new Stopwatch();
		edgeCells.Start();
		currentGrid.AssignEdgeCells();
		edgeCells.Stop();
		if ( printInfo )
			currentGrid.Print( $"Assigned edge cells in {edgeCells.ElapsedMilliseconds}ms" );

		Stopwatch droppableCells = new Stopwatch();
		droppableCells.Start();
		currentGrid.AssignDroppableCells();
		droppableCells.Stop();
		if ( printInfo )
			currentGrid.Print( $"Assigned droppable cells in {droppableCells.ElapsedMilliseconds}ms" );

		Stopwatch jumpableCells = new Stopwatch();
		jumpableCells.Start();
		currentGrid.AssignJumpableCells( "shortjump", 200f, 300f, Game.PhysicsWorld.Gravity.z ); // TODO: Hands when translating the bounds into the void?
		jumpableCells.Stop();
		if ( printInfo )
			currentGrid.Print( $"Assigned jumpable cells in {jumpableCells.ElapsedMilliseconds}ms" );

		totalWatch.Stop();
		if ( printInfo )
			currentGrid.Print( $"Finished in {totalWatch.ElapsedMilliseconds}ms" );

		currentGrid.Initialize();

		return currentGrid;
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
		var maxDropHeightHashCode = MaxDropHeight.GetHashCode();
		var cylinderShapedHashCode = CylinderShaped.GetHashCode();
		var tagsToIncludeHashCode = string.Join( string.Empty, TagsToInclude ).GetHashCode();
		var tagsToExcludeHashCode = string.Join( string.Empty, TagsToExclude ).GetHashCode();

		var hashCodeFirst = HashCode.Combine( identifierHashCode, positionHashCode, boundsHashCode, rotationHashCode, axisAlignedHashCode, standableAngleHashCode, stepSizeHashCode, cellSizeHashCode );
		var hashCodeSecond = HashCode.Combine( cellSizeHashCode, heightClearanceHashCode, widthClearanceHashCode, gridPerfectHashCode, worldOnlyHashCode, maxDropHeightHashCode, cylinderShapedHashCode, tagsToIncludeHashCode );
		var hashCodeThird = HashCode.Combine( tagsToExcludeHashCode );

		return HashCode.Combine( hashCodeFirst, hashCodeSecond, hashCodeThird );
	}

}

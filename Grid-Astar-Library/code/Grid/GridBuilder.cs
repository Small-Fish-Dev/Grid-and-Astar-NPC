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
	public async Task<Grid> Create()
	{
		Stopwatch totalWatch = new Stopwatch();
		totalWatch.Start();

		var currentGrid = new Grid( this );

		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Creating grid {currentGrid.Identifier}" );

		var rotatedBounds = currentGrid.RotatedBounds;
		var worldBounds = currentGrid.WorldBounds;

		var minimumGrid = rotatedBounds.Mins.ToIntVector2( currentGrid.CellSize );
		var maximumGrid = rotatedBounds.Maxs.ToIntVector2( currentGrid.CellSize );
		var totalColumns = maximumGrid.y - minimumGrid.y;
		var totalRows = maximumGrid.x - minimumGrid.x;
		var minHeight = rotatedBounds.Mins.z;
		var maxHeight = rotatedBounds.Maxs.z;

		await GameTask.RunInThreadAsync( () =>
		{
			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Casting {(maximumGrid.y - minimumGrid.y) * (maximumGrid.x - minimumGrid.x)} cells. [{maximumGrid.x - minimumGrid.x}x{maximumGrid.y - minimumGrid.y}]" );

			for ( int column = 0; column < totalColumns; column++ )
			{
				for ( int row = 0; row < totalRows; row++ )
				{
					var startPosition = worldBounds.Mins.WithZ( worldBounds.Maxs.z ) + new Vector3( row * currentGrid.CellSize + currentGrid.CellSize / 2f, column * currentGrid.CellSize + currentGrid.CellSize / 2f, currentGrid.Tolerance * 2f ) * currentGrid.AxisRotation;
					var endPosition = worldBounds.Mins + new Vector3( row * currentGrid.CellSize + currentGrid.CellSize / 2f, column * currentGrid.CellSize + currentGrid.CellSize / 2f, -currentGrid.Tolerance ) * currentGrid.AxisRotation;
					var checkBBox = new BBox( new Vector3( -currentGrid.CellSize / 2f + currentGrid.Tolerance, -currentGrid.CellSize / 2f + currentGrid.Tolerance, 0f ), new Vector3( currentGrid.CellSize / 2f - currentGrid.Tolerance, currentGrid.CellSize / 2f - currentGrid.Tolerance, 0.001f ) );
					var positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition )
						.WithGridSettings( currentGrid.Settings );

					var positionResult = positionTrace.Run();

					while ( positionResult.Hit && startPosition.z >= endPosition.z )
					{
						if ( currentGrid.IsInsideBounds( positionResult.HitPosition ) )
						{
							if ( !currentGrid.CylinderShaped || currentGrid.IsInsideCylinder( positionResult.HitPosition ) )
							{
								var angle = Vector3.GetAngle( Vector3.Up, positionResult.Normal );
								if ( angle <= currentGrid.StandableAngle )
								{
									var newCell = Cell.TryCreate( currentGrid, positionResult.HitPosition );

									if ( newCell != null )
										currentGrid.AddCell( newCell );
								}
							}
						}

						startPosition = positionResult.HitPosition + Vector3.Down * currentGrid.HeightClearance;

						while ( Sandbox.Trace.TestPoint( startPosition, radius: currentGrid.CellSize / 2f - currentGrid.Tolerance ) )
							startPosition += Vector3.Down * currentGrid.HeightClearance;

						positionTrace = Sandbox.Trace.Box( checkBBox, startPosition, endPosition )
						.WithAllTags( currentGrid.Settings.TagsToInclude.ToArray() )
						.WithoutTags( currentGrid.Settings.TagsToExclude.ToArray() );

						if ( currentGrid.WorldOnly )
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
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {currentGrid.Identifier} assigned edge cells in {edgeCells.ElapsedMilliseconds}ms" );

		Stopwatch droppableCells = new Stopwatch();
		droppableCells.Start();
		currentGrid.AssignDroppableCells();
		droppableCells.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {currentGrid.Identifier} assigned droppable cells in {droppableCells.ElapsedMilliseconds}ms" );

		Stopwatch jumpableCells = new Stopwatch();
		jumpableCells.Start();
		currentGrid.AssignJumpableCells();
		jumpableCells.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {currentGrid.Identifier} assigned jumpable cells in {jumpableCells.ElapsedMilliseconds}ms" );

		totalWatch.Stop();
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {currentGrid.Identifier} created in {totalWatch.ElapsedMilliseconds}ms" );

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

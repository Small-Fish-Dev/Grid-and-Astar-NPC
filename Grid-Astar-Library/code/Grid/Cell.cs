namespace GridAStar;

public struct CellTags
{
	public List<string> All { get; }

	public CellTags()
	{
		All = new List<string>();
	}
	public CellTags( List<string> tags )
	{
		All = new List<string>( tags );
	}

	public bool Has( string tag ) => All.Contains( tag );

	public bool Has( params string[] tags )
	{
		foreach ( string tag in tags )
			if ( !Has( tag ) )
				return false;
		return true;
	}

	public bool Has( List<string> tags )
	{
		foreach ( string tag in tags )
			if ( Has( tag ) )
				return true;
		return false;
	}

	public void Add( string tag )
	{
		if ( !Has( tag ) )
			All.Add( tag );
	}

	public void Remove( string tag )
	{
		if ( Has( tag ) )
			All.Remove( tag );
	}

	public void Clear()
	{
		All.Clear();
	}
}

public struct CellConnection
{
	public Cell Cell { get; private set; }
	public string ConnectionTag { get; private set; } = string.Empty;

	public CellConnection( Cell cell )
	{
		Cell = cell;
	}

	public CellConnection( Cell cell, string tag )
	{
		Cell = cell;
		ConnectionTag = tag;
	}
}

public partial class Cell : IEquatable<Cell>, IValid
{
	/// <summary>
	/// The parent grid
	/// </summary>
	public Grid Grid { get; set; }
	public Rotation Rotation => Grid.AxisAligned ? new Rotation() : Grid.Rotation;
	public Vector3 Position { get; set; }
	public IntVector2 GridPosition { get; set; }
	/// <summary>
	/// Since we know the size of each cell, all we need to define is the height of each vertices
	/// 0 = Bottom Left
	/// 1 = Bottom Right
	/// 2 = Top Left
	/// 3 = Top Right
	/// </summary>
	public float[] Vertices = new float[4];
	// Note: There is no performance boost in having the variables below being set in the constructor
	/// <summary>
	/// Get the point with both minimum coordinates
	/// </summary>
	public Vector3 BottomLeft => Position.WithZ( Vertices[0] ) + new Vector3( -Grid.CellSize / 2, -Grid.CellSize / 2, 0f ) * Rotation;
	/// <summary>
	/// Get the point with minimum x and maximum y
	/// </summary>
	public Vector3 BottomRight => Position.WithZ( Vertices[1] ) + new Vector3( -Grid.CellSize / 2, Grid.CellSize / 2, 0f ) * Rotation;
	// Get the point with maxinum x and minimum y
	public Vector3 TopLeft => Position.WithZ( Vertices[2] ) + new Vector3( Grid.CellSize / 2, -Grid.CellSize / 2, 0f ) * Rotation;
	/// <summary>
	/// Get the point with both maximum coordinates
	/// </summary>
	public Vector3 TopRight => Position.WithZ( Vertices[3] ) + new Vector3( Grid.CellSize / 2, Grid.CellSize / 2, 0f ) * Rotation;
	public float Height => Vertices.Max() - Vertices.Min();
	public Vector3 Bottom => Position.WithZ( Vertices.Min() );
	public BBox Bounds => new BBox( new Vector3( -Grid.WidthClearance, -Grid.WidthClearance, 0f ), new Vector3( Grid.WidthClearance, Grid.WidthClearance, Grid.HeightClearance ) );
	public BBox WorldBounds => new BBox( (Position + Bounds.Mins).WithZ( Vertices.Min() ), Position + Bounds.Maxs );
	public CellTags Tags { get; private set; }
	public List<AStarNode> CellConnections { get; private set; } = new();
	private List<AStarNode> connectedCells = new();


	public bool Occupied
	{
		get => Tags.Has( "occupied" );
		set
		{
			if ( value )
				Tags.Add( "occupied" );
			else
				Tags.Remove( "occupied" );
		}
	}
	public Entity OccupyingEntity { get; set; } = null;
	internal Transform currentOccupyingTransform { get; set; } = Transform.Zero;
	bool IValid.IsValid { get; }

	/// <summary>
	/// Try to create a new cell with the given position and the max standing angle
	/// </summary>
	/// <param name="grid">Parent grid</param>
	/// <param name="position">Starting center of the cell</param>
	/// <returns></returns>
	public static Cell TryCreate( Grid grid, Vector3 position )
	{
		float[] validCoordinates = new float[4];
		var height = position.z - validCoordinates.Min();

		var coordinatesAndStairs = TraceCoordinates( grid, position, ref validCoordinates );
		if ( !coordinatesAndStairs.Item1 )
			return null;

		if ( !TestForClearance( grid, position, height ) )
			return null;

		var cell = new Cell( grid, position, validCoordinates );
		if ( coordinatesAndStairs.Item2 )
			cell.Tags.Add( "step" );

		return cell;
	}

	//(IsWalkable, IsSteps)
	private static (bool, bool) TraceCoordinates( Grid grid, Vector3 position, ref float[] validCoordinates )
	{
		Vector3[] testCoordinates = new Vector3[4] {
			new Vector3( -grid.CellSize / 2, -grid.CellSize / 2 ) * grid.AxisRotation,
			new Vector3( -grid.CellSize / 2, grid.CellSize / 2 ) * grid.AxisRotation,
			new Vector3( grid.CellSize / 2, -grid.CellSize / 2 ) * grid.AxisRotation,
			new Vector3( grid.CellSize / 2, grid.CellSize / 2 ) * grid.AxisRotation
		};

		var maxHeight = Math.Max( grid.CellSize * MathF.Tan( MathX.DegreeToRadian( grid.StandableAngle ) ), grid.StepSize );

		for ( int i = 0; i < 4; i++ )
		{
			var centerDir = testCoordinates[i].Normal; // Test a little closer to the center, for grid-perfect terrain
			var startTestPos = position + testCoordinates[i].WithZ( maxHeight * 2f ) - centerDir * grid.Tolerance;
			var endTestPos = position + testCoordinates[i].WithZ( -maxHeight * 2f ) - centerDir * grid.Tolerance;
			var testTrace = Sandbox.Trace.Ray( startTestPos, endTestPos )
				.WithGridSettings( grid.Settings );
			var testResult = testTrace.Run();

			if ( testResult.StartedSolid ) return (false, false);
			if ( !testResult.Hit ) return (false, false);
			if ( testResult.HitPosition.z > position.z + grid.Tolerance ) return (false, false); // How did it generate higher than the initial wide cast?

			validCoordinates[i] = testResult.HitPosition.z;
			testCoordinates[i] = testResult.HitPosition;
		}

		var orderedByHeight = testCoordinates.OrderBy( x => x.z );
		var lowest = orderedByHeight.First();
		var highest = orderedByHeight.Last();

		if ( IsCliff( grid, lowest, highest ) || IsCliff( grid, lowest, position ) )
			return (false, false); // Oops we traced over a hill but thought it was valid before

		return TestForSteps( grid, position, testCoordinates );
	}

	private static bool IsCliff( Grid grid, Vector3 from, Vector3 to )
	{
		var trace = Sandbox.Trace.Ray( from, to )
			.WithGridSettings( grid.Settings );
		var result = trace.Run();

		if ( result.Hit )
			if ( Vector3.GetAngle( Vector3.Up, result.Normal ) > 90 )
				return true;

		return false;
	}

	private static bool TestForClearance( Grid grid, Vector3 position, float height )
	{
		var clearanceBBox = new BBox( new Vector3( -grid.WidthClearance / 2f, -grid.WidthClearance / 2f, 0f ), new Vector3( grid.WidthClearance / 2f, grid.WidthClearance / 2f, 1f ) );
		var startPos = position + Vector3.Up * grid.HeightClearance;
		var clearanceTrace = Sandbox.Trace.Box( clearanceBBox, startPos, position + Vector3.Up * grid.StepSize )
			.WithGridSettings( grid.Settings );

		var clearanceResult = clearanceTrace.Run();
		var heightDifference = clearanceResult.EndPosition.z - (position.z - height);

		return heightDifference <= grid.StepSize + height;
	}


	//(IsWalkable, IsSteps)
	private static (bool, bool) TestForSteps( Grid grid, Vector3 position, Vector3[] testCoordinates )
	{
		if ( grid.StepSize <= 0.1f ) // At this point why bother
			return (true, true);

		var lowestToHighest = testCoordinates
			.OrderBy( x => x.z )
			.ToArray();

		var stepTestMin = TestForStep( grid, lowestToHighest[0], lowestToHighest[3], position, lowestToHighest[0] );

		if ( !stepTestMin.Item1 )
			return (false, stepTestMin.Item2);

		var stepTestMid = TestForStep( grid, lowestToHighest[1], lowestToHighest[3], position, lowestToHighest[1] );

		if ( !stepTestMid.Item1 )
			return (false, stepTestMid.Item2);

		return (true, stepTestMin.Item2 || stepTestMid.Item2);
	}

	//(IsWalkable, IsSteps)
	private static (bool, bool) TestForStep( Grid grid, Vector3 startPosition, Vector3 endPosition, Vector3 highestPosition, Vector3 lowestPosition )
	{
		var stepsTried = 0;
		var maxSteps = (int)Math.Max( (Math.Abs( highestPosition.z - lowestPosition.z ) / (grid.StepSize / 2f)) + 1, 3 );
		var stepDistances = new float[maxSteps];

		if ( highestPosition.z - lowestPosition.z <= grid.StepSize / 2 ) // No stairs here
			return (true, false);

		while ( stepsTried < maxSteps )
		{
			var tolerance = 0.01f;
			var stepPositionStart = startPosition + Vector3.Up * (grid.StepSize / 4f + grid.StepSize / 2f * stepsTried + tolerance);
			var stepPositionEnd = endPosition.WithZ( stepPositionStart.z );
			var stepDirection = (stepPositionEnd - stepPositionStart).Normal;
			var stepDistance = stepPositionStart.Distance( stepPositionEnd );
			var stepTrace = Sandbox.Trace.Ray( stepPositionStart, stepPositionStart + stepDirection * (stepDistance + tolerance * 2f) )
				.Size( grid.StepSize / 2f )
				.WithGridSettings( grid.Settings );

			var stepResult = stepTrace.Run();
			var stepAngle = Vector3.GetAngle( Vector3.Up, stepResult.Normal );

			if ( stepsTried == 0 )
				if ( stepResult.EndPosition.Distance( endPosition ) <= tolerance * 3f ) // Pack it up, no stairs here
					return (true, false);

			if ( stepResult.Hit && stepAngle > grid.StandableAngle && stepAngle < 89.9f ) // MoveHelper straight up doesn't count it as a step if it's not 90°
				return (false, false);

			if ( stepResult.Hit && stepAngle < grid.StandableAngle ) // Guess not a step but just a slope
				return (true, false);

			var distanceFromStart = startPosition.Distance( stepResult.EndPosition.WithZ( startPosition.z ) );

			if ( stepsTried >= 2 )
			{
				var distanceDifference = Math.Abs( distanceFromStart - stepDistances[stepsTried - 2] );

				if ( distanceDifference < tolerance )
					return (false, true);
			}

			stepDistances[stepsTried] = distanceFromStart;
			stepsTried++;
		}

		return (true, true);
	}

	public void AddConnection( Cell other, string tag = "" )
	{
		var node = new AStarNode( other, new AStarNode( this ), tag == "" ? string.Empty : tag );
		CellConnections.Add( node );
		other.connectedCells.Add( node );
	}

	public void RemoveConnections( Cell other )
	{
		var foundConnections = CellConnections.Where( x => x.Current == other ).ToList();

		foreach ( var connection in foundConnections )
		{
			CellConnections.Remove( connection );
			other.connectedCells.Remove( connection );
		}
	}

	public void SetOccupant( Entity entity )
	{
		OccupyingEntity = entity;
		currentOccupyingTransform = entity.Transform;
	}

	public void RemoveOccupant()
	{
		OccupyingEntity = null;
		currentOccupyingTransform = Transform.Zero;
	}

	public bool TestForOccupancy( string tag )
	{
		if ( OccupyingEntity != null && OccupyingEntity.Transform == currentOccupyingTransform ) return Occupied;

		var occupyTrace = Sandbox.Trace.Box( Bounds, Position, Position )
			.DynamicOnly()
			.WithTag( tag );

		var occupyResult = occupyTrace.Run();

		if ( occupyResult.Entity != null )
			SetOccupant( occupyResult.Entity );

		return occupyResult.Hit;
	}

	public Cell( Grid grid, Vector3 position, float[] vertices, List<string> tags = null )
	{
		Grid = grid;
		Position = position;
		GridPosition = (Position - Grid.WorldBounds.Mins - grid.CellSize / 2).ToIntVector2( grid.CellSize );
		Vertices = vertices;

		if ( tags != null && tags.Count() > 0 )
			Tags = new CellTags( tags );
		else
			Tags = new CellTags();
	}

	public void Delete( bool deleteConnections = true )
	{
		if ( deleteConnections )
		{
			var connections = connectedCells.ToList();
			foreach ( var connectedCell in connections )
				connectedCell.Parent.Current.RemoveConnections( this );
			connectedCells.Clear();
			CellConnections.Clear();
		}

		Grid.CellStacks[GridPosition].Remove( this );
	}

	// Perhaps there's a way to check these automatically, but I tried! :-)
	internal static Dictionary<IntVector2, List<IntVector2>> CompareVertices = new()
	{
		[new IntVector2( -1, -1 )] = new List<IntVector2>() { new IntVector2( 0, 3 ) },
		[new IntVector2( -1, 0 )] = new List<IntVector2>() { new IntVector2( 1, 3 ), new IntVector2( 0, 2 ) },
		[new IntVector2( -1, 1 )] = new List<IntVector2>() { new IntVector2( 1, 2 ) },
		[new IntVector2( 0, -1 )] = new List<IntVector2>() { new IntVector2( 0, 1 ), new IntVector2( 2, 3 ) },
		[new IntVector2( 0, 1 )] = new List<IntVector2>() { new IntVector2( 1, 0 ), new IntVector2( 3, 2 ) },
		[new IntVector2( 1, -1 )] = new List<IntVector2>() { new IntVector2( 2, 1 ) },
		[new IntVector2( 1, 0 )] = new List<IntVector2>() { new IntVector2( 3, 1 ), new IntVector2( 2, 0 ) },
		[new IntVector2( 1, 1 )] = new List<IntVector2>() { new IntVector2( 3, 0 ) },
	};

	public bool IsNeighbour( Cell cell )
	{
		var xDistance = cell.GridPosition.x - GridPosition.x;
		var yDistance = cell.GridPosition.y - GridPosition.y;

		if ( xDistance < -1 || xDistance > 1 || yDistance < -1 || yDistance > 1 ) return false;
		if ( cell == this ) return true;
		if ( xDistance == 0 && yDistance == 0 ) return false;

		var verticesToCompare = CompareVertices[new IntVector2( xDistance, yDistance )];

		// Compare neighbouring vertices to check if they are in the same position
		foreach ( var comparePair in verticesToCompare )
		{
			var heightDifference = Math.Abs( Vertices[comparePair[0]] - cell.Vertices[comparePair[1]] );
			if ( heightDifference > Grid.StepSize ) return false;
		}

		return true;
	}

	/// <summary>
	/// Return all neighboring cells that are directly connected
	/// </summary>
	/// <param name="ignoreHeight"></param>
	/// <returns></returns>
	public IEnumerable<Cell> GetNeighbours( bool ignoreHeight = false )
	{
		var height = ignoreHeight ? float.MaxValue : Position.z;

		for ( int y = -1; y <= 1; y++ )
		{
			for ( int x = -1; x <= 1; x++ )
			{
				if ( x == 0 && y == 0 ) continue;

				var cellFound = Grid.GetCell( new IntVector2( GridPosition.x + x, GridPosition.y + y ), height );
				if ( cellFound == null ) continue;

				if ( IsNeighbour( cellFound ) )
					yield return cellFound;
			}
		}
	}

	public Cell GetClosestNeighbour( Vector3 position ) => GetNeighbours().OrderBy( x => x.Position.Distance( position ) ).FirstOrDefault();
	public Cell GetClosestNeighbourAndConnection( Vector3 position ) => GetNeighbourAndConnections().OrderBy( x => x.Current.Position.Distance( position ) ).FirstOrDefault()?.Current ?? null;

	/// <summary>
	/// Returns all neighbours and connected cells where you can travel to from this cell
	/// </summary>
	/// <param name="ignoreHeight"></param>
	/// <returns></returns>
	public IEnumerable<AStarNode> GetNeighbourAndConnections( bool ignoreHeight = false )
	{
		return GetNeighbours( ignoreHeight )
			.Select( x => new AStarNode( x ) )
			.Concat( CellConnections );
	}

	/// <summary>
	/// Return the first cell below spaces where a neighbour is missing
	/// </summary>
	/// <param name="minCellDistance"></param>
	/// <param name="maxCellsDistance"></param>
	/// <param name="maxHeightDistance"></param>
	/// <returns></returns>
	public Cell GetFirstValidDroppable( int minCellDistance = 1, int maxCellsDistance = 3, float maxHeightDistance = GridSettings.DEFAULT_DROP_HEIGHT )
	{
		for ( int y = 0; y <= maxCellsDistance * 2; y++ )
		{
			var spiralY = MathAStar.SpiralPattern( y );
			for ( int x = 0; x <= maxCellsDistance * 2; x++ )
			{
				var spiralX = MathAStar.SpiralPattern( x );
				if ( spiralX == 0 && spiralY == 0 ) continue;
				if ( Math.Abs( spiralX ) <= minCellDistance && Math.Abs( spiralY ) <= minCellDistance ) continue;

				var cellFound = Grid.GetCell( new IntVector2( GridPosition.x + spiralX, GridPosition.y + spiralY ), Position.z );

				if ( cellFound == null ) continue; // Ignore if there's no cell
				if ( cellFound == this ) continue; // Ignore if it's the same cell
				if ( IsNeighbour( cellFound ) ) continue; // Ignore if the cell is touching

				var verticalDistance = Position.z - cellFound.Position.z;
				if ( verticalDistance > maxHeightDistance ) continue; // Ignore if it's too high

				var horizontalDistance = new Vector2( spiralX, spiralY ).Length - 1f;
				if ( verticalDistance < Grid.StepSize * horizontalDistance ) continue; // It's probably a step already here

				if ( Grid.LineOfSight( this, cellFound ) ) continue; // Ignore if the cell cal be walked to

				// Check if you can walk off the edge
				var clearanceBBox = new BBox( new Vector3( -Grid.WidthClearance / 2f, -Grid.WidthClearance / 2f, 0f ), new Vector3( Grid.WidthClearance / 2f, Grid.WidthClearance / 2f, Grid.HeightClearance - Grid.StepSize ) );
				var horizontalClearanceTrace = Sandbox.Trace.Box( clearanceBBox, Position + Vector3.Up * Grid.StepSize, cellFound.Position.WithZ( Position.z + Grid.StepSize ) )
					.WithGridSettings( Grid.Settings )
					.Run();
				if ( horizontalClearanceTrace.Hit ) continue; // Ignore if you can't walk off the edge

				// Check if you can drop down
				var verticalClearanceTrace = Sandbox.Trace.Box( clearanceBBox, cellFound.Position.WithZ( Position.z + Grid.StepSize ), cellFound.Position + Vector3.Up * Grid.StepSize )
					.WithGridSettings( Grid.Settings )
					.Run();
				if ( verticalClearanceTrace.Hit ) continue; // Ignore if you can't drop down

				return cellFound;
			}
		}

		return null;
	}

	public IEnumerable<Cell> GetValidJumpables( JumpDefinition definition, float maxHeightDistance = GridSettings.DEFAULT_DROP_HEIGHT )
	{
		var jumpableCells = new List<Cell>();

		for ( int side = 0; side < definition.SidesToCheck; side++ )
		{
			var directionToCheck = Rotation.FromYaw( definition.AngleOffset + 360 / definition.SidesToCheck * side ).Forward;
			var horizontalVelocity = directionToCheck * definition.HorizontalSpeed;

			var endPosition = Grid.TraceParabola( Position, horizontalVelocity, definition.VerticalSpeed, definition.Gravity, maxHeightDistance );
			var cell = Grid.GetCellInArea( endPosition, Grid.WidthClearance );

			if ( cell == null || cell == this ) continue;

			if ( !Grid.IsDirectlyWalkable( this, cell ) && !Grid.IsDirectlyWalkable( this, cell, withConnections: false ) )
				if ( !jumpableCells.Any( otherCell => Grid.IsDirectlyWalkable( otherCell, cell ) ) && !jumpableCells.Any( otherCell => Grid.IsDirectlyWalkable( otherCell, cell, withConnections: false ) ) )
					if ( !CellConnections.Any( otherNode => Grid.IsDirectlyWalkable( otherNode.Current, cell ) ) && !CellConnections.Any( otherNode => Grid.IsDirectlyWalkable( otherNode.Current, cell, withConnections: false ) ) )
					{
						var clearanceBBox = new BBox( new Vector3( -Grid.WidthClearance / 2f, -Grid.WidthClearance / 2f, Grid.StepSize ), new Vector3( Grid.WidthClearance / 2f, Grid.WidthClearance / 2f, Grid.HeightClearance ) );
						var jumpTrace = Sandbox.Trace.Box( clearanceBBox, endPosition, cell.Position )
							.WithGridSettings( Grid.Settings )
							.Run();

						if ( !jumpTrace.Hit )
							jumpableCells.Add( cell );
					}

			if ( jumpableCells.Count() >= definition.MaxPerCell )
				break;
		}

		return jumpableCells;
	}


	public Cell GetValidJumpable( JumpDefinition definition, Vector3 directionToCheck, float maxHeightDistance = GridSettings.DEFAULT_DROP_HEIGHT )
	{
		var horizontalVelocity = directionToCheck * definition.HorizontalSpeed;

		var endPosition = Grid.TraceParabola( Position, horizontalVelocity, definition.VerticalSpeed, definition.Gravity, maxHeightDistance );
		var cell = Grid.GetCellInArea( endPosition, Grid.WidthClearance );

		if ( cell == null ) return null;

		if ( !Grid.IsDirectlyWalkable( this, cell ) && !Grid.IsDirectlyWalkable( this, cell, withConnections: false ) )
			if ( !CellConnections.Any( otherNode => Grid.IsDirectlyWalkable( otherNode.Current, cell ) ) && !CellConnections.Any( otherNode => Grid.IsDirectlyWalkable( otherNode.Current, cell, withConnections: false ) ) )
			{
				var clearanceBBox = new BBox( new Vector3( -Grid.WidthClearance / 2f, -Grid.WidthClearance / 2f, Grid.StepSize ), new Vector3( Grid.WidthClearance / 2f, Grid.WidthClearance / 2f, Grid.HeightClearance ) );
				var jumpTrace = Sandbox.Trace.Box( clearanceBBox, endPosition, cell.Position )
					.WithGridSettings( Grid.Settings )
					.Run();

				if ( !jumpTrace.Hit )
					return cell;
			}

		return null;
	}

	/// <summary>
	/// Draw this cell with color
	/// </summary>
	/// <param name="color"></param>
	/// <param name="duration"></param>
	/// <param name="depthTest"></param>
	/// <param name="drawCenter">Draw a point on the cell's position</param>
	/// <param name="drawCross">Draw diagonal lines</param>
	/// <param name="drawCoordinates">Draw coordinates</param>
	public void Draw( Color color, float duration = 0f, bool depthTest = true, bool drawCenter = false, bool drawCross = false, bool drawCoordinates = false )
	{
		DebugOverlay.Line( BottomLeft, BottomRight, color, duration, depthTest );
		DebugOverlay.Line( BottomRight, TopRight, color, duration, depthTest );
		DebugOverlay.Line( TopRight, TopLeft, color, duration, depthTest );
		DebugOverlay.Line( TopLeft, BottomLeft, color, duration, depthTest );

		if ( drawCenter )
			DebugOverlay.Sphere( Position, 1f, color, duration, depthTest );

		if ( drawCross )
		{
			var bottomLeftDir = (BottomLeft - TopRight) / 4f;
			var topLeftDir = (TopLeft - BottomRight) / 4f;
			DebugOverlay.Line( BottomLeft - bottomLeftDir, TopRight + bottomLeftDir, color, duration, depthTest );
			DebugOverlay.Line( TopLeft - topLeftDir, BottomRight + topLeftDir, color, duration, depthTest );
		}

		if ( drawCoordinates )
			DebugOverlay.Text( $"{GridPosition}", Position, duration, 200 );

		int index = 0;
		foreach ( var tag in Tags.All )
		{
			DebugOverlay.Text( $"{tag}", Position, index, color, duration, 200 );
			index++;
		}
	}

	/// <summary>
	/// Draw this cell
	/// </summary>
	/// <param name="duration"></param>
	/// <param name="depthTest"></param>
	/// <param name="drawCenter">Draw a point on the cell's position</param>
	/// <param name="drawCross">Draw diagonal lines</param>
	public void Draw( float duration = 0f, bool depthTest = true, bool drawCenter = false, bool drawCross = false )
	{
		var color = Occupied ? Color.Red : Color.White;

		DebugOverlay.Line( BottomLeft, BottomRight, color, duration, depthTest );
		DebugOverlay.Line( BottomRight, TopRight, color, duration, depthTest );
		DebugOverlay.Line( TopRight, TopLeft, color, duration, depthTest );
		DebugOverlay.Line( TopLeft, BottomLeft, color, duration, depthTest );

		if ( drawCenter )
			DebugOverlay.Sphere( Position, 5f, color, duration, depthTest );

		if ( drawCross )
		{
			DebugOverlay.Line( BottomLeft, TopRight, color, duration, depthTest );
			DebugOverlay.Line( TopLeft, BottomRight, color, duration, depthTest );
		}
	}


	public override bool Equals( object obj )
	{
		return Equals( obj as Cell );
	}

	public bool Equals( Cell obj )
	{
		return obj != null && obj.GetHashCode() == this.GetHashCode();
	}

	public override int GetHashCode()
	{
		var gridHash = Grid.GetHashCode();
		var positionHash = Position.GetHashCode();

		return gridHash + positionHash;
	}
}

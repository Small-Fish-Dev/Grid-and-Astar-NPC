namespace GridAStar;

public partial class Cell
{
	/// <summary>
	/// The parent grid
	/// </summary>
	public Grid Grid { get; set; }
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
	public Vector3 BottomLeft => Position.WithZ( Vertices[0] ) + new Vector3( -Grid.CellSize / 2, -Grid.CellSize / 2, 0f );
	/// <summary>
	/// Get the point with minimum x and maximum y
	/// </summary>
	public Vector3 BottomRight => Position.WithZ( Vertices[1] ) + new Vector3( -Grid.CellSize / 2, Grid.CellSize / 2, 0f );
	// Get the point with maxinum x and minimum y
	public Vector3 TopLeft => Position.WithZ( Vertices[2] ) + new Vector3( Grid.CellSize / 2, -Grid.CellSize / 2, 0f );
	/// <summary>
	/// Get the point with both maximum coordinates
	/// </summary>
	public Vector3 TopRight => Position.WithZ( Vertices[3] ) + new Vector3( Grid.CellSize / 2, Grid.CellSize / 2, 0f );
	public float Height => Vertices.Max() - Vertices.Min();
	public bool Occupied { get; set; } = false;

	/// <summary>
	/// Try to create a new cell with the given position and the max standing angle
	/// </summary>
	/// <param name="grid">Parent grid</param>
	/// <param name="position">Starting center of the cell</param>
	/// <param name="worldOnly">If the parent cell is on a slope</param>
	/// <returns></returns>
	public static Cell TryCreate( Grid grid, Vector3 position, bool worldOnly = true )
	{

		float[] validCoordinates = new float[4];

		if ( !TraceCoordinates( position, ref validCoordinates, grid.CellSize, grid.StandableAngle, grid.StepSize, worldOnly ) )
			return null;
		
		return new Cell( grid, position, validCoordinates );
	}

	private static bool TraceCoordinates( Vector3 position, ref float[] validCoordinates, float cellSize, float standableAngle, float stepSize, bool worldOnly )
	{
		Vector3[] testCoordinates = new Vector3[4] {
			new Vector3( -cellSize / 2, -cellSize / 2 ),
			new Vector3( -cellSize / 2, cellSize / 2 ),
			new Vector3( cellSize / 2, -cellSize / 2 ),
			new Vector3( cellSize / 2, cellSize / 2 ) };

		var maxHeight = Math.Max( cellSize * MathF.Tan( MathX.DegreeToRadian( standableAngle ) ), stepSize );

		// TODO: Maybe borrow the two adjacent coordinates from parent cell?
		for ( int i = 0; i < 4; i++ )
		{
			var testTrace = Sandbox.Trace.Ray( position + testCoordinates[i].WithZ(maxHeight * 2f), position + testCoordinates[i].WithZ(-maxHeight * 2f) );

			if ( worldOnly )
				testTrace.WorldOnly();
			else
				testTrace.WorldAndEntities();

			var testResult = testTrace.Run();

			if ( testResult.StartedSolid ) return false;
			if ( !testResult.Hit ) return false;

			validCoordinates[i] = testResult.HitPosition.z;
		}

		var difference1 = Math.Abs( validCoordinates[0] - validCoordinates[3] );
		var difference2 = Math.Abs( validCoordinates[1] - validCoordinates[2] );
		var differenceMedian = (difference1 + difference2) / 2f;

		if ( differenceMedian > maxHeight )
			return false;

		/*var bbox = new BBox( new Vector3( -cellSize / 2, -cellSize / 2, 0f ), new Vector3( cellSize / 2, cellSize / 2, 48f ) );
		var occupyTrace = Sandbox.Trace.Box( bbox, position.WithZ( validCoordinates.Max() + 1f ) + Vector3.Up * 48f, position.WithZ( validCoordinates.Max() + 1f ) );

		
		if ( worldOnly )
			occupyTrace.WorldOnly();
		else
			occupyTrace.WorldAndEntities()
			.WithoutTags( "NoGridOccupancy" );

		var occupyResult = occupyTrace.Run();

		if ( occupyResult.Hit )
			return false;
		*/
		return true;
	}

	public Cell( Grid grid, Vector3 position, float[] vertices )
	{
		Grid = grid;
		Position = position;
		GridPosition = Position.ToIntVector2( grid.CellSize );
		Vertices = vertices;

		/*var bbox = new BBox( new Vector3( -grid.CellSize / 2, -grid.CellSize / 2, 0f ), new Vector3( grid.CellSize / 2, grid.CellSize / 2, 48f ) );
		var occupyTrace = Sandbox.Trace.Box( bbox, Position + Vector3.Up * 48f, Position)
			.WithoutTags( "NoGridOccupancy" )
			.EntitiesOnly()
			.Run();

		if ( occupyTrace.Hit )
			Occupied = true;*/
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
		if ( xDistance == 0 && yDistance == 0 ) return false;

		var verticesToCompare = CompareVertices[new IntVector2( xDistance, yDistance )];

		// Compare neighbouring vertices to check if they are in the same position
		foreach ( var comparePair in verticesToCompare )
		{
			var heightDifference = Math.Abs( Vertices[comparePair[0]] - cell.Vertices[comparePair[1]] );
			if ( heightDifference >= 0.1f ) return false;
		}

		return true;
	}

	public List<Cell> GetNeighbours( bool ignoreHeight = false )
	{
		var neighbours = new List<Cell>();
		
		for( int y = -1; y <= 1; y++ )
		{
			for ( int x = -1; x <= 1; x++ )
			{
				if ( x == 0 && y == 0 ) continue;

				var cellFound = Grid.GetCell( new IntVector2( GridPosition.x + x, GridPosition.y + y ), ignoreHeight ? float.MaxValue : Position.z );
				if ( cellFound == null ) continue;

				if ( IsNeighbour( cellFound ) )
					neighbours.Add( cellFound );
			}
		}

		return neighbours;
	}

	/// <summary>
	/// Draw this cell with color
	/// </summary>
	/// <param name="color"></param>
	/// <param name="duration"></param>
	/// <param name="depthTest"></param>
	/// <param name="drawCenter">Draw a point on the cell's position</param>
	/// <param name="drawCross">Draw diagonal lines</param>
	public void Draw( Color color, float duration = 0f, bool depthTest = true, bool drawCenter = false, bool drawCross = false )
	{
		DebugOverlay.Line( BottomLeft, BottomRight, color, duration, depthTest );
		DebugOverlay.Line( BottomRight, TopRight, color, duration, depthTest );
		DebugOverlay.Line( TopRight, TopLeft, color, duration, depthTest );
		DebugOverlay.Line( TopLeft, BottomLeft, color, duration, depthTest );

		if ( drawCenter )
			DebugOverlay.Sphere( Position, 1f, color, duration, depthTest );

		if ( drawCross )
		{
			DebugOverlay.Line( BottomLeft, TopRight, color, duration, depthTest );
			DebugOverlay.Line( TopLeft, BottomRight, color, duration, depthTest );
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
		DebugOverlay.Line( BottomLeft, BottomRight, duration, depthTest );
		DebugOverlay.Line( BottomRight, TopRight, duration, depthTest );
		DebugOverlay.Line( TopRight, TopLeft, duration, depthTest );
		DebugOverlay.Line( TopLeft, BottomLeft, duration, depthTest );

		if ( drawCenter )
			DebugOverlay.Sphere( Position, 5f, Color.White, duration, depthTest );

		if ( drawCross )
		{
			DebugOverlay.Line( BottomLeft, TopRight, duration, depthTest );
			DebugOverlay.Line( TopLeft, BottomRight, duration, depthTest );
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

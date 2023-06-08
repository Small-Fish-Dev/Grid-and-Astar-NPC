namespace GridAStar;

public struct AStarNode
{
	public Cell Parent { get; private set; }
	public Cell Destination { get; private set; }
	public string MovementTag { get; private set; } = string.Empty;
	public Vector3 StartPosition => Parent.Position;
	public Vector3 EndPosition => Destination.Position;
	public Vector3 Direction => (EndPosition - StartPosition).Normal;

	public AStarNode( Cell parent, Cell destination )
	{
		Parent = parent;
		Destination = destination;
	}

	public AStarNode( Cell parent, Cell destination, string tag ) 
	{
		Parent = parent;
		Destination = destination;
		MovementTag = tag;
	}
}

public struct AStarPath
{
	public AStarPathBuilder Settings { get; internal set; }
	public List<Cell> Cells { get; set; }
	public Grid Grid => Settings.Grid;
	public int Count => Cells.Count();
	public bool IsEmpty => Cells == null || Count == 0;

	public AStarPath() { }

	public AStarPath( AStarPathBuilder builder, List<Cell> cells ) : this()
	{
		Settings = builder;
		Cells = cells;
	}

	public static AStarPath From( AStarPathBuilder builder, List<Cell> cells ) => new AStarPath( builder, cells );

	public static AStarPath Empty() => new AStarPath();

	/// <summary>
	/// Simplify the path by iterating over line of sights between the given segment size, joining them if valid
	/// </summary>
	/// <param name="segmentAmounts"></param>
	/// <param name="iterations"></param>
	/// <returns></returns>
	public void Simplify( int segmentAmounts = 2, int iterations = 8 )
	{
		for ( int iteration = 0; iteration < iterations; iteration++ )
		{
			var segmentStart = 0;
			var segmentEnd = Math.Min( segmentAmounts, Count - 1 );

			while ( Count > 2 && segmentEnd < Count - 1 )
			{
				var currentCell = Cells[segmentStart];
				var furtherCell = Cells[segmentEnd];

				if ( Settings.Grid.LineOfSight( currentCell, furtherCell, Settings.PathCreator ) )
					for ( int toDelete = segmentStart + 1; toDelete < segmentEnd; toDelete++ )
						Cells.RemoveAt( toDelete );

				if ( segmentEnd == Count - 1 )
					break;

				segmentStart++;
				segmentEnd = Math.Min( segmentStart + segmentAmounts, Count - 1 );
			}
		}
	}
}

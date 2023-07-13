namespace GridAStar;

public struct AStarPath
{
	public AStarPathBuilder Settings { get; internal set; }
	public List<AStarNode> Nodes { get; set; }
	public Grid Grid => Settings.Grid;
	public int Count => Nodes.Count();
	public bool IsEmpty => Nodes == null || Count == 0;
	public float Length { get; set; } = 0f;

	public AStarPath() { }

	public AStarPath( AStarPathBuilder builder, List<AStarNode> nodes ) : this()
	{
		Settings = builder;
		Nodes = nodes;
		CalculateLenght();
	}

	public static AStarPath From( AStarPathBuilder builder, List<AStarNode> nodes ) => new AStarPath( builder, nodes );

	public static AStarPath Empty() => new AStarPath();

	public void CalculateLenght()
	{
		var length = 0f;
		for ( int i = 0; i < Nodes.Count - 1; i++ )
			length += Nodes[i].EndPosition.Distance( Nodes[i + 1].EndPosition );
		Length = length;
	}

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
				var currentNode = Nodes[segmentStart];
				var nextNode = Nodes[segmentStart + 1];
				var furtherNode = Nodes[segmentEnd];

				if ( nextNode.MovementTag == "" || nextNode.MovementTag == string.Empty || furtherNode.MovementTag == "" || furtherNode.MovementTag == string.Empty )
					if ( Settings.Grid.LineOfSight( currentNode.Current, furtherNode.Current, Settings.PathCreator ) )
						for ( int toDelete = segmentStart + 1; toDelete < segmentEnd; toDelete++ )
							Nodes.RemoveAt( toDelete );


				if ( segmentEnd == Count - 1 )
					break;

				segmentStart++;
				segmentEnd = Math.Min( segmentStart + segmentAmounts, Count - 1 );
			}
		}

		CalculateLenght();
	}
}

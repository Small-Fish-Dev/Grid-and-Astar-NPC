namespace GridAStar;

public partial class AStarNode : IHeapItem<AStarNode>, IEquatable<AStarNode>
{
	public Cell Current { get; internal set; } = null;
	public AStarNode Parent { get; internal set; } = null;
	public string MovementTag { get; private set; } = string.Empty;
	public Vector3 StartPosition => Parent.Current.Position;
	public Vector3 EndPosition => Current.Position;
	public Vector3 Direction => (EndPosition - StartPosition).Normal;
	public CellTags Tags => Current.Tags;
	public float gCost { get; internal set; } = 0f;
	public float hCost { get; internal set; } = 0f;
	public float fCost => gCost + hCost;
	public int HeapIndex { get; set; }

	public AStarNode( Cell cell )
	{
		Current = cell;
	}
	public AStarNode( Cell cell, AStarNode parent )
	{
		Current = cell;
		Parent = parent;
	}
	public AStarNode( Cell cell, string tag )
	{
		Current = cell;
		MovementTag = tag;
	}
	public AStarNode( Cell cell, AStarNode parent, string tag )
	{
		Current = cell;
		Parent = parent;
		MovementTag = tag;
	}

	public float Distance( Cell other ) => Current.Position.Distance( other.Position );
	public float Distance( AStarNode other ) => Current.Position.Distance( other.Current.Position );

	public int CompareTo( AStarNode other )
	{
		var compare = fCost.CompareTo( other.fCost );
		if ( compare == 0 )
			compare = hCost.CompareTo( other.hCost );
		return -compare;
	}

	public override int GetHashCode() => Current.GetHashCode();

	public static bool operator ==( AStarNode a, AStarNode b ) => a?.Equals( b ) ?? false;
	public static bool operator !=( AStarNode a, AStarNode b ) => !a?.Equals( b ) ?? false;

	public override bool Equals( object obj )
	{
		if ( obj is not AStarNode node ) return false;

		if ( node?.Current != Current ) return false;
		if ( node?.Parent?.Current != Parent?.Current ) return false;
		if ( node?.MovementTag != MovementTag ) return false;

		return true;
	}

	public bool Equals( AStarNode other )
	{
		if ( other is not AStarNode ) return false;

		if ( other?.Current != Current ) return false;
		if ( other?.Parent?.Current != Parent?.Current ) return false;
		if ( other?.MovementTag != MovementTag ) return false;

		return true;
	}
}

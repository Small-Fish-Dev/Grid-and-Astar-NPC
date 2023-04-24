namespace GridAStar;

public partial class Node : IHeapItem<Node>
{
	public Cell Current;
	public Node Parent;
	public float gCost;
	public float hCost;
	public float fCost => gCost + hCost;
	public int HeapIndex { get; set; }

	public Node( Cell cell )
	{
		Current = cell;
	}

	public float Distance( Cell other ) => Current.Position.DistanceSquared( other.Position );
	public float Distance( Node other ) => Current.Position.DistanceSquared( other.Current.Position );

	public int CompareTo( Node other )
	{
		var compare = fCost.CompareTo( other.fCost );
		if ( compare == 0 )
			compare = hCost.CompareTo( other.hCost );
		return -compare;
	}
	public override bool Equals( object obj )
	{
		return Equals( obj as Node );
	}

	public bool Equals( Node obj )
	{
		return obj != null && obj.GetHashCode() == this.GetHashCode();
	}

	public override int GetHashCode()
	{
		return Current.GetHashCode();
	}

}

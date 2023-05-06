namespace GridAStar;

public partial class Node : IHeapItem<Node>, IEquatable<Node>
{
	public Cell Current;
	public Node Parent;
	public float gCost = 0f;
	public float hCost = 0f;
	public float fCost => gCost + hCost;
	public int HeapIndex { get; set; }

	public Node( Cell cell )
	{
		Current = cell;
	}

	public float Distance( Cell other ) => Current.Position.Distance( other.Position );
	public float Distance( Node other ) => Current.Position.Distance( other.Current.Position );

	public int CompareTo( Node other )
	{
		var compare = fCost.CompareTo( other.fCost );
		if ( compare == 0 )
			compare = hCost.CompareTo( other.hCost );
		return -compare;
	}

	public override int GetHashCode() => Current.GetHashCode();

	// Alex Instagib from Facepunch Ltd. code VVV
	public static bool operator ==( Node a, Node b ) => a.Equals( b );
	public static bool operator !=( Node a, Node b ) => !a.Equals( b );

	public override bool Equals( object obj ) => (obj as Node)?.Current == Current;
	public bool Equals( Node other ) => other.Current == Current;

}

namespace GridAStar;

public partial class Cell : IHeapItem<Cell>
{

	public Cell Parent;
	public float gCost;
	public float hCost;
	public float fCost => gCost + hCost;
	public int HeapIndex { get; set; }

	public float Distance( Cell cell )
	{
		return Position.DistanceSquared( cell.Position );
	}

	public int CompareTo( Cell other )
	{
		var compare = fCost.CompareTo( other.fCost );
		if ( compare == 0 )
			compare = hCost.CompareTo( other.hCost );
		return -compare;
	}

}

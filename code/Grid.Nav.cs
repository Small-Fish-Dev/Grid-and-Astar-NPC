namespace GridAStar;

public partial class Grid
{
	public List<Cell> ComputePath( Cell startingCell, Cell targetCell )
	{
		List<Cell> finalPath = new();

		if ( startingCell == null || targetCell == null ) return finalPath; // Escape if invalid end position Ex. if FindNearestDestination is false

		Heap<Cell> openSet = new Heap<Cell>( Cells.Count );
		HashSet<Cell> closedSet = new();
		openSet.Add( startingCell );
		
		while ( openSet.Count > 0 )
		{

			Cell currentNode = openSet.RemoveFirst();
			closedSet.Add( currentNode );

			if ( currentNode == targetCell )
			{
				retracePath( ref finalPath, startingCell, currentNode );
				break;
			}

			foreach ( var neighbour in currentNode.GetNeighbours() )
			{
				if ( neighbour.Occupied || closedSet.Contains( neighbour ) ) continue;

				float newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( neighbour );
				bool isInOpenSet = openSet.Contains( neighbour );

				if ( newMovementCostToNeighbour < neighbour.gCost || !isInOpenSet )
				{
					neighbour.gCost = newMovementCostToNeighbour;
					neighbour.hCost = neighbour.Distance( targetCell );
					neighbour.Parent = currentNode;

					if ( !isInOpenSet )
						openSet.Add( neighbour );
				}
			}
		}
		
		return finalPath;
	}

	void retracePath( ref List<Cell> pathList, Cell startCell, Cell targetCell )
	{
		Cell currentNode = targetCell;

		while ( currentNode != startCell )
		{
			pathList.Add( currentNode );
			currentNode = currentNode.Parent;
		}

		pathList.Reverse();
	}

	public List<Cell> ComputePath( Vector3 startingPosition, Vector3 endingPosition, bool findNearestDestination = false )
	{
		return ComputePath( GetCell( startingPosition ), GetCell( endingPosition, findNearestDestination ) );
	}

	/*[ConCmd.Server( "TestPath" )]
	public static void TestPath()
	{
		foreach ( var player in Entity.All.OfType<Player>() )
		{
			var cells = Grid.Main.ComputePath( player.NearestCell, Grid.Main.GetCell( new IntVector2( -31, 27 ), 1000f ) );

			foreach( var cell in cells )
			{
				cell.Draw( 5 );
			}
		}
	}*/
}



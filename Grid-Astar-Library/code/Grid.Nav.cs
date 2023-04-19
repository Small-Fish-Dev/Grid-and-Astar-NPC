namespace GridAStar;

public partial class Grid
{
	public async Task<List<Cell>> ComputePath( Cell startingCell, Cell targetCell )
	{
		List<Cell> finalPath = new();

		if ( startingCell == null || targetCell == null ) return finalPath; // Escape if invalid end position Ex. if FindNearestDestination is false

		Heap<Cell> openSet = new Heap<Cell>( Cells.Count );
		HashSet<Cell> closedSet = new();
		openSet.Add( startingCell );

		await GameTask.RunInThreadAsync( () =>
		{
			while ( openSet.Count > 0 )
			{

				Cell currentNode = openSet.RemoveFirst();
				closedSet.Add( currentNode );

				if ( currentNode == targetCell )
				{
					retracePath( ref finalPath, startingCell, currentNode );
					break;
				}

				currentNode.Draw( Color.White, 1f );

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
		} );

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

	public async Task<List<Cell>> ComputePath( Vector3 startingPosition, Vector3 endingPosition, bool findNearestDestination = false )
	{
		return await ComputePath( GetCell( startingPosition ), GetCell( endingPosition, findNearestDestination ) );
	}

	[ConCmd.Server( "TestPath" )]
	public async static void TestPath()
	{
		foreach ( var client in Game.Clients )
		{
			var cells = await Grid.Main.ComputePath( Grid.Main.GetCell( new IntVector2( -40, 98 ), 1000f ), Grid.Main.GetCell( client.Pawn.Position + Vector3.Up * 100f, true ) );

			foreach ( var cell in cells )
			{
				cell.Draw( 5 );
			}
		}
	}
}



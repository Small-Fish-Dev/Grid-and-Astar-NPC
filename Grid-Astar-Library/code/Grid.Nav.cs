namespace GridAStar;

public partial class Grid
{
	public async Task<List<Cell>> ComputePath( Cell startingCell, Cell targetCell, bool reversed = false )
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
		
		if ( reversed )
			finalPath.Reverse();

		return finalPath;
	}

	/// <summary>
	/// Compute two paths at the same time, From->To and To->From and return the first one that finishes, can massively speed up
	/// </summary>
	/// <param name="startingCell"></param>
	/// <param name="targetCell"></param>
	/// <returns></returns>
	public async Task<List<Cell>> ComputePathParallel( Cell startingCell, Cell targetCell )
	{
		var pathFromTo = ComputePath( startingCell, targetCell );
		var pathToFrom = ComputePath( targetCell, startingCell );

		List<Cell> result = new();

		await GameTask.RunInThreadAsync( () =>
		{
			var result = GameTask.WhenAny( pathFromTo, pathToFrom );
		} );

		return result;

	}

	void retracePath( ref List<Cell> pathList, Cell startCell, Cell targetCell )
	{
		var currentNode = targetCell;

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



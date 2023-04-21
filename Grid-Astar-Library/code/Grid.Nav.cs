using System.Threading;

namespace GridAStar;

public partial class Grid
{
	public async Task<List<Cell>> ComputePath( Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false )
	{
		List<Cell> finalPath = new();

		if ( startingCell == null || targetCell == null ) return finalPath; // Escape if invalid end position Ex. if FindNearestDestination is false

		var startingNode = new Node( startingCell );
		var targetNode = new Node( targetCell );

		Heap<Node> openSet = new Heap<Node>( Cells.Count );
		HashSet<Node> closedSet = new();
		HashSet<Cell> closedCellSet = new();
		HashSet<Cell> openCellSet = new();
		Dictionary<Cell, Node> cellNodePair = new();
		openSet.Add( startingNode );
		openCellSet.Add( startingCell );
		cellNodePair.Add( startingCell, startingNode );
		cellNodePair.Add( targetCell, targetNode );

		await GameTask.RunInThreadAsync( () =>
		{
			while ( openSet.Count > 0 && !token.IsCancellationRequested )
			{
				var currentNode = openSet.RemoveFirst();
				closedSet.Add( currentNode );
				openCellSet.Remove( currentNode.Current );
				closedCellSet.Add( currentNode.Current );

				if ( currentNode.Current == targetNode.Current )
				{
					retracePath( ref finalPath, startingNode, currentNode );
					break;
				}

				foreach ( var neighbour in currentNode.Current.GetNeighbours() )
				{
					if ( neighbour.Occupied || closedCellSet.Contains( neighbour ) ) continue;

					bool isInOpenSet = openCellSet.Contains( neighbour );
					Node neighbourNode;

					if ( isInOpenSet )
						neighbourNode = cellNodePair[neighbour];
					else
						neighbourNode = new Node( neighbour );

					float newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( neighbour );

					if ( newMovementCostToNeighbour < neighbourNode.gCost || !isInOpenSet )
					{
						neighbourNode.gCost = newMovementCostToNeighbour;
						neighbourNode.hCost = neighbourNode.Distance( targetCell );
						neighbourNode.Parent = currentNode;

						if ( !isInOpenSet )
						{
							openSet.Add( neighbourNode );
							openCellSet.Add( neighbour );
							if ( !cellNodePair.ContainsKey( neighbour ) )
								cellNodePair.Add( neighbour, neighbourNode );
							else
								cellNodePair[neighbour] = neighbourNode;
						}
					}
				}
			}
		} );

		if ( token.IsCancellationRequested )
			return null;
		
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

		var ctoken = new CancellationTokenSource();

		var fromTo = ComputePath( startingCell, targetCell, ctoken.Token );
		var toFrom = ComputePath( targetCell, startingCell, ctoken.Token, true );

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		ctoken.Cancel();

		return pathResult;

	}

	void retracePath( ref List<Cell> pathList, Node startNode, Node targetNode )
	{
		var currentNode = targetNode;

		while ( currentNode != startNode )
		{
			pathList.Add( currentNode.Current );
			currentNode = currentNode.Parent;
		}

		pathList.Reverse();
	}

	[ConCmd.Server( "TestPath" )]
	public async static void TestPath()
	{
		foreach ( var client in Game.Clients )
		{
			// Check distance before?
			var cells = await Grid.Main.ComputePathParallel( Grid.Main.GetCell( new IntVector2( -60, 60 ), 1000f ), Grid.Main.GetCell( client.Pawn.Position + Vector3.Up * 100f, true ) );
			
			foreach ( var cell in cells )
			{
				cell.Draw( Color.Red, 1, false );
			}
		}
	}
}



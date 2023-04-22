using System.Collections.Immutable;
using System.Threading;

namespace GridAStar;

public partial class Grid
{
	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, false, default ) );
	}

	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell, CancellationToken token )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, false, token ) );
	}

	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell, bool reversed = false, CancellationToken token = default )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, reversed, token ) );
	}

	public ImmutableArray<Cell> ComputePath( Cell startingCell, Cell targetCell, bool reversed = false )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return ComputePathInternal( startingCell, targetCell, reversed, default );
	}

	private ImmutableArray<Cell> ComputePathInternal( Cell startingCell, Cell targetCell, bool reversed, CancellationToken token = default )
	{
		var path = ImmutableArray.CreateBuilder<Cell>();

		var startingNode = new Node( startingCell );
		var targetNode = new Node( targetCell );

		Heap<Node> openSet = new( Cells.Count );
		HashSet<Node> closedSet = new();
		HashSet<Cell> closedCellSet = new();
		HashSet<Cell> openCellSet = new();
		Dictionary<Cell, Node> cellNodePair = new();
		openSet.Add( startingNode );
		openCellSet.Add( startingCell );
		cellNodePair.Add( startingCell, startingNode );
		cellNodePair.Add( targetCell, targetNode );

		while ( openSet.Count > 0 && !token.IsCancellationRequested )
		{
			var currentNode = openSet.RemoveFirst();
			closedSet.Add( currentNode );
			openCellSet.Remove( currentNode.Current );
			closedCellSet.Add( currentNode.Current );

			if ( currentNode.Current == targetNode.Current )
			{
				RetracePath( path, startingNode, currentNode );
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

		if ( reversed )
			path.Reverse();

		return path.ToImmutable();
	}

	public async Task<ImmutableArray<Cell>> ComputePathParallel( Cell startingCell, Cell targetCell )
	{
		var ctoken = new CancellationTokenSource();

		var fromTo = ComputePathAsync( startingCell, targetCell, ctoken.Token );
		var toFrom = ComputePathAsync( targetCell, startingCell, true, ctoken.Token );

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		ctoken.Cancel();

		return pathResult;
	}

	public async Task<ImmutableArray<Cell>> ComputePathParallel( Grid grid, Vector3 startingPosition, Vector3 targetPosition, bool findClosest = false )
	{
		var ctoken = new CancellationTokenSource();

		var startingCell = grid.GetCell( startingPosition, findClosest );
		var targetCell = grid.GetCell( targetPosition, findClosest );

		var fromTo = ComputePathAsync( startingCell, targetCell, ctoken.Token );
		var toFrom = ComputePathAsync( targetCell, startingCell, true, ctoken.Token );

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		ctoken.Cancel();

		return pathResult;
	}

	private static void RetracePath( ImmutableArray<Cell>.Builder pathList, Node startNode, Node targetNode )
	{
		var currentNode = targetNode;

		while ( currentNode != startNode )
		{
			pathList.Add( currentNode.Current );
			currentNode = currentNode.Parent;
		}

		pathList.Reverse();
	}
}



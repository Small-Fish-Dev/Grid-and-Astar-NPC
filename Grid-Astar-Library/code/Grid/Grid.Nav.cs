using System.Collections.Immutable;
using System.Threading;

namespace GridAStar;

public partial class Grid
{
	/// <summary>
	/// Computes a path from the starting point to a target point on another thread.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>A task that represents the asynchronous operation. The result of the task is an <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell, Entity pathCreator = null )
	{
		// Fast path.
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, false, CancellationToken.None, pathCreator ) );
	}

	/// /// <summary>
	/// Computes a path from the starting point to a target point on another thread.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="token">A cancellation token used to cancel computing the path.</param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>A task that represents the asynchronous operation. The result of the task is an <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell, CancellationToken token, Entity pathCreator = null )
	{
		// Fast path.
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, false, token, pathCreator ) );
	}

	/// <summary>
	/// Computes a path from the starting point to a target point on another thread.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="reversed">Whether or not to reverse the resulting path.</param>
	/// <param name="token">A cancellation token used to cancel computing the path.</param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>A task that represents the asynchronous operation. The result of the task is an <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public async Task<ImmutableArray<Cell>> ComputePathAsync( Cell startingCell, Cell targetCell, bool reversed = false, CancellationToken token = default, Entity pathCreator = null )
	{
		// Fast path.
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return await GameTask.RunInThreadAsync( () => ComputePathInternal( startingCell, targetCell, reversed, token, pathCreator ) );
	}

	/// <summary>
	/// Computes a path from the starting point to a target point.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="reversed">Whether or not to reverse the resulting path.</param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>An <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public ImmutableArray<Cell> ComputePath( Cell startingCell, Cell targetCell, bool reversed = false, Entity pathCreator = null )
	{
		// Fast path.
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		return ComputePathInternal( startingCell, targetCell, reversed, CancellationToken.None, pathCreator );
	}

	/// <summary>
	/// Computes a path from the starting point to a target point. Reversing the path if needed.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="reversed">Whether or not to reverse the resulting path.</param>
	/// <param name="token">A cancellation token used to cancel computing the path.</param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>An <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	private ImmutableArray<Cell> ComputePathInternal( Cell startingCell, Cell targetCell, bool reversed, CancellationToken token, Entity pathCreator = null )
	{
		// Setup.
		var path = ImmutableArray.CreateBuilder<Cell>();

		var startingNode = new Node( startingCell );
		var targetNode = new Node( targetCell );

		var openSet = new Heap<Node>( Cells.Count );
		var closedSet = new HashSet<Node>();
		var closedCellSet = new HashSet<Cell>();
		var openCellSet = new HashSet<Cell>();
		var cellNodePair = new Dictionary<Cell, Node>();

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
				if ( pathCreator == null && neighbour.Occupied ) continue;
				if ( pathCreator != null && neighbour.Occupied && neighbour.OccupyingEntity != pathCreator ) continue;
				if ( closedCellSet.Contains( neighbour ) ) continue;

				var isInOpenSet = openCellSet.Contains( neighbour );
				Node neighbourNode;

				if ( isInOpenSet )
					neighbourNode = cellNodePair[neighbour];
				else
					neighbourNode = new Node( neighbour );

				var newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( neighbour );

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

	/// <summary>
	/// Computes a path from the starting point to a target point and vice versa simultaneously.
	/// </summary>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="token"></param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns>A task that represents the asynchronous operation. The result of the task is an <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public async Task<ImmutableArray<Cell>> ComputePathParallel( Cell startingCell, Cell targetCell, CancellationTokenSource token, Entity pathCreator = null )
	{
		// Fast path.
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return ImmutableArray<Cell>.Empty;

		var fromTo = ComputePathAsync( startingCell, targetCell, token.Token, pathCreator );
		var toFrom = ComputePathAsync( targetCell, startingCell, true, token.Token, pathCreator );

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		// Cancel the other task that hasn't finished yet.
		token.Cancel();

		return pathResult;
	}

	/// <summary>
	/// Computes a path from a starting <see cref="Vector3"/> to a target <see cref="Vector3"/> and vice versa simultaneously.
	/// </summary>
	/// <param name="startingPosition">A starting world position.</param>
	/// <param name="targetPosition">A target world position.</param>
	/// <param name="token"></param>
	/// <param name="findClosest">Whether or not to find a cell that is closest to the position.</param>
	/// <returns>A task that represents the asynchronous operation. The result of the task is an <see cref="ImmutableArray{Cell}"/> that contains the computed path.</returns>
	public async Task<ImmutableArray<Cell>> ComputePathParallel( Vector3 startingPosition, Vector3 targetPosition, CancellationTokenSource token, bool findClosest = false )
	{
		var startingCell = GetCell( startingPosition, findClosest );
		var targetCell = GetCell( targetPosition, findClosest );

		return await ComputePathParallel( startingCell, targetCell, token );
	}

	/// <summary>
	/// Simplify the path by iterating over line of sights between the given segment size, joining them if valid
	/// </summary>
	/// <param name="path"></param>
	/// <param name="segmentAmounts"></param>
	/// <param name="iterations"></param>
	/// <param name="pathCreator">Who created the path, cells occupied by this entity will get ignored.</param>
	/// <returns></returns>
	public ImmutableArray<Cell> SimplifyPath( ImmutableArray<Cell> path, int segmentAmounts = 2, int iterations = 8, Entity pathCreator = null )
	{
		var pathResult = path.ToList();

		for ( int iteration = 0; iteration < iterations; iteration++ )
		{
			var segmentStart = 0;
			var segmentEnd = Math.Min( segmentAmounts, pathResult.Count() - 1 );
			
			while ( pathResult.Count() > 2 && segmentEnd < pathResult.Count() - 1 )
			{

				var currentCell = pathResult[segmentStart];
				var furtherCell = pathResult[segmentEnd];

				if ( LineOfSight( currentCell, furtherCell, pathCreator ) )
				{
					for ( int toDelete = segmentStart + 1; toDelete < segmentEnd; toDelete++ )
					{
						pathResult.RemoveAt( toDelete );
					}
				}

				if ( segmentEnd == pathResult.Count() - 1 )
					break;

				segmentStart++;
				segmentEnd = Math.Min( segmentStart + segmentAmounts, pathResult.Count() - 1 );
			}
		}

		return pathResult.ToImmutableArray();
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



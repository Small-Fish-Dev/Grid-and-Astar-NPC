using System.Threading;

namespace GridAStar;

public partial class Grid
{
	/// <summary>
	/// Computes a path from the starting point to a target point. Reversing the path if needed.
	/// </summary>
	/// <param name="pathBuilder">The path building settings.</param>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="token">A cancellation token used to cancel computing the path.</param>
	/// <param name="reversed">Whether or not to reverse the resulting path.</param>
	/// <param name="withCellConnections">Allow to take cellConnections into consideration</param>
	/// <returns>An <see cref="List{AStarNode}"/> that contains the computed path.</returns>
	internal List<AStarNode> ComputePathInternal( AStarPathBuilder pathBuilder, Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false, bool withCellConnections = true )
	{
		// Setup.
		var path = new List<AStarNode>();

		var startingNode = new AStarNode( startingCell );
		var targetNode = new AStarNode( targetCell );

		var openSet = new Heap<AStarNode>( AllCells.Count() );
		var closedSet = new HashSet<AStarNode>();
		var openSetReference = new Dictionary<int, AStarNode>(); // .NET Still doesn't let you get an item inside of a HashSet by HashCode
		var initialDistance = startingNode.Distance( targetNode );
		var maxDistance = Math.Max( initialDistance, initialDistance + pathBuilder.MaxCheckDistance ) + CellSize;

		openSet.Add( startingNode );
		openSetReference.Add( startingNode.GetHashCode(), startingNode );

		while ( openSet.Count > 0 && !token.IsCancellationRequested )
		{
			var currentNode = openSet.RemoveFirst();
			closedSet.Add( currentNode );

			if ( currentNode.Current == targetNode.Current )
			{
				RetracePath( ref path, startingNode, currentNode );
				break;
			}

			if ( openSet.Count == 1 && pathBuilder.AcceptsPartial )
			{
				var closestNode = closedSet.OrderBy( x => x.hCost )
					.Where( x => x.gCost != 0f )
					.First();
				RetracePath( ref path, startingNode, closestNode );
				break;
			}

			foreach ( var neighbour in withCellConnections ? currentNode.Current.GetNeighbourAndConnections() : currentNode.Current.GetNeighbours().Select( x => new AStarNode( x ) ) )
			{
				if ( pathBuilder.HasOccupiedTagToExclude && !pathBuilder.HasPathCreator && neighbour.Occupied ) continue;
				if ( pathBuilder.HasOccupiedTagToExclude && pathBuilder.HasPathCreator && neighbour.Occupied && neighbour.OccupyingEntity != pathBuilder.PathCreator ) continue;
				if ( pathBuilder.HasTagsToExlude && neighbour.Tags.Has( pathBuilder.TagsToExclude ) ) continue;
				if ( pathBuilder.HasTagsToInclude && !neighbour.Tags.Has( pathBuilder.TagsToInclude ) ) continue;
				if ( neighbour.MovementTag == "drop" && currentNode.Current.Bottom.z - neighbour.Current.Position.z > pathBuilder.MaxDropHeight ) continue;
				if ( closedSet.Contains( neighbour ) ) continue;

				var isInOpenSet = openSetReference.ContainsKey( neighbour.GetHashCode() );
				var currentNeighbour = isInOpenSet ? openSetReference[neighbour.GetHashCode()] : neighbour;

				var newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( currentNeighbour );
				var distanceToTarget = currentNeighbour.Distance( targetNode );

				if ( distanceToTarget > maxDistance ) continue;

				if ( newMovementCostToNeighbour < currentNeighbour.gCost || !isInOpenSet )
				{
					currentNeighbour.gCost = newMovementCostToNeighbour;
					currentNeighbour.hCost = distanceToTarget;
					currentNeighbour.Parent = currentNode;

					if ( !isInOpenSet )
					{
						openSet.Add( currentNeighbour );
						openSetReference.Add( currentNeighbour.GetHashCode(), currentNeighbour );
					}
				}
			}
		}

		if ( reversed )
			path.Reverse();

		return path;
	}

	private static void RetracePath( ref List<AStarNode> pathList, AStarNode startNode, AStarNode targetNode )
	{
		var currentNode = targetNode;

		while ( currentNode != startNode )
		{
			pathList.Add( currentNode );
			currentNode = currentNode.Parent;
		}

		pathList.Reverse();
		pathList = pathList.Select( node => new AStarNode( node.Parent.Current, node, node.MovementTag ) ).ToList(); // Cell connections are flipped when we reverse
	}
}



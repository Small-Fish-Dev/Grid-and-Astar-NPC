﻿using System.Threading;

namespace GridAStar;

public partial class Grid
{
	/// <summary>
	/// Computes a path from the starting point to a target point. Reversing the path if needed.
	/// </summary>
	/// <param name="pathBuilder">The path building settings.</param>
	/// <param name="startingCell">The starting point of the path.</param>
	/// <param name="targetCell">The desired destination point of the path.</param>
	/// <param name="reversed">Whether or not to reverse the resulting path.</param>
	/// <param name="token">A cancellation token used to cancel computing the path.</param>
	/// <returns>An <see cref="List{Cell}"/> that contains the computed path.</returns>
	internal List<Cell> ComputePathInternal( AStarPathBuilder pathBuilder, Cell startingCell, Cell targetCell, bool reversed, CancellationToken token )
	{
		// Setup.
		var path = new List<Cell>();

		var startingNode = new Node( startingCell );
		var targetNode = new Node( targetCell );

		var openSet = new Heap<Node>( Cells.Count );
		var closedSet = new HashSet<Node>();
		var closedCellSet = new HashSet<Cell>();
		var openCellSet = new HashSet<Cell>();
		var cellNodePair = new Dictionary<Cell, Node>();
		var initialDistance = startingCell.Position.Distance( targetCell.Position );
		var maxDistance = Math.Max( initialDistance, initialDistance + pathBuilder.MaxCheckDistance ) + CellSize; 

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

			if ( openSet.Count == 1 && pathBuilder.AcceptsPartial )
			{
				var closestNode = closedSet.OrderBy( x => x.hCost )
					.Where( x => x.gCost != 0f )
					.First();
				RetracePath( path, startingNode, closestNode );
				break;
			}

			foreach ( var neighbour in currentNode.Current.GetNeighbourAndConnections() )
			{
				if ( pathBuilder.HasOccupiedTagToExclude && !pathBuilder.HasPathCreator && neighbour.Occupied ) continue;
				if ( pathBuilder.HasOccupiedTagToExclude && pathBuilder.HasPathCreator && neighbour.Occupied && neighbour.OccupyingEntity != pathBuilder.PathCreator ) continue;
				if ( pathBuilder.HasTagsToExlude && neighbour.Tags.Has( pathBuilder.TagsToExclude ) ) continue;
				if ( pathBuilder.HasTagsToInclude && !neighbour.Tags.Has( pathBuilder.TagsToInclude ) ) continue;
				if ( closedCellSet.Contains( neighbour ) ) continue;

				var isInOpenSet = openCellSet.Contains( neighbour );
				Node neighbourNode;

				if ( isInOpenSet )
					neighbourNode = cellNodePair[neighbour];
				else
					neighbourNode = new Node( neighbour );

				var newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( neighbour );
				var distanceToTarget = neighbourNode.Distance( targetCell );

				if ( distanceToTarget > maxDistance ) continue;

				if ( newMovementCostToNeighbour < neighbourNode.gCost || !isInOpenSet )
				{
					neighbourNode.gCost = newMovementCostToNeighbour;
					neighbourNode.hCost = distanceToTarget;
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

		return path;
	}

	private static void RetracePath( List<Cell> pathList, Node startNode, Node targetNode )
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



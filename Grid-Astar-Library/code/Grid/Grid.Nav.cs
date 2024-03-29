﻿using System.Diagnostics.Metrics;
using System.Threading;

namespace GridAStar;

public partial class Grid
{
	/*
	static Dictionary<int, (int,int)> threadsUsed = new();

	[GameEvent.Tick.Server]
	static void displayThreads()
	{
		var line = 0;
		foreach ( var thread in threadsUsed )
		{
			DebugOverlay.ScreenText( $"Thread #{thread.Key}: {(thread.Value.Item1 > 0 ? $"IN USE {thread.Value.Item1} nodes With openset count: {thread.Value.Item2}" : "unused")}", line, Time.Delta );
			line++;
		}
	}*/

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

		var maxCells = AllCells.Count();
		var openSet = new Heap<AStarNode>( maxCells );
		var closedSet = new HashSet<AStarNode>();
		var openSetReference = new Dictionary<int, AStarNode>(); // We need this because we create AStarNode down the line for each neighbour and we need a way to reference these
		var initialDistance = startingNode.Distance( targetNode );
		var maxDistance = Math.Max( initialDistance, initialDistance + pathBuilder.MaxCheckDistance ) + CellSize;

		openSet.Add( startingNode );
		openSetReference.Add( startingNode.GetHashCode(), startingNode );
		/*
		var curId = ThreadSafe.CurrentThreadId;
		if ( threadsUsed.ContainsKey( curId ) )
			threadsUsed[curId] = (0,0);
		else
			threadsUsed.Add( curId, (0,0) );

		var count = 0;*/

		while ( openSet.Count > 0 && !token.IsCancellationRequested )
		{
			var currentNode = openSet.RemoveFirst();
			closedSet.Add( currentNode );
			//count++;

			//threadsUsed[curId] = (count,openSet.Count);

			if ( currentNode.Current == targetNode.Current )
			{
				RetracePath( ref path, startingNode, currentNode );
				break;
			}

			foreach ( var neighbour in withCellConnections ? currentNode.Current.GetNeighbourAndConnections() : currentNode.Current.GetNeighbourConnections() )
			{
				if ( pathBuilder.HasOccupiedTagToExclude && !pathBuilder.HasPathCreator && neighbour.Occupied ) continue;
				if ( pathBuilder.HasOccupiedTagToExclude && pathBuilder.HasPathCreator && neighbour.Occupied && neighbour.OccupyingEntity != pathBuilder.PathCreator ) continue;
				if ( pathBuilder.HasTagsToExlude && neighbour.Tags.Has( pathBuilder.TagsToExclude ) ) continue;
				if ( pathBuilder.HasTagsToInclude && !neighbour.Tags.Has( pathBuilder.TagsToInclude ) ) continue;
				if ( neighbour.MovementTag == "drop" && currentNode.Current.Bottom.z - neighbour.Current.Position.z > pathBuilder.MaxDropHeight ) continue;
				if ( closedSet.Contains( neighbour ) ) continue;

				var isInOpenSet = openSetReference.ContainsKey( neighbour.GetHashCode() );
				var currentNeighbour = isInOpenSet ? openSetReference[neighbour.GetHashCode()] : neighbour;

				var malus = 0f;

				if ( pathBuilder.HasTagsToAvoid && currentNeighbour.Tags.Has( pathBuilder.TagsToAvoid.Keys ) )
					foreach ( var tag in currentNeighbour.Tags.All )
						if ( pathBuilder.TagsToAvoid.TryGetValue( tag, out float tagMalus ) )
							malus += tagMalus;

				var newMovementCostToNeighbour = currentNode.gCost + currentNode.Distance( currentNeighbour ) + malus / 2f;
				var distanceToTarget = currentNeighbour.Distance( targetNode ) + malus / 2f;

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

		//threadsUsed[curId] = (0,0);

		if ( token.IsCancellationRequested )
			return path;

		if ( path.Count == 0 && pathBuilder.AcceptsPartial )
		{
			var closestNode = closedSet.OrderBy( x => x.hCost )
				.Where( x => x.gCost != 0f )
				.First();

			RetracePath( ref path, startingNode, closestNode );
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

		var fixedList = new List<AStarNode>();
		
		foreach ( var node in pathList )
		{
			if ( node.Parent?.Current == null )
				continue;

			var newNode = new AStarNode( node.Parent.Current, node, node.MovementTag );
			fixedList.Add( newNode );
		}
		
		pathList = fixedList;
		//pathList = pathList.Select( node => new AStarNode( node.Parent.Current, node, node.MovementTag ) ).ToList(); // Cell connections are flipped when we reversed earlier
	}
}



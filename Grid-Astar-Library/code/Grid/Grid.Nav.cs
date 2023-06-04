using System.Collections.Immutable;
using System.Threading;

namespace GridAStar;

public struct AStarPath
{
	public AStarPathBuilder Settings { get; internal set; }
	public List<Cell> Cells { get; set; }
	public Grid Grid => Settings.Grid;
	public int Count => Cells.Count();
	public bool IsEmpty => Cells == null || Count == 0;

	public AStarPath() { }

	public AStarPath( AStarPathBuilder builder, List<Cell> cells ) : this()
	{
		Settings = builder;
		Cells = cells;
	}

	public static AStarPath From( AStarPathBuilder builder, List<Cell> cells ) => new AStarPath( builder, cells );

	public static AStarPath Empty() => new AStarPath();

	/// <summary>
	/// Simplify the path by iterating over line of sights between the given segment size, joining them if valid
	/// </summary>
	/// <param name="segmentAmounts"></param>
	/// <param name="iterations"></param>
	/// <returns></returns>
	public void Simplify( int segmentAmounts = 2, int iterations = 8 )
	{
		for ( int iteration = 0; iteration < iterations; iteration++ )
		{
			var segmentStart = 0;
			var segmentEnd = Math.Min( segmentAmounts, Count - 1 );

			while ( Count > 2 && segmentEnd < Count - 1 )
			{
				var currentCell = Cells[segmentStart];
				var furtherCell = Cells[segmentEnd];

				if ( Settings.Grid.LineOfSight( currentCell, furtherCell, Settings.PathCreator ) )
					for ( int toDelete = segmentStart + 1; toDelete < segmentEnd; toDelete++ )
						Cells.RemoveAt( toDelete );

				if ( segmentEnd == Count - 1 )
					break;

				segmentStart++;
				segmentEnd = Math.Min( segmentStart + segmentAmounts, Count - 1 );
			}
		}
	}
}

public struct AStarPathBuilder
{
	public Grid Grid { get; private set; } = null;
	public List<string> TagsToExclude { get; private set; } = new() { "occupied" };
	public bool HasTagsToExlude => TagsToExclude.Count() > 0;
	public bool HasOccupiedTagToExclude => HasTagsToExlude ? TagsToExclude.Contains( "occupied" ) : false;
	public List<string> TagsToInclude { get; private set; } = new();
	public bool HasTagsToInclude => TagsToInclude.Count() > 0;
	public bool AcceptsPartial { get; private set; } = false;
	public float MaxCheckDistance { get; private set; } = float.PositiveInfinity;
	public Entity PathCreator { get; private set; } = null;
	public bool HasPathCreator => PathCreator != null;

	public AStarPathBuilder() { }
	public AStarPathBuilder( Grid grid ) : this()
	{
		Grid = grid;
	}

	public static AStarPathBuilder From( Grid grid ) => new AStarPathBuilder( grid );

	/// <summary>
	/// Which tags the cells need to have to be a part of the path
	/// </summary>
	/// <param name="tags"></param>
	public AStarPathBuilder WithTags( params string[] tags )
	{
		foreach ( var tag in tags )
		{
			if ( !TagsToInclude.Contains( tag ) )
				TagsToInclude.Add( tag );
			if ( TagsToExclude.Contains( tag ) )
				TagsToExclude.Remove( tag );
		}
		return this;
	}
	/// <summary>
	/// Which tags the cells cannot have to be a part of the path ("occupied" for example goes here)
	/// </summary>
	/// <param name="tags"></param>
	public AStarPathBuilder WithoutTags( params string[] tags )
	{
		foreach ( var tag in tags )
		{
			if ( !TagsToExclude.Contains( tag ) )
				TagsToExclude.Add( tag );
			if ( TagsToInclude.Contains( tag ) )
				TagsToInclude.Remove( tag );
		}
		return this;
	}
	/// <summary>
	/// How far from the destination are we willing to check, this is added on top of the distance between start and end, else it would never run
	/// </summary>
	/// <param name="maxDistance">Default is infinity</param>
	public AStarPathBuilder WithMaxDistance( float maxDistance )
	{
		MaxCheckDistance = Math.Max( 0f, maxDistance );
		return this;
	}
	/// <summary>
	/// Accept paths that don't reach the destination, pairs well with WithMaxDistance
	/// </summary>
	public AStarPathBuilder WithPartialEnabled()
	{
		AcceptsPartial = true;
		return this;
	}

	/// <summary>
	/// If the cell is being occupied by the path creator then it counts it anyways
	/// </summary>
	/// <param name="pathCreator"></param>
	/// <returns></returns>
	public AStarPathBuilder WithPathCreator( Entity pathCreator )
	{
		PathCreator = pathCreator;
		return this;
	}

	public AStarPath Run( Cell startingCell, Cell targetCell, bool reversed = false )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		return AStarPath.From( this, Grid.ComputePathInternal( this, startingCell, targetCell, reversed, CancellationToken.None ) );
	}
	public AStarPath Run( Vector3 startingPosition, Cell targetCell, bool reversed = false ) => Run( Grid.GetCell( startingPosition ), targetCell, reversed );
	public AStarPath Run( Cell startingCell, Vector3 targetPosition, bool reversed = false ) => Run( startingCell, Grid.GetCell( targetPosition ), reversed );
	public AStarPath Run( Vector3 startingPosition, Vector3 targetPosition, bool reversed = false ) => Run( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), reversed );
	
	
	internal AStarPath Run( Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		return AStarPath.From( this, Grid.ComputePathInternal( this, startingCell, targetCell, reversed, token ) );
	}

	public async Task<AStarPath> RunAsync( Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false )
	{
		var builder = this;

		return await GameTask.RunInThreadAsync( () => builder.Run( startingCell, targetCell, token, reversed ) );
	}
	public async Task<AStarPath> RunAsync( Vector3 startingPosition, Cell targetCell, CancellationToken token, bool reversed = false ) => await RunAsync( Grid.GetCell( startingPosition ), targetCell, token, reversed );
	public async Task<AStarPath> RunAsync( Cell startingCell, Vector3 targetPosition, CancellationToken token, bool reversed = false ) => await RunAsync( startingCell, Grid.GetCell( targetPosition ), token, reversed );
	public async Task<AStarPath> RunAsync( Vector3 startingPosition, Vector3 targetPosition, CancellationToken token, bool reversed = false ) => await RunAsync( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), token, reversed );

	public async Task<AStarPath> RunInParallel( Cell startingCell, Cell targetCell, CancellationTokenSource tokenSource )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		var fromTo = RunAsync( startingCell, targetCell, tokenSource.Token );
		var toFrom = RunAsync( targetCell, startingCell, tokenSource.Token, true );

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		// Cancel the other task that hasn't finished yet.
		tokenSource.Cancel();

		return pathResult;
	}
	public async Task<AStarPath> RunInParallel( Vector3 startingPosition, Cell targetCell, CancellationTokenSource tokenSource ) => await RunInParallel( Grid.GetCell( startingPosition ), targetCell, tokenSource );
	public async Task<AStarPath> RunInParallel( Cell startingCell, Vector3 targetPosition, CancellationTokenSource tokenSource ) => await RunInParallel( startingCell, Grid.GetCell( targetPosition ), tokenSource );
	public async Task<AStarPath> RunInParallel( Vector3 startingPosition, Vector3 targetPosition, CancellationTokenSource tokenSource ) => await RunInParallel( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), tokenSource );
}

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
		var maxDistance = Math.Max( float.PositiveInfinity, initialDistance + pathBuilder.MaxCheckDistance ); 

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
				RetracePath( path, startingNode, closedSet.OrderBy( x => x.hCost ).First() );
				break;
			}

			foreach ( var neighbour in currentNode.Current.GetNeighbours() )
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



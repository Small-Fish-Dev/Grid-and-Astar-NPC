using System.Threading;

namespace GridAStar;

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
	public float MaxDropHeight { get; private set; } = GridSettings.DEFAULT_DROP_HEIGHT;
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
	/// How high up you can drop
	/// </summary>
	/// <param name="maxDropHeight">Default is infinity</param>
	public AStarPathBuilder WithMaxDropHeight( float maxDropHeight )
	{
		MaxDropHeight = Math.Min( Grid.MaxDropHeight, maxDropHeight );
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

	public AStarPath Run( Cell startingCell, Cell targetCell, bool reversed = false, bool withCellConnections = true )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		return AStarPath.From( this, Grid.ComputePathInternal( this, startingCell, targetCell, CancellationToken.None, reversed, withCellConnections ) );
	}
	public AStarPath Run( Vector3 startingPosition, Cell targetCell, bool reversed = false, bool withCellConnections = true ) => Run( Grid.GetCell( startingPosition ), targetCell, reversed, withCellConnections );
	public AStarPath Run( Cell startingCell, Vector3 targetPosition, bool reversed = false, bool withCellConnections = true ) => Run( startingCell, Grid.GetCell( targetPosition ), reversed, withCellConnections );
	public AStarPath Run( Vector3 startingPosition, Vector3 targetPosition, bool reversed = false, bool withCellConnections = true ) => Run( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), reversed, withCellConnections );


	internal AStarPath Run( Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false, bool withCellConnections = true )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		return AStarPath.From( this, Grid.ComputePathInternal( this, startingCell, targetCell, token, reversed, withCellConnections ) );
	}

	public async Task<AStarPath> RunAsync( Cell startingCell, Cell targetCell, CancellationToken token, bool reversed = false, bool withCellConnections = true )
	{
		var builder = this;

		return await GameTask.RunInThreadAsync( () => builder.Run( startingCell, targetCell, token, reversed, withCellConnections ) );
	}
	public async Task<AStarPath> RunAsync( Vector3 startingPosition, Cell targetCell, CancellationToken token, bool reversed = false, bool withCellConnections = true ) => await RunAsync( Grid.GetCell( startingPosition ), targetCell, token, reversed, withCellConnections );
	public async Task<AStarPath> RunAsync( Cell startingCell, Vector3 targetPosition, CancellationToken token, bool reversed = false, bool withCellConnections = true ) => await RunAsync( startingCell, Grid.GetCell( targetPosition ), token, reversed, withCellConnections );
	public async Task<AStarPath> RunAsync( Vector3 startingPosition, Vector3 targetPosition, CancellationToken token, bool reversed = false, bool withCellConnections = true ) => await RunAsync( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), token, reversed, withCellConnections );

	public async Task<AStarPath> RunInParallel( Cell startingCell, Cell targetCell, CancellationTokenSource tokenSource, bool withCellConnections = true )
	{
		if ( startingCell is null || targetCell is null || startingCell == targetCell ) return AStarPath.Empty();

		var fromTo = RunAsync( startingCell, targetCell, tokenSource.Token, false, withCellConnections );
		var toFrom = RunAsync( targetCell, startingCell, tokenSource.Token, true, false ); // You can't reverse some cell connections, like dropping down

		var pathResult = await GameTask.WhenAny( fromTo, toFrom ).Result;

		// Cancel the other task that hasn't finished yet.
		tokenSource.Cancel();

		return pathResult;
	}
	public async Task<AStarPath> RunInParallel( Vector3 startingPosition, Cell targetCell, CancellationTokenSource tokenSource, bool withCellConnections = true ) => await RunInParallel( Grid.GetCell( startingPosition ), targetCell, tokenSource, withCellConnections );
	public async Task<AStarPath> RunInParallel( Cell startingCell, Vector3 targetPosition, CancellationTokenSource tokenSource, bool withCellConnections = true ) => await RunInParallel( startingCell, Grid.GetCell( targetPosition ), tokenSource, withCellConnections );
	public async Task<AStarPath> RunInParallel( Vector3 startingPosition, Vector3 targetPosition, CancellationTokenSource tokenSource, bool withCellConnections = true ) => await RunInParallel( Grid.GetCell( startingPosition ), Grid.GetCell( targetPosition ), tokenSource, withCellConnections );
}

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

using GridAStar;
using System.Collections.Immutable;
using System.Threading;

namespace GridAStarNPC;

public abstract partial class BaseActor
{
	internal ImmutableArray<GridAStar.Cell> currentPath { get; set; } = ImmutableArray<Cell>.Empty;
	public int CurrentPathLength => currentPath.Length;
	internal int currentPathIndex { get; set; } = -1; // -1 = Not set / Hasn't started
	internal GridAStar.Cell currentPathCell => IsFollowingPath ? currentPath[currentPathIndex] : null;
	internal GridAStar.Cell lastPathCell => currentPath.Length > 0 ? currentPath[^1] : null;
	internal GridAStar.Cell targetPathCell { get; set; } = null;
	internal GridAStar.Cell nextPathCell => IsFollowingPath ? currentPath[Math.Min( currentPathIndex + 1, currentPath.Length - 1 )] : null;
	public bool IsFollowingPath => currentPathIndex >= 0 && currentPath.Length > 0;
	[Net] public BaseActor Following { get; set; } = null;
	public bool IsFollowingSomeone => Following != null;
	public bool HasArrivedDestination { get; internal set; } = false;
	public virtual float PathRetraceFrequency { get; set; } = 0.1f; // How many seconds before it checks if the path is being followed or the target position changed
	internal TimeUntil lastRetraceCheck { get; set; } = 0f;

	/// <summary>
	/// Start navigating from its current position to the target cell. Returns false if the path isn't valid
	/// </summary>
	/// <param name="targetCell"></param>
	/// <returns></returns>
	public virtual async Task<bool> NavigateTo( GridAStar.Cell targetCell )
	{
		if ( targetCell == null ) return false;
		if ( targetCell == NearestCell ) return false;

		var computedPath = await CurrentGrid.ComputePathParallel( NearestCell, targetCell, new CancellationTokenSource() );

		if ( computedPath == null || computedPath.Length < 1 ) return false;

		currentPath = computedPath;
		currentPathIndex = 0;
		HasArrivedDestination = false;
		targetPathCell = lastPathCell;

		return true;
	}

	public async virtual void ComputeNavigation()
	{
		if ( lastRetraceCheck )
		{
			if ( IsFollowingSomeone )
			{
				var closestDirection = (Position - Following.Position).Normal;
				targetPathCell = Following.GetCellInDirection( closestDirection, 1 );
			}

			if ( targetPathCell != lastPathCell ) // If the target cell is not the current navpath's last cell, retrace path
				await NavigateTo( targetPathCell );

			if ( IsFollowingPath && Position.DistanceSquared( currentPathCell.Position ) > (CurrentGrid.CellSize * 1.42f) * (CurrentGrid.CellSize * 1.42f) ) // Or if you strayed away from the path too far
				await NavigateTo( targetPathCell );

			lastRetraceCheck = PathRetraceFrequency;
		}

		if ( !IsFollowingPath )
		{
			Direction = Vector3.Zero;
			return;
		}

		for ( int i = 0; i < currentPath.Length; i++ )
		{
			//currentPath[i].Draw( Color.White, Time.Delta );
			//DebugOverlay.Text( i.ToString(), currentPath[i].Position, duration: Time.Delta );
		}

		IsRunning = CurrentPathLength > 20;

		Direction = (nextPathCell.Position - Position).Normal;

		if ( Position.DistanceSquared( nextPathCell.Position ) <= (CurrentGrid.CellSize / 2 + CurrentGrid.StepSize) * (CurrentGrid.CellSize / 2 + CurrentGrid.StepSize) )
			currentPathIndex++;

		if ( currentPathIndex >= currentPath.Length || currentPathCell == targetPathCell )
		{
			HasArrivedDestination = true;
			currentPathIndex = -1;
		}

	}
}

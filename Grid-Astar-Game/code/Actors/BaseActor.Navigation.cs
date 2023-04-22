namespace GridAStarNPC;

public abstract partial class BaseActor
{
	internal List<GridAStar.Cell> currentPath { get; set; } = new List<GridAStar.Cell>();
	public int CurrentPathLength => currentPath.Count;
	internal int currentPathIndex { get; set; } = -1; // -1 = Not set / Hasn't started
	internal GridAStar.Cell currentPathCell => IsFollowingPath ? currentPath[currentPathIndex] : null;
	internal GridAStar.Cell lastPathCell => currentPath.Count > 0 ? currentPath.Last() : null;
	internal GridAStar.Cell targetPathCell { get; set; } = null;
	internal GridAStar.Cell nextPathCell => IsFollowingPath ? currentPath[Math.Min( currentPathIndex + 1, currentPath.Count - 1 )] : null;
	public bool IsFollowingPath => currentPathIndex >= 0 && currentPath.Count > 0;
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
	public virtual async Task<bool> NavigateTo( GridAStar.Cell targetCell)
	{
		if ( targetCell == null ) return false;
		if ( targetCell == NearestCell ) return false;

		targetPathCell = targetCell;

		var computedPath = await CurrentGrid.ComputePath( NearestCell, targetPathCell );

		if ( computedPath == null || computedPath.Count < 1 ) return false;

		currentPath = computedPath;
		currentPathIndex = 0;
		HasArrivedDestination = false;

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

			if ( IsFollowingPath && Position.Distance( currentPathCell.Position ) >= CurrentGrid.CellSize ) // Or if you strayed away from the path too far
				await NavigateTo( targetPathCell );

			lastRetraceCheck = PathRetraceFrequency;
		}

		if ( !IsFollowingPath )
		{
			Direction = Vector3.Zero;
			return;
		}

		Direction = (nextPathCell.Position - Position).Normal;

		if ( IsFollowingSomeone )
		{
			IsRunning = Following.IsRunning;
		}

		if ( NearestCell == nextPathCell || Position.Distance( nextPathCell.Position ) <= CurrentGrid.CellSize / 4f )
			currentPathIndex++;

		if ( currentPathIndex >= currentPath.Count || currentPathCell == targetPathCell )
		{
			HasArrivedDestination = true;
			currentPathIndex = -1;
		}

	}
}

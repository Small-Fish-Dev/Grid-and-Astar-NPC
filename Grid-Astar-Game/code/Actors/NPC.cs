namespace GridAStarNPC;

public partial class NPC : BaseActor
{
	public NPC()
	{
		CurrentGrid ??= GridAStar.Grid.Main;
	}

	public NPC( GridAStar.Grid initialGrid ) : base()
	{
		CurrentGrid = initialGrid;
	}

	public Vector2 TimeBetweenIdleMove => new Vector2( 1f, 3f );
	internal TimeUntil nextIdleMode { get; set; } = 0f;

	public override void Think()
	{
		base.Think();
		
		if ( !IsFollowingSomeone )
		{
			if ( nextIdleMode )
			{
				var randomSpot = GetCellInDirection( Vector3.Random.WithZ(0).Normal, Game.Random.Int( 1, 3 ) );
				NavigateTo( randomSpot );
				nextIdleMode = Game.Random.Float( TimeBetweenIdleMove.x, TimeBetweenIdleMove.y );
			}
		}
	}
}

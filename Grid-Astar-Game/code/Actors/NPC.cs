using GridAStar;
using System;

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

	public Vector2 TimeBetweenIdleMove => new Vector2( 7f, 12f );
	internal TimeUntil nextIdleMode { get; set; } = 0f;

	public override void Spawn()
	{
		base.Spawn();
		AccelerationSpeed = 150f;
		WalkSpeed = 50f;
		RunSpeed = 200f;
	}

	public override void Think()
	{
		base.Think();
		
		if ( !IsFollowingSomeone )
		{
			if ( nextIdleMode && !IsFollowingPath )
			{
				var random = Vector3.Random;
				var targetPosition = random * 3000;
				var targetCell = CurrentGrid.GetCell( targetPosition, false );

				NavigateTo( targetCell );
				nextIdleMode = Game.Random.Float( TimeBetweenIdleMove.x, TimeBetweenIdleMove.y );
			}
		}
	}

	[ConCmd.Server( "SpawnNPC" )]
	public static void SpawnNPC()
	{
		var caller = ConsoleSystem.Caller.Pawn;

		var npc = new NPC();
		npc.Position = caller.Position;
	}
}

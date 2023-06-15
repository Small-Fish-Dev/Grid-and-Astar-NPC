using GridAStar;

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

	public Vector2 TimeBetweenIdleMove => new Vector2( 2f, 4f );
	internal TimeUntil nextIdleMode { get; set; } = 0f;

	public override void Spawn()
	{
		base.Spawn();
		AccelerationSpeed = 150f;
		WalkSpeed = 200f;
		RunSpeed = 350f;
	}

	public override void Think()
	{
		base.Think();

		if ( CurrentGrid == null )
		{
			foreach ( var grid in Grid.Grids )
			{
				if ( grid.Value.IsInsideBounds( Position ) )
				{
					CurrentGrid = grid.Value;
					break;
				}
			}
		}

		if ( Target == null )
		{
				//var random = Vector3.Random;
				//var targetPosition = random * 3000;
				//var targetCell = CurrentGrid.GetCell( targetPosition, false );

				//NavigateTo( CurrentGrid.GetCell( Entity.All.OfType<Player>().First().Position ) );
				//nextIdleMode = Game.Random.Float( TimeBetweenIdleMove.x, TimeBetweenIdleMove.y );

			Target = Entity.All.OfType<Player>().First();
		}
	}

	[ConCmd.Server( "SpawnNPC" )]
	public static void SpawnNPC()
	{
		var caller = ConsoleSystem.Caller.Pawn;

		var npc = new NPC();
		npc.Position = caller.Position;
	}

	[GameEvent.Tick.Server]
	public static void CreateFunny()
	{
		if ( Time.Tick % 40 == 0 )
		{
			var startPos = new Vector3( 802.80f, -448.82f, 890f - 64f );
			var targetPos = new Vector3( 622.21f, -308.05f, 128f - 64f );
			if ( !Grid.Grids.ContainsKey( "tower" ) ) return;
			var currentGrid = Grid.Grids["tower"];
			var startCell = currentGrid.GetCell( startPos );
			var targetCell = currentGrid.GetCell( targetPos );

			var npc = new NPC( currentGrid );
			npc.Position = startPos;
			npc.NavigateTo( targetCell );

			var allNpcs = Entity.All.OfType<NPC>();

			foreach ( var curNpc in allNpcs )
			{
				if ( curNpc.Position.DistanceSquared( targetPos ) <= 5000f )
				{
					curNpc.Delete();
				}
			}
		}
	}
}

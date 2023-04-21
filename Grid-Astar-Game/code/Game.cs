global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;

namespace GridAStarNPC;

public partial class GridGame : GameManager
{
	public GridGame()
	{
		if ( Game.IsServer )
		{
			if ( GridAStar.Grid.Exists() )
				GridAStar.Grid.Load();
		}
		else
		{
			if ( GridAStar.Grid.Exists() )
				GridAStar.Grid.Load();
			else
				GridAStar.Grid.Create( Game.PhysicsWorld.Body.GetBounds() );
		}

	}

	public override void ClientJoined( IClient client )
	{
		base.ClientJoined( client );

		var pawn = new Player();
		client.Pawn = pawn;

		var spawnpoints = Entity.All.OfType<SpawnPoint>();

		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		if ( randomSpawnPoint != null )
		{
			var tx = randomSpawnPoint.Transform;
			tx.Position = tx.Position + Vector3.Up * 50.0f;
			pawn.Transform = tx;
		}
	}

	public override void PostLevelLoaded()
	{
		base.PostLevelLoaded();

		if ( Game.IsServer )
			if ( !GridAStar.Grid.Exists() )
				GridAStar.Grid.Create( Game.PhysicsWorld.Body.GetBounds() );
	}
}

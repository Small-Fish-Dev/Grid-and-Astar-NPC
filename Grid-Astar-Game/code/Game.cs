global using Sandbox;
global using System;
global using Sandbox.UI;
global using System.Runtime.CompilerServices;
global using System.Collections;
global using System.Linq;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Threading.Tasks;

namespace GridAStarNPC;

public partial class GridGame : GameManager
{
	public GridGame()
	{
		/*
		if ( Game.IsServer ) // Try loading the Grid on the server, if it's not found it will create it in the PostLevelLoaded method
			GridAStar.Grid.Load();
		else
		{
			if ( GridAStar.Grid.Load().Result == null ) // Try loading the Grid on the client, else it creates a new one
				GridAStar.Grid.Create( Vector3.Zero, Game.PhysicsWorld.Body.GetBounds(), new Rotation() );
		}*/

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

		/*if ( Game.IsServer )
			if ( !GridAStar.Grid.Exists() )
				GridAStar.Grid.Create( Vector3.Zero, Game.PhysicsWorld.Body.GetBounds(), new Rotation() ); // If no main grid was created before, create one now
		*/
	}
}

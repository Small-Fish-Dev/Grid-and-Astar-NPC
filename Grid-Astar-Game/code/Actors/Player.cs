using GridAStar;
using System.Threading;

namespace GridAStarNPC;

partial class Player : BaseActor
{
	public Vector3 EyePosition => Position + Vector3.Up * 64f;
	[ClientInput] public Vector3 InputDirection { get; set; }
	[ClientInput] public Angles InputAngles { get; set; }
	public Rotation InputRotation => InputAngles.ToRotation();

	public override void Respawn()
	{
		base.Respawn();

		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
	}

	public override void Think() { }

	public override void BuildInput()
	{
		InputDirection = Input.AnalogMove;
		InputAngles += Input.AnalogLook;
		InputAngles = InputAngles.WithPitch( Math.Clamp( InputAngles.pitch, -89f, 89f ) );
	}

	public void SimulateController()
	{
		Direction = InputDirection.WithZ( 0 ).Normal * Rotation.FromYaw( InputAngles.yaw );
		IsRunning = Input.Down( "run" );

		ComputeMotion();
	}


	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );

		SimulateController();
		ComputeAnimations();
		ComputeNavigation();
	}

	/// <summary>
	/// Called every frame on the client
	/// </summary>
	public override void FrameSimulate( IClient cl )
	{
		base.FrameSimulate( cl );

		Rotation = Rotation.FromYaw( InputRotation.Yaw() );

		Camera.Position = EyePosition;
		Camera.Rotation = InputRotation;

		Camera.FirstPersonViewer = this;
	}

	[ConCmd.Server( "TestLOD" )]
	public async static void TestLOD()
	{
		var caller = ConsoleSystem.Caller.Pawn as Player;
		var standingCell = Grid.Main.GetCell( caller.Position );
		var trace = Sandbox.Trace.Ray( caller.EyePosition, caller.EyePosition + caller.InputRotation.Forward * 10000f )
			.Ignore( caller )
			.Run();
		var targetCell = Grid.Main.GetCell( trace.HitPosition );


		var builder = AStarPathBuilder.From( Grid.Main )
			.WithMaxDropHeight( 100f );

		var computedPath = await builder.RunAsync( standingCell, targetCell, CancellationToken.None );

		for ( int i = 0; i < computedPath.Nodes.Count(); i++ )
		{
			var node = computedPath.Nodes[i];
			node.Current.Draw( Color.White, 1f, false );
			DebugOverlay.Text( i.ToString(), node.EndPosition, duration: 1 );

			if ( i < computedPath.Nodes.Count() - 1 )
				DebugOverlay.Line( node.EndPosition, computedPath.Nodes[i + 1].EndPosition, 1f );
		}

		Log.Error( Grid.Main.LineOfSight( standingCell, targetCell ) );
	}
}

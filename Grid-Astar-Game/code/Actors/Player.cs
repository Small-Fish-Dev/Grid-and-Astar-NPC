﻿namespace GridAStarNPC;

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
		IsRunning = Input.Down( InputButton.Run );

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
}
namespace GridAStar;

partial class Player : AnimatedEntity
{
	public BBox CollisionBox => new BBox( new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 72f ) );
	public Vector3 EyePosition => Position + Vector3.Up * 64f;
	[ClientInput] public Vector3 InputDirection { get; set; }
	[ClientInput] public Angles InputAngles { get; set; }
	public Rotation InputRotation => InputAngles.ToRotation();

	public override void Spawn()
	{
		base.Spawn();

		SetModel( "models/citizen/citizen.vmdl" );
		SetupPhysicsFromAABB( PhysicsMotionType.Keyframed, CollisionBox.Mins, CollisionBox.Maxs );

		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;
	}

	public override void BuildInput()
	{
		InputDirection = Input.AnalogMove;
		InputAngles += Input.AnalogLook;
		InputAngles = InputAngles.WithPitch( Math.Clamp( InputAngles.pitch, -89f, 89f ) );
	}

	public TimeSince TimeSinceLostFooting = 0f;

	public override void Simulate( IClient cl )
	{
		base.Simulate( cl );

		Rotation = Rotation.FromYaw( InputRotation.Yaw() );

		var stepSize = 12f;

		var WishVelocity = InputDirection.Normal * Rotation * 200f;
		WishVelocity *= Input.Down( InputButton.Run ) ? 2f : 1f;

		Velocity = Vector3.Lerp( Velocity, WishVelocity, 10f * Time.Delta )
			.WithZ( Velocity.z );

		if ( Input.Down( InputButton.Jump ) && GroundEntity != null )
		{
			Velocity += Vector3.Up * 300f;
			GroundEntity = null;
		}

		if ( TimeSinceLostFooting > Time.Delta * 2f )
			Velocity -= Vector3.Down * (TimeSinceLostFooting + 1f) * Game.PhysicsWorld.Gravity * Time.Delta * 5f;

		var helper = new MoveHelper( Position, Velocity );
		helper.MaxStandableAngle = 31f;

		helper.Trace = helper.Trace
			.Size( CollisionBox.Mins, CollisionBox.Maxs )
			.Ignore( this );

		helper.TryMoveWithStep( Time.Delta, stepSize );

		Position = helper.Position;
		Velocity = helper.Velocity; 
		
		var traceDown = helper.TraceDirection( Vector3.Down ); 

		if ( traceDown.Entity != null )
		{
			GroundEntity = traceDown.Entity;
			Position = traceDown.EndPosition;

			if ( Vector3.GetAngle( Vector3.Up, traceDown.Normal ) <= helper.MaxStandableAngle )
				TimeSinceLostFooting = 0f;
		}
		else
		{
			GroundEntity = null;
			TimeSinceLostFooting = 0f;
			Velocity -= Vector3.Down * Game.PhysicsWorld.Gravity * Time.Delta;
		}
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

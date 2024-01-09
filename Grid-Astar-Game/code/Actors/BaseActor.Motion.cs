namespace GridAStarNPC;

public abstract partial class BaseActor
{/*
	[Net] public float WalkSpeed { get; set; } = 200f;
	[Net] public float RunSpeed { get; set; } = 350f;
	[Net] public bool IsRunning { get; set; } = false;
	[Net] public float AccelerationSpeed { get; set; } = 600f; // Units per second (Ex. 200f means that after 1 second you've reached 200f speed)
	[Net] public float WishSpeed { get; private set; } = 0f;
	[Net] public Vector3 Direction { get; set; } = Vector3.Zero;

	public Vector3 WishVelocity => Direction.Normal * WishSpeed;
	public Rotation WishRotation => Rotation.LookAt( Direction, Vector3.Up );
	public float StepSize => 16f;
	public float MaxWalkableAngle => 46f;


	public TimeSince TimeSinceLostFooting = 0f;

	// If you want to move your actor just set the Direction, anything that isn't 0 will start moving
	public virtual void ComputeMotion()
	{
		if ( GroundEntity != null )
		{
			if ( Direction != Vector3.Zero )
				WishSpeed = Math.Clamp( WishSpeed + AccelerationSpeed * Time.Delta, 0f, IsRunning ? RunSpeed : WalkSpeed );
			else
				WishSpeed = 0f;

			Velocity = Vector3.Lerp( Velocity, WishVelocity, 15f * Time.Delta ) // Smooth horizontal movement
				.WithZ( Velocity.z ); // Don't smooth vertical movement
		}

		if ( TimeSinceLostFooting > Time.Delta * 2f )
			Velocity -= Vector3.Down * (TimeSinceLostFooting + 1f) * Game.PhysicsWorld.Gravity * Time.Delta * 5f;

		var helper = new MoveHelper( Position, Velocity );
		helper.MaxStandableAngle = MaxWalkableAngle;

		helper.Trace = helper.Trace
			.Size( CollisionBox.Mins, CollisionBox.Maxs )
			.WithoutTags( "Actor" )
			.Ignore( this );

		helper.TryMoveWithStep( Time.Delta, StepSize );
		helper.TryUnstuck();

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

		if ( Client.IsValid() )
		{
			if ( Input.Down( "jump" ) )
			{
				if ( Game.IsClient ) return;
				if ( GroundEntity != null )
				{
					GroundEntity = null;
					Velocity = Velocity.WithZ( 300f );
				}
			}
		}
		DebugOverlay.Line( Position, Position + Direction * 30f );
		//DebugOverlay.Sphere( Position, 10f, Color.Blue );
	}*/
}


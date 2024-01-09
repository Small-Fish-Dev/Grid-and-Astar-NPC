namespace GridAStarNPC;

public abstract partial class BaseActor
{/*
	public virtual void ComputeAnimations()
	{
		if ( Velocity.Length > 10 )
			Rotation = Rotation.Lerp( Rotation, Rotation.LookAt( Velocity.WithZ( 0f ), Vector3.Up ), Time.Delta * 6f );

		var animationHelper = new CitizenAnimationHelper( this );
		animationHelper.WithVelocity( Velocity );

		animationHelper.IsGrounded = GroundEntity != null;
	}*/
}

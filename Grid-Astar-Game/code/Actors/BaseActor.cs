namespace GridAStarNPC;

public abstract partial class BaseActor : AnimatedEntity
{
	public virtual float CollisionWidth { get; set; } = 24f;
	public virtual float CollisionHeight { get; set; } = 72f;
	public BBox CollisionBox => new( new Vector3( -CollisionWidth / 2f, -CollisionWidth / 2f, 0f ), new Vector3( CollisionWidth / 2f, CollisionWidth / 2f, CollisionHeight ) );
	public Capsule CollisionCapsule => new Capsule( new Vector3( 0f, 0f, CollisionBox.Mins.z + CollisionWidth / 2 ), new Vector3( 0f, 0f, CollisionBox.Maxs.z - CollisionWidth / 2 ), CollisionWidth / 2f);

	public GridAStar.Grid CurrentGrid = GridAStar.Grid.Main;
	public GridAStar.Cell NearestCell => CurrentGrid?.GetCell( Position );

	public BaseActor()
	{
		CurrentGrid ??= GridAStar.Grid.Main;
	}

	public BaseActor( GridAStar.Grid initialGrid ) : base()
	{
		CurrentGrid = initialGrid;
	}

	public override void Spawn()
	{
		base.Spawn();
		Respawn();
	}

	public virtual void Respawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );
		SetupPhysicsFromCapsule( PhysicsMotionType.Keyframed, CollisionCapsule );
		Tags.Add( "Actor" );

		EnableAllCollisions = true;
		EnableDrawing = true;
	}

	[GameEvent.Tick.Server]
	public virtual void Think()
	{
		ComputeNavigation();
		ComputeMotion();
		ComputeAnimations();
	}

	/// <summary>
	/// Returns the nearest cell in any direction.
	/// </summary>
	/// <param name="directionNormal"></param>
	/// <param name="numOfCellsInDirection"></param>
	/// <returns></returns>
	public GridAStar.Cell GetCellInDirection( Vector3 directionNormal, int numOfCellsInDirection = 1 )
	{
		return CurrentGrid.GetNearestCell( Position + (directionNormal * CurrentGrid.CellSize) * numOfCellsInDirection );
	}

}

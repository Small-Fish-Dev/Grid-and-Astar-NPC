namespace GridAStar;

/// <summary>
/// Like a Vector2, but with integers instead.
/// </summary>
public struct IntVector2 : IEquatable<IntVector2>
{
	public int x { get; set; }
	public int y { get; set; }

	public int this[int index]
	{
		get
		{
			int result = index switch
			{
				0 => x,
				1 => y,
				_ => throw new IndexOutOfRangeException(),
			};

			return result;
		}
		set
		{
			switch ( index )
			{
				case 0:
					x = value;
					break;
				case 1:
					y = value;
					break;
			}
		}
	}

	public IntVector2(int x, int y )
	{
		this.x = x;
		this.y = y;
	}

	public IntVector2 WithX( int x ) => new IntVector2( x, this.y );
	public IntVector2 WithY( int y ) => new IntVector2( this.x, y );
	public Vector2 ToVector2() => new Vector2( x, y );
	public float DistanceSquared( IntVector2 other ) => ToVector2().DistanceSquared( other.ToVector2());
	public override string ToString() => $"{x},{y}";
	public override bool Equals( object obj ) => obj is IntVector2 other && Equals( other );
	public bool Equals( IntVector2 other ) => x == other.x && y == other.y;
	public override int GetHashCode() => HashCode.Combine( x, y );

	public static bool operator ==( IntVector2 left, IntVector2 right ) => left.Equals( right );
	public static bool operator !=( IntVector2 left, IntVector2 right ) => !(left == right);
	public static IntVector2 operator +( IntVector2 a, IntVector2 b ) => new IntVector2( a.x + b.x, a.y + b.y );
	public static IntVector2 operator -( IntVector2 a, IntVector2 b ) => new IntVector2( a.x - b.x, a.y - b.y );
}

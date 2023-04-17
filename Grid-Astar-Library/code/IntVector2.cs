namespace GridAStar;

/// <summary>
/// Like a Vector2, but with integers instead.
/// </summary>
public struct IntVector2
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

	public IntVector2 WithX( int x )
	{
		return new IntVector2( x, this.y );
	}

	public IntVector2 WithY( int y )
	{
		return new IntVector2( this.x, y );
	}

	public Vector2 ToVector2()
	{
		return new Vector2( x, y );
	}

	public float DistanceSquared( IntVector2 other )
	{
		return ToVector2().DistanceSquared( other.ToVector2());
	}

	public override string ToString()
	{
		return $"{x},{y}";
	}
}

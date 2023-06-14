namespace GridAStar;

/// <summary>
/// Like a Vector3, but with integers instead.
/// </summary>
public struct IntVector3
{
	public int x { get; set; }
	public int y { get; set; }
	public int z { get; set; }

	public int this[int index]
	{
		get
		{
			int result = index switch
			{
				0 => x,
				1 => y,
				2 => z,
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
				case 2:
					z = value;
					break;
			}
		}
	}

	public IntVector3( int x, int y, int z )
	{
		this.x = x;
		this.y = y;
		this.z = z;
	}

	public IntVector3( int x, int y ) : this( x, y, 0 )
	{
	}

	public IntVector3 WithX( int x )
	{
		return new IntVector3( x, this.y, this.z );
	}

	public IntVector3 WithY( int y )
	{
		return new IntVector3( this.x, y, this.z );
	}

	public IntVector3 WithZ( int z )
	{
		return new IntVector3( this.x, this.y, z );
	}

	public override string ToString()
	{
		return $"{x},{y},{z}";
	}
}

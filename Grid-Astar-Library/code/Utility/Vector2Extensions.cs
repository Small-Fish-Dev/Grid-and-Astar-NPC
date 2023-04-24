namespace GridAStar;

public static partial class Vector2Extensions
{

	/// <summary>
	/// Round every value of the Vector2 into an integer after diving it by divideBy. Ex. Vector2(30.5f, 20.1f).ToIntVector2(24) = IntVector2(1,1)
	/// </summary>
	/// <param name="self"></param>
	/// <param name="divideBy"></param>
	/// <returns></returns>
	public static IntVector2 ToIntVector2( this Vector2 self, float divideBy = 1 )
	{
		return new IntVector2( (int)Math.Round( self.x / divideBy ), (int)Math.Round( self.y / divideBy ) );
	}

	/// <summary>
	/// Round every value of the Vector3 into an integer after diving it by divideBy. Ex. Vector2(30.5f, 20.1f, 50.9f).ToIntVector2(24) = IntVector2(1,1)
	/// </summary>
	/// <param name="self"></param>
	/// <param name="divideBy"></param>
	/// <returns></returns>
	public static IntVector2 ToIntVector2( this Vector3 self, float divideBy = 1f )
	{
		return new IntVector2( (int)Math.Round( self.x / divideBy ), (int)Math.Round( self.y / divideBy ) );
	}

}

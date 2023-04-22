namespace GridAStar;

public static partial class Vector3Extensions
{

	/// <summary>
	/// Round every value of the Vector3 into an integer after diving it by divideBy. Ex. Vector3(30.5f, 20.1f, 50.9f).ToIntVector3(24) = IntVector3(1,1,2)
	/// </summary>
	/// <param name="self"></param>
	/// <param name="divideBy"></param>
	/// <returns></returns>
	public static IntVector3 ToIntVector3( this Vector3 self, int divideBy = 1 )
	{
		return new IntVector3( (int)Math.Round( self.x / divideBy ), (int)Math.Round( self.y / divideBy ), (int)Math.Round( self.z / divideBy ) );
	}

}

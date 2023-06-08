namespace GridAStar;

public static class MathAStar
{
	/// <summary>
	/// Used in for loops to check values going inward to outward instead of incrementally 0 1 2 3 4 5 6 7 8 9 -> 0 1 -1 2 -2 3 -3 4 -4 5 -5
	/// </summary>
	/// <param name="input"></param>
	/// <returns></returns>
	public static int SpiralPattern( int input )
	{
		var halfInput = (int)Math.Ceiling( input / 2d );
		var alternatedInput = input % 2 == 0 ? -1 : 1;

		return halfInput * alternatedInput;
	}

}

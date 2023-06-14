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

	public static Vector3 Parabola3D( Vector3 horizontalVelocity, Vector3 verticalVelocity, Vector3 gravity, float time )
	{
		var horizontalPosition = horizontalVelocity * time;
		var verticalPosition = verticalVelocity * time;
		var gravityOffset = 0.5f * gravity * time * time;

		return horizontalPosition + verticalPosition + gravityOffset;
	}

	public static Vector2 Parabola( float horizontalSpeed, float verticalSpeed, float gravity, float time )
	{
		float horizontalPosition = horizontalSpeed * time;
		float verticalPosition = verticalSpeed * time;
		float gravityOffset = 0.5f * gravity * time * time;

		return new Vector2( horizontalPosition, verticalPosition + gravityOffset );
	}

	public static float ParabolaHeight( float horizontalPosition, float horizontalSpeed, float verticalSpeed, float gravity )
	{
		float time = horizontalPosition / horizontalSpeed;

		float verticalPosition = verticalSpeed * time;
		float gravityOffset = 0.5f * gravity * time * time;

		return verticalPosition + gravityOffset;
	}

	public static float ParabolaMaxHeight( float verticalVelocity, float gravity )
	{
		float timeToPeak = -verticalVelocity / gravity;
		float maxVerticalPosition = verticalVelocity * timeToPeak + 0.5f * gravity * timeToPeak * timeToPeak;

		return maxVerticalPosition;
	}
}

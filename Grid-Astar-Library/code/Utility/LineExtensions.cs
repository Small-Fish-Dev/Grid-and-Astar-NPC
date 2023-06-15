namespace GridAStar;

public static class LineExtensions
{
	public static Vector3 Direction( this Line line ) => (line.End - line.Start).Normal;
}

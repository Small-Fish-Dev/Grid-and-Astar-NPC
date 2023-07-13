namespace GridAStar;

public static partial class BBoxExtensions
{
	public static BBox GetRotatedBounds( this BBox bbox, Rotation rotation )
	{
		if ( rotation.Angles() == Angles.Zero )
			return bbox;

		List<float> posX = new();
		List<float> posY = new();
		List<float> posZ = new();

		foreach ( var corner in bbox.Corners )
		{
			var rotatedCorner = corner * rotation;
			posX.Add( rotatedCorner.x );
			posY.Add( rotatedCorner.y );
			posZ.Add( rotatedCorner.z );
		}

		var minX = posX.Min();
		var maxX = posX.Max();
		var minY = posY.Min();
		var maxY = posY.Max();
		var minZ = posZ.Min();
		var maxZ = posZ.Max();

		var rotatedBounds = new BBox( new Vector3( minX, minY, minZ ), new Vector3( maxX, maxY, maxZ ) );

		return rotatedBounds;
	}
	public static bool IsInsideSquishedRotatedCylinder( this BBox bbox, Vector3 position, Vector3 point, Rotation rotation )
	{
		var pointToCheck = point - position;
		if ( rotation.Angles() == Angles.Zero )
			pointToCheck = (point - position) * rotation.Inverse;

		var radiusX = (bbox.Maxs.x - bbox.Mins.x) / 2f;
		var radiusY = (bbox.Maxs.y - bbox.Mins.y) / 2f;

		var insideSphere = pointToCheck.x * pointToCheck.x / (radiusX * radiusX) + pointToCheck.y * pointToCheck.y / (radiusY * radiusY);

		return insideSphere <= 1f;
	}

	public static bool IsRotatedPointWithinBounds( this BBox bbox, Vector3 position, Vector3 point, Rotation rotation )
	{
		var pointToCheck = point - position;
		if ( rotation.Angles() == Angles.Zero )
			pointToCheck = (point - position) * rotation.Inverse;

		var biggerBbox = new BBox( bbox.Mins - 0.01f, bbox.Maxs + 0.01f ); // Add some clearance to allow grid-perfect terrain

		return biggerBbox.Contains( pointToCheck );
	}

}

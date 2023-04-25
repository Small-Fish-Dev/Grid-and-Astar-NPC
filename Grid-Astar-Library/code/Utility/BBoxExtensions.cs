﻿using System.Numerics;

namespace GridAStar;

public static partial class BBoxExtensions
{
	public static BBox GetRotatedBounds( this BBox bbox, Rotation rotation )
	{
		if ( rotation == new Rotation() ) // Return the same if the rotation is zero
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

	public static bool IsRotatedPointWithinBounds( this BBox bbox, Vector3 position, Vector3 point, Rotation rotation )
	{
		var rotatedPoint = ( point - position ) * rotation.Inverse;
		return bbox.Contains( rotatedPoint );
	}

}
using Editor;
using Sandbox;
using System;
using System.ComponentModel.DataAnnotations;

namespace GridAStar;

[HammerEntity, Solid, AutoApplyMaterial( "materials/no_grid.vmat" )]
[Display( Name = "Grid Blocker Box", GroupName = "Navigation", Description = "Define an box where all cells inside are occupied (Can be rotated)" )]
public partial class GridBlockerBox : ModelEntity
{
	[Net, Property, Description( "Expand the blocker before checking, this should be like your grid's width clearance" )]
	public float WidthClearance { get; set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;
	public BBox BoundsWithClearance => new BBox( CollisionBounds.Mins - WidthClearance, CollisionBounds.Maxs + WidthClearance );
	public BBox RotatedBounds => CollisionBounds.GetRotatedBounds( Rotation );
	public BBox WorldBounds => RotatedBounds.Translate( Position );
	public List<Grid> OverlappingGrids { get; set;} = new();
	public HashSet<Cell> OverlappingCells { get; set; } = new();
	public bool IsInsideBounds( Vector3 point ) => BoundsWithClearance.IsRotatedPointWithinBounds( Position, point, Rotation );

	public GridBlockerBox()
	{
	}

	public override void Spawn()
	{
		base.Spawn();

		EnableDrawing = false;

		FindAndApplyAll();
	}

	public void FindOverlappingGrids()
	{
		foreach ( var grid in Grid.Grids )
			if ( grid.Value.WorldBounds.Overlaps( WorldBounds ) )
				OverlappingGrids.Add( grid.Value );
	}

	public void ApplyOccupied( Grid grid )
	{
		foreach ( var cellStack in grid.Cells )
			foreach ( var cell in cellStack.Value )
				if ( IsInsideBounds( cell.Position ) )
				{
					OverlappingCells.Add( cell );
					cell.Occupied = true;
				}
	}

	[Grid.LoadedAll]
	public void FindAndApplyAll()
	{
		FindOverlappingGrids();

		foreach ( var grid in OverlappingGrids )
			ApplyOccupied( grid );
	}

}

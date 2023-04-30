using Editor;
using Sandbox;
using System;
using System.ComponentModel.DataAnnotations;

namespace GridAStar;

[HammerEntity, Solid, AutoApplyMaterial( "materials/no_grid.vmat" )]
[Display( Name = "Grid Blocker Box", GroupName = "Navigation", Description = "Define an box where all cells inside are occupied (Can be rotated)" )]
public partial class GridBlockerBox : ModelEntity
{
	[Net, Property, Description( "Which grid is affected" )]
	public string GridIdentifier { get; set; } = "main";
	[Net, Property, Description( "Expand the blocker before checking, this should be like your grid's width clearance" )]
	public float WidthClearance { get; set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;
	public Grid CurrentGrid { get; set; }
	public BBox BoundsWithClearance => new BBox( CollisionBounds.Mins - WidthClearance, CollisionBounds.Maxs + WidthClearance );
	public BBox RotatedBounds => CollisionBounds.GetRotatedBounds( Rotation );
	public BBox WorldBounds => RotatedBounds.Translate( Position );
	public HashSet<Cell> OverlappingCells { get; set; } = new();
	public bool IsInsideBounds( Vector3 point ) => BoundsWithClearance.IsRotatedPointWithinBounds( Position, point, Rotation );

	public GridBlockerBox() { }

	public GridBlockerBox( Grid grid ) : base()
	{
		CurrentGrid = grid;
		GridIdentifier = grid.Identifier;
	}

	public override void Spawn()
	{
		base.Spawn();

		EnableDrawing = false;

		SearchForGrid();
		Apply();
	}

	public void SearchForGrid()
	{
		if ( CurrentGrid == null || CurrentGrid.Identifier != GridIdentifier )
			if ( Grid.Grids.ContainsKey( GridIdentifier ) )
				CurrentGrid = Grid.Grids[GridIdentifier];
	}

	public void Apply()
	{
		if ( CurrentGrid == null ) return;
		
		foreach ( var cellStack in CurrentGrid.Cells )
		{
			foreach ( var cell in cellStack.Value )
			{
				if ( IsInsideBounds( cell.Position ) )
				{
					OverlappingCells.Add( cell );
					cell.Occupied = true;
				}
			}
		}
	}
	public void Update()
	{
		if ( CurrentGrid == null ) return;

		OverlappingCells.Clear();

		foreach ( var cellStack in CurrentGrid.Cells )
		{
			foreach ( var cell in cellStack.Value )
			{
				if ( IsInsideBounds( cell.Position ) )
				{
					OverlappingCells.Add( cell );
					cell.Occupied = true;
				}
			}
		}
	}

	[Grid.LoadedAll]
	public void ApplyAll()
	{
		SearchForGrid();
		Apply();
	}

}

using Editor;
using System;
using System.ComponentModel.DataAnnotations;

namespace GridAStar;

[HammerEntity, Solid, AutoApplyMaterial( "materials/no_grid.vmat" )]
[Display( Name = "Grid Blocker Box", GroupName = "Navigation", Description = "Define an box where all cells inside are occupied (Can be rotated)" )]
public partial class GridBlockerBox : ModelEntity
{
	[Net, Property, Description( "Expand the blocker before checking, this should be like your grid's width clearance" )]
	public float WidthClearance { get; set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;

	public GridBlockerBox()
	{
	}

	public override void Spawn()
	{
		base.Spawn();

		EnableDrawing = false;
	}

	[Grid.LoadedAll]
	public void CheckOverlap()
	{
		Log.Error( "LOADED ALL" );
	}

}

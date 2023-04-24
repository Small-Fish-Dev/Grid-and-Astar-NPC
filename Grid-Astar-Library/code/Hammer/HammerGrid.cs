using Editor;
using System.ComponentModel.DataAnnotations;

namespace GridAStar;

[HammerEntity, Solid, AutoApplyMaterial( "materials/grid.vmat" )]
[Display( Name = "3D Grid", GroupName = "Navigation", Description = "Define a grid to load on this map" )]
public partial class HammerGrid : ModelEntity
{

	[Net, Property, Description( "Name of the grid" )]
	public string Identifier { get; set; } = "main";
	[Net, Property, Description( "Maximum steepness of a surface to be considered walkable" )]
	public float StandableAngle { get; set; } = GridSettings.DEFAULT_STANDABLE_ANGLE;
	[Net, Property, Description( "Step size based off of your MoveHelper to climb 90° surfaces" )]
	public float StepSize { get; set; } = GridSettings.DEFAULT_STEP_SIZE;
	[Net, Property, Description( "Size of each cell in hammer units" )]
	public float CellSize { get; set; } = GridSettings.DEFAULT_CELL_SIZE;
	[Net, Property, Description( "Minimum height clearance for a surface to be walkable" )]
	public float HeightClearance { get; set; } = GridSettings.DEFAULT_HEIGHT_CLEARANCE;
	[Net, Property, Description( "Minimum width clearance for a surface to be walkable" )]
	public float WidthClearance { get; set; } = GridSettings.DEFAULT_WIDTH_CLEARANCE;
	[Net, Property, Description( "Ignore entities while creating the grid (Static props placed in hammer count as the world, otherwise they don't)" )]
	public bool WorldOnly { get; set; } = GridSettings.DEFAULT_WORLD_ONLY;

	public HammerGrid() 
	{
	}

	public override void Spawn()
	{
		base.Spawn();

		Log.Error( "FUCK" );
		DebugOverlay.Box( Position + CollisionBounds.Mins, Position + CollisionBounds.Maxs, Color.Red, 30f );
	}


}

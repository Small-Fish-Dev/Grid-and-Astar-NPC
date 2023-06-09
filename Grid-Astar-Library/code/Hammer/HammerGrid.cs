﻿using Editor;
using Sandbox;
using System;
using System.ComponentModel.DataAnnotations;

namespace GridAStar;

[HammerEntity, Solid, AutoApplyMaterial( "materials/grid.vmat" )]
[Display( Name = "3D Grid", GroupName = "Navigation", Description = "Define a grid to load on this map" )]
public partial class HammerGrid : ModelEntity
{

	[Net, Property, Description( "Name of the grid. Use the name 'main' to access using Grid.Main via code" )]
	public string Identifier { get; set; } = "main";
	[Net, Property, Description( "True = Follow the world's axis, False = Follow's the grid's rotation for how cells are rotated and generated" )]
	public bool AxisAligned { get; set; } = true;
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
	[Net, Property, Description( "For grid-perfect terrain, this disables Steps so make sure to use ramps instead" )]
	public bool GridPerfect { get; set; } = GridSettings.DEFAULT_GRID_PERFECT;
	[Net, Property, Description( "Ignore entities while creating the grid (Static props placed in hammer count as the world, otherwise they don't)" )]
	public bool WorldOnly { get; set; } = GridSettings.DEFAULT_WORLD_ONLY;
	public string SaveIdentifier => $"{Game.Server.MapIdent}-{Identifier}";

	public HammerGrid()
	{
	}

	public override void Spawn()
	{
		base.Spawn();

		EnableDrawing = false;
	}

	public bool PropertiesEqual( GridLoadProperties properties ) => properties.GetHashCode() == GridHashCode();

	public async Task<Grid> CreateFromSettings() => await GridAStar.Grid.Create( Position, CollisionBounds, Rotation, Identifier, AxisAligned, StandableAngle, StepSize, CellSize, HeightClearance, WidthClearance, GridPerfect, WorldOnly );

	public static void LoadAllGrids()
	{
		GameTask.RunInThreadAsync( async () =>
		{
			var allGrids = Entity.All.OfType<HammerGrid>().ToList();

			foreach ( var grid in allGrids )
			{
				if ( GridAStar.Grid.Exists( grid.SaveIdentifier ) )
				{
					var properties = await GridAStar.Grid.LoadProperties( grid.SaveIdentifier );

					if ( grid.PropertiesEqual( properties ) )
					{
						if ( await GridAStar.Grid.Load( grid.SaveIdentifier ) == null ) // If everything is valid, which it should be, it will load the map in
							await grid.CreateFromSettings(); // Else it will create a new one
					}
					else
					{
						Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {grid.Identifier} properties don't match. Creating new one..." );
						Grid.DeleteSave( grid.Identifier );
						await grid.CreateFromSettings();
					}

				}
				else
					await grid.CreateFromSettings();
			}

			Event.Run( Grid.LoadedAll );
		} );
	}

	[GameEvent.Entity.PostSpawn]
	public static void LoadServerGrids()
	{
		LoadAllGrids();
	}

	[ClientRpc]
	public static void LoadClientGrids()
	{
		LoadAllGrids();
	}

	[GameEvent.Server.ClientJoined]
	public static void LoadClientGridsOnConnect( ClientJoinedEvent joinedEvent )
	{
		LoadClientGrids( To.Single( joinedEvent.Client ) );
	}

	public int GridHashCode() // Overriding GetHashCode returns an error because of Position.GetHashCode()
	{
		var identifierHashCode = Identifier.GetHashCode();
		var positionHashCode = Position.GetHashCode();
		var boundsHashCode = CollisionBounds.GetHashCode();
		var rotationHashCode = Rotation.GetHashCode();
		var axisAlignedHashCode = AxisAligned.GetHashCode();
		var standableAngleHashCode = StandableAngle.GetHashCode();
		var stepSizeHashCode = StepSize.GetHashCode();
		var cellSizeHashCode = CellSize.GetHashCode();
		var heightClearanceHashCode = HeightClearance.GetHashCode();
		var widthClearanceHashCode = WidthClearance.GetHashCode();
		var gridPerfectHashCode = GridPerfect.GetHashCode();
		var worldOnlyHashCode = WorldOnly.GetHashCode();

		var hashCodeFirst = HashCode.Combine( identifierHashCode, positionHashCode, boundsHashCode, rotationHashCode, axisAlignedHashCode, standableAngleHashCode, stepSizeHashCode, cellSizeHashCode );
		var hashCodeSecond = HashCode.Combine( cellSizeHashCode, heightClearanceHashCode, widthClearanceHashCode, gridPerfectHashCode, worldOnlyHashCode );

		return HashCode.Combine( hashCodeFirst, hashCodeSecond );
	}
}

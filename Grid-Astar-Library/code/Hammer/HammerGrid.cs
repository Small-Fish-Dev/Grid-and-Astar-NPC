using Editor;
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
	[Net, Property, Description( "How high up you can drop down" )]
	public float MaxDropHeight { get; set; } = GridSettings.DEFAULT_DROP_HEIGHT;
	[Net, Property, Description( "Cells will be generated in the shape of a squished circle instead of rectangle" )]
	public bool CylinderShaped { get; set; } = false;
	[Net, Property, Description( "Tags needed to be generated on, separated by commas, spaces are removed" )]
	public string TagsToInclude { get; set; } = "solid";
	[Net, Property, Description( "Tags to exclude when generating, separated by commas, spaces are removed" )]
	public string TagsToExclude { get; set; } = "player";
	public string SaveIdentifier => $"{Game.Server.MapIdent}-{Identifier}";

	public HammerGrid()
	{
	}

	public override void Spawn()
	{
		base.Spawn();

		EnableDrawing = false;
	}

	public bool PropertiesEqual( GridBuilder properties ) => properties.GetHashCode() == GridHashCode();

	public GridBuilder GetProperties()
	{
		var settings = new GridBuilder( Identifier );

		settings.WithBounds( Position, CollisionBounds, Rotation )
			.WithAxisAligned( AxisAligned )
			.WithStandableAngle( StandableAngle )
			.WithStepSize( StepSize )
			.WithCellSize( CellSize )
			.WithHeightClearance( HeightClearance )
			.WithWidthClearance( WidthClearance )
			.WithGridPerfect( GridPerfect )
			.WithWorldOnly( WorldOnly )
			.WithMaxDropHeight( MaxDropHeight )
			.WithCylinderShaped( CylinderShaped )
			.WithTags( TagsToInclude.Replace( " ", string.Empty ).Split( "," ) )
			.WithoutTags( TagsToExclude.Replace( " ", string.Empty ).Split( "," ) );

		return settings;
	}

	public async Task<Grid> CreateFromSettings() => await GetProperties().Create();

	public static void LoadAllGrids()
	{
		GameTask.RunInThreadAsync( async () =>
		{
			var allGrids = Entity.All.OfType<HammerGrid>().ToList();

			foreach ( var grid in allGrids )
			{
				var existsOnLocal = GridAStar.Grid.Exists( grid.SaveIdentifier );
				var existsOnMounted = GridAStar.Grid.ExistsMounted( grid.SaveIdentifier );
				if ( existsOnLocal || existsOnMounted )
				{
					var properties = await GridAStar.Grid.LoadProperties( grid.SaveIdentifier );

					if ( grid.PropertiesEqual( properties ) )
					{
						var loadedGrid = await GridAStar.Grid.Load( grid.SaveIdentifier );

						if ( loadedGrid == null ) // If everything is valid, which it should be, it will load the map in
						{
							var newGrid = await grid.CreateFromSettings(); // Else it will create a new one
							await newGrid.Save();
						}
						else
							if ( existsOnMounted && !existsOnLocal )
							await loadedGrid.Save();
					}
					else
					{
						Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {grid.Identifier} properties don't match. Creating new one..." );
						Grid.DeleteSave( grid.Identifier );
						var newGrid = await grid.CreateFromSettings();
						await newGrid.Save();
					}

				}
				else
				{
					var newGrid = await grid.CreateFromSettings();
					await newGrid.Save();
				}
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

	// Overriding GetHashCode returns an error because of Position.GetHashCode()
	public int GridHashCode() => GetProperties().GetHashCode();
}

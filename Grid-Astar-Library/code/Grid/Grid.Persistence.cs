using System.IO;
using System.IO.Compression;
namespace GridAStar;

public static partial class GridSettings
{
	public const string DEFAULT_SAVE_PATH = "./grid-%identifier%.dat";   // Where the grid is saved (%identifier% will be the grid's identifier)
}

public struct GridLoadProperties
{
	public string Identifier { get; set; } = "";
	public Vector3 Position { get; set; }
	public BBox Bounds { get; set; }
	public Rotation Rotation { get; set; }
	public bool AxisAligned { get; set; }
	public float StandableAngle { get; set; }
	public float StepSize { get; set; }
	public float CellSize { get; set; }
	public float HeightClearance { get; set; }
	public float WidthClearance { get; set; }
	public bool GridPerfect { get; set; }
	public bool WorldOnly { get; set; }

	public GridLoadProperties( string identifier )
	{
		Identifier = identifier;
	}

	public override int GetHashCode()
	{
		var identifierHashCode = Identifier.GetHashCode();
		var positionHashCode = Position.GetHashCode();
		var boundsHashCode = Bounds.GetHashCode();
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

public partial class Grid
{
	public string SavePath => GetSavePath( SaveIdentifier );
	public static string GetSavePath( string identifier = "main" ) => GridSettings.DEFAULT_SAVE_PATH.Replace( "%identifier%", identifier );
	public static bool Exists( string identifier = "main" ) => FileSystem.Data.FileExists( Grid.GetSavePath( identifier ) );
	public bool Exists() => FileSystem.Data.FileExists( SavePath );

	/// <summary>
	/// Return a struct containing the grid's data, without loading in the grid
	/// </summary>
	/// <param name="identifier"></param>
	/// <returns></returns>
	public async static Task<GridLoadProperties> LoadProperties( string identifier = "main" )
	{
		if ( !Grid.Exists( identifier ) )
			return new GridLoadProperties();

		using ( var fileStream = FileSystem.Data.OpenRead( GetSavePath( identifier ) ) )
		using ( var gzipStream = new GZipStream( fileStream, CompressionMode.Decompress ) )
		using ( var reader = new BinaryReader( gzipStream ) )
		{
			var loadedGrid = new GridLoadProperties( reader.ReadString() );

			await GameTask.RunInThreadAsync( () =>
			{
				loadedGrid.Position = reader.ReadVector3();
				loadedGrid.Bounds = new BBox( reader.ReadVector3(), reader.ReadVector3() );
				loadedGrid.Rotation = reader.ReadRotation();
				loadedGrid.AxisAligned = reader.ReadBoolean();
				loadedGrid.StandableAngle = reader.ReadSingle();
				loadedGrid.StepSize = reader.ReadSingle();
				loadedGrid.CellSize = reader.ReadSingle();
				loadedGrid.HeightClearance = reader.ReadSingle();
				loadedGrid.WidthClearance = reader.ReadSingle();
				loadedGrid.GridPerfect = reader.ReadBoolean();
				loadedGrid.WorldOnly = reader.ReadBoolean();
			} );

			return loadedGrid;
		}
	}

	/// <summary>
	/// Load the grid from the save file
	/// </summary>
	/// <param name="identifier"></param>
	/// <returns></returns>
	public async static Task<Grid> Load( string identifier = "main" )
	{
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Loading grid {identifier}" );

		if ( !Grid.Exists( identifier ) )
		{
			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} not found" );
			return null;
		}

		var loadWatch = new Stopwatch();
		loadWatch.Start();

		using ( var fileStream = FileSystem.Data.OpenRead( GetSavePath( identifier ) ) )
		using ( var gzipStream = new GZipStream( fileStream, CompressionMode.Decompress ) )
		using ( var reader = new BinaryReader( gzipStream ) )
		{
			try
			{
				var currentGrid = new Grid( reader.ReadString() );

				await GameTask.RunInThreadAsync( () =>
				{
					currentGrid.Position = reader.ReadVector3();
					currentGrid.Bounds = new BBox( reader.ReadVector3(), reader.ReadVector3() );
					currentGrid.Rotation = reader.ReadRotation();
					currentGrid.AxisAligned = reader.ReadBoolean();
					currentGrid.StandableAngle = reader.ReadSingle();
					currentGrid.StepSize = reader.ReadSingle();
					currentGrid.CellSize = reader.ReadSingle();
					currentGrid.HeightClearance = reader.ReadSingle();
					currentGrid.WidthClearance = reader.ReadSingle();
					currentGrid.GridPerfect = reader.ReadBoolean();
					currentGrid.WorldOnly = reader.ReadBoolean();

					var cellsToRead = reader.ReadInt32();
					Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} {cellsToRead} cells found in Grid {identifier}" );

					for ( int i = 0; i < cellsToRead; i++ )
					{
						var cellPosition = reader.ReadVector3();
						var cellVertices = new float[4];
						for ( int vertex = 0; vertex < 4; vertex++ )
							cellVertices[vertex] = reader.ReadSingle();
						var tagsAmount = reader.ReadInt32();
						var tags = new List<string>();
						for ( int tag = 0; tag < tagsAmount; tag++ )
							tags.Add( reader.ReadString() );

						var cell = new Cell( currentGrid, cellPosition, cellVertices, tags );
						currentGrid.AddCell( cell );
					}
				} );

				await currentGrid.Initialize( false );

				loadWatch.Stop();
				Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} loaded in {loadWatch.ElapsedMilliseconds}ms" );

				return currentGrid;
			}
			catch ( Exception error )
			{
				loadWatch.Stop();
				Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {identifier} failed to load ({error})" );
				return null;
			}
		}
	}

	/// <summary>
	/// Save the grid on a file
	/// </summary>
	/// <returns></returns>
	public async Task<bool> Save()
	{
		Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Saving grid {Identifier}" );

		var saveWatch = new Stopwatch();
		saveWatch.Start();

		try
		{
			byte[] compressedData;

			using ( var memoryStream = new MemoryStream() )
			using ( var writer = new BinaryWriter( memoryStream ) )
			{
				writer.Write( Identifier );
				writer.Write( Position );
				writer.Write( Bounds.Mins );
				writer.Write( Bounds.Maxs );
				writer.Write( Rotation );
				writer.Write( AxisAligned );
				writer.Write( StandableAngle );
				writer.Write( StepSize );
				writer.Write( CellSize );
				writer.Write( HeightClearance );
				writer.Write( WidthClearance );
				writer.Write( GridPerfect );
				writer.Write( WorldOnly );

				await GameTask.RunInThreadAsync( () =>
				{
					var cellsCount = 0;

					foreach ( var cellStack in Cells )
						foreach ( var cell in cellStack.Value )
							cellsCount++;

					writer.Write( cellsCount );

					foreach ( var cellStack in Cells )
					{
						foreach ( var cell in cellStack.Value )
						{
							writer.Write( cell.Position );
							foreach ( var vertex in cell.Vertices )
								writer.Write( vertex );
							writer.Write( cell.Tags.All.Count() );
							foreach ( var tag in cell.Tags.All )
								writer.Write( tag );
						}
					}
				} );

				compressedData = CompressData( memoryStream.ToArray() );
			}

			using ( var fileStream = FileSystem.Data.OpenWrite( SavePath, System.IO.FileMode.OpenOrCreate ) )
			{
				await fileStream.WriteAsync( compressedData, 0, compressedData.Length );
			}

			saveWatch.Stop();
			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {Identifier} saved in {saveWatch.ElapsedMilliseconds}ms" );

			return true;
		}
		catch ( Exception error )
		{
			saveWatch.Stop();
			Log.Info( $"{(Game.IsServer ? "[Server]" : "[Client]")} Grid {Identifier} failed to save ({error})" );
			return false;
		}
	}

	private byte[] CompressData( byte[] data )
	{
		using ( var compressedStream = new MemoryStream() )
		{
			using ( var gzipStream = new GZipStream( compressedStream, CompressionMode.Compress ) )
			{
				gzipStream.Write( data, 0, data.Length );
			}

			return compressedStream.ToArray();
		}
	}

	/// <summary>
	/// Delete the saved file
	/// </summary>
	/// <param name="identifier"></param>
	/// <returns></returns>
	public static bool DeleteSave( string identifier = "main" )
	{
		if ( Exists( identifier ) )
		{
			FileSystem.Data.DeleteFile( GetSavePath( identifier ) );
			return true;
		}

		return false;
	}

	/// <summary>
	/// Delete the saved file
	/// </summary>
	/// <returns></returns>
	public bool DeleteSave() => Grid.DeleteSave( Identifier );
}



﻿using System.IO;
using System.IO.Compression;
namespace GridAStar;

public static partial class GridSettings
{
	public const string DEFAULT_SAVE_PATH = "./grid-%identifier%.dat";   // Where the grid is saved (%identifier% will be the grid's saveidentifier)
	public const string DEFAULT_MOUNTED_LOAD_FOLDER = "Grids";          // Which folder should it look for any grid that comes with the game (Just give the name and nothing else)
}

public partial class Grid
{
	public string SavePath => GetSavePath( SaveIdentifier );
	public static string GetSavePath( string identifier = "main" ) => GridSettings.DEFAULT_SAVE_PATH.Replace( "%identifier%", identifier );
	public static string GetMountedPath( string identifier = "main" ) => $"./{GridSettings.DEFAULT_MOUNTED_LOAD_FOLDER}{GetSavePath( identifier ).Substring( 1 )}";
	public static bool Exists( string identifier = "main" ) => FileSystem.Data.FileExists( Grid.GetSavePath( identifier ) );
	public static bool ExistsMounted( string identifier = "main" ) => FileSystem.Mounted.FileExists( Grid.GetMountedPath( identifier ) );
	public bool Exists() => FileSystem.Data.FileExists( SavePath );

	internal async static Task<GridBuilder> internalLoadProperties( BinaryReader reader )
	{
		var loadedGrid = new GridBuilder( reader.ReadString() );

		await GameTask.RunInThreadAsync( () =>
		{
			loadedGrid.WithBounds( reader.ReadVector3(), new BBox( reader.ReadVector3(), reader.ReadVector3() ), reader.ReadRotation() )
			.WithAxisAligned( reader.ReadBoolean() )
			.WithStandableAngle( reader.ReadSingle() )
			.WithStepSize( reader.ReadSingle() )
			.WithCellSize( reader.ReadSingle() )
			.WithHeightClearance( reader.ReadSingle() )
			.WithWidthClearance( reader.ReadSingle() )
			.WithGridPerfect( reader.ReadBoolean() )
			.WithStaticOnly( reader.ReadBoolean() )
			.WithMaxDropHeight( reader.ReadSingle() )
			.WithCylinderShaped( reader.ReadBoolean() );

			var tagsToIncludeCount = reader.ReadInt32();

			for ( int i = 0; i < tagsToIncludeCount; i++ )
				loadedGrid.WithTags( reader.ReadString() );

			var tagsToExcludeCount = reader.ReadInt32();

			for ( int i = 0; i < tagsToExcludeCount; i++ )
				loadedGrid.WithoutTags( reader.ReadString() );
		} );

		return loadedGrid;
	}

	/// <summary>
	/// Return a struct containing the grid's data, without loading in the grid
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="printInfo"></param>
	/// <returns></returns>
	public async static Task<GridBuilder> LoadProperties( string identifier = "main", bool printInfo = true )
	{
		var isOnMounted = false;
		if ( !Grid.Exists( identifier ) )
		{
			if ( Grid.ExistsMounted( identifier ) )
				isOnMounted = true;
			else
				return new GridBuilder();
		}

		try
		{

			using ( var fileStream = isOnMounted ? FileSystem.Mounted.OpenRead( GetMountedPath( identifier ) ) : FileSystem.Data.OpenRead( GetSavePath( identifier ) ) )
			{
				Stream dataStream = fileStream;

				// Check if the data is compressed
				if ( IsCompressed( dataStream ) )
				{
					dataStream = new GZipStream( fileStream, CompressionMode.Decompress );
					if ( printInfo )
						Grid.Print( identifier, "is compressed, decompressing..." );
				}

				using ( var reader = new BinaryReader( dataStream ) )
					return internalLoadProperties( reader ).Result;
			}
		}
		catch ( Exception error )
		{
			if ( printInfo )
				Grid.Print( identifier, $"Failed to load properties ({error})" );
			return new GridBuilder();
		}
	}

	/// <summary>
	/// Load the grid from the save file
	/// </summary>
	/// <param name="identifier"></param>
	/// <param name="printInfo"></param>
	/// <returns></returns>
	public async static Task<Grid> Load( string identifier = "main", bool printInfo = true )
	{
		if ( printInfo )
			Grid.Print( identifier, "Loading grid" );

		var isOnMounted = false;

		if ( !Grid.Exists( identifier ) )
		{
			if ( printInfo )
				Grid.Print( identifier, "Not found on local" );
			if ( Grid.ExistsMounted( identifier ) )
			{
				if ( printInfo )
					Grid.Print( identifier, "Found on mounted" );
				isOnMounted = true;
			}
			else
			{
				if ( printInfo )
					Grid.Print( identifier, "Not found on mounted" );
				return null;
			}
		}

		var loadWatch = new Stopwatch();
		loadWatch.Start();

		using ( var fileStream = isOnMounted ? FileSystem.Mounted.OpenRead( GetMountedPath( identifier ) ) : FileSystem.Data.OpenRead( GetSavePath( identifier ) ) )
		{
			Stream dataStream = fileStream;

			// Check if the data is compressed
			if ( IsCompressed( dataStream ) )
			{
				dataStream = new GZipStream( fileStream, CompressionMode.Decompress );
				if ( printInfo )
					Grid.Print( identifier, "Is compressed, decompressing..." );
			}

			using ( var reader = new BinaryReader( dataStream ) )
			{
				try
				{
					Grid currentGrid = null;

					await GameTask.RunInThreadAsync( async () =>
					{
						var loadedGrid = await internalLoadProperties( reader );

						currentGrid = new Grid( loadedGrid );

						var cellsToRead = reader.ReadInt32();
						if ( printInfo )
							Grid.Print( identifier, $"{cellsToRead} cells found" );

						var connectedDictionary = new Dictionary<Cell, List<(Vector3, string)>>();

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

							var connectedAmount = reader.ReadInt32();
							if ( connectedAmount > 0 )
							{
								connectedDictionary.Add( cell, new List<(Vector3, string)>() );

								for ( int connected = 0; connected < connectedAmount; connected++ )
								{
									var connectedPosition = reader.ReadVector3();
									var connectedTag = reader.ReadString();
									connectedDictionary[cell].Add( (connectedPosition, connectedTag) );
								}
							}
						}

						foreach ( var parentCell in connectedDictionary )
						{
							foreach ( var cellToConnect in parentCell.Value )
							{
								var cellPosition = cellToConnect.Item1;
								var cellTag = cellToConnect.Item2;
								parentCell.Key.AddConnection( currentGrid.GetCell( cellPosition ), cellTag );
							}
						}

						currentGrid.Initialize();
					} );

					loadWatch.Stop();
					if ( printInfo )
						Grid.Print( identifier, $"Loaded in {loadWatch.ElapsedMilliseconds}ms" );

					return currentGrid;
				}
				catch ( Exception error )
				{
					loadWatch.Stop();
					if ( printInfo )
						Grid.Print( identifier, $"Failed to load ({error})" );
					return null;
				}
			}
		}
	}

	private static bool IsCompressed( Stream stream )
	{
		// Read the first two bytes to check if they match the gzip file signature
		byte[] signature = new byte[2];
		stream.Read( signature, 0, 2 );
		stream.Position = 0;

		return signature[0] == 0x1F && signature[1] == 0x8B;
	}

	/// <summary>
	/// Save the grid on a file
	/// </summary>
	/// <returns></returns>
	public async Task<bool> Save( bool compress = false, bool printInfo = true )
	{
		if ( printInfo )
			Print( "Saving grid" );

		var saveWatch = new Stopwatch();
		saveWatch.Start();

		try
		{
			byte[] data;

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
				writer.Write( StaticOnly );
				writer.Write( MaxDropHeight );
				writer.Write( CylinderShaped );

				var tagsToIncludeCount = 0;

				foreach ( var tag in Settings.TagsToInclude )
					tagsToIncludeCount++;

				writer.Write( tagsToIncludeCount );

				foreach ( var tag in Settings.TagsToInclude )
					writer.Write( tag );

				var tagsToExcludeCount = 0;

				foreach ( var tag in Settings.TagsToExclude )
					tagsToExcludeCount++;

				writer.Write( tagsToExcludeCount );

				foreach ( var tag in Settings.TagsToExclude )
					writer.Write( tag );

				await GameTask.RunInThreadAsync( () =>
				{
					var cellsCount = 0;

					foreach ( var cellStack in CellStacks )
						foreach ( var cell in cellStack.Value )
							cellsCount++;

					writer.Write( cellsCount );

					foreach ( var cellStack in CellStacks )
					{
						foreach ( var cell in cellStack.Value )
						{
							writer.Write( cell.Position );
							foreach ( var vertex in cell.Vertices )
								writer.Write( vertex );
							writer.Write( cell.Tags.All.Count() );
							foreach ( var tag in cell.Tags.All )
								writer.Write( tag );
							writer.Write( cell.CellConnections.Count() );
							foreach ( var cellConnection in cell.CellConnections )
							{
								writer.Write( cellConnection.Current.Position );
								writer.Write( cellConnection.MovementTag );
							}
						}
					}
				} );

				data = memoryStream.ToArray();
			}

			if ( compress )
				data = CompressData( data );

			using ( var fileStream = FileSystem.Data.OpenWrite( SavePath, System.IO.FileMode.OpenOrCreate ) )
				await fileStream.WriteAsync( data, 0, data.Length );

			saveWatch.Stop();
			if ( printInfo )
				Print( $"Saved in {saveWatch.ElapsedMilliseconds}ms" );

			return true;
		}
		catch ( Exception error )
		{
			saveWatch.Stop();
			if ( printInfo )
				Print( $"Failed to save ({error})" );
			return false;
		}
	}

	private byte[] CompressData( byte[] data )
	{
		using ( var compressedStream = new MemoryStream() )
		{
			using ( var gzipStream = new GZipStream( compressedStream, CompressionMode.Compress ) )
				gzipStream.Write( data, 0, data.Length );

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



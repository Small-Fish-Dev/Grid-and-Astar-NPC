using Sandbox;
using System.Data.Common;
using System.IO;
using System.Runtime.CompilerServices;

namespace GridAStar;

public static partial class GridSettings
{
	public const string DEFAULT_SAVE_PATH = "./grid_%identifier%.dat";   // Where the grid is saved (%identifier% will be the grid's identifier)
}

public partial class Grid
{
	public string SavePath => GetSavePath( Identifier );
	public static string GetSavePath( string identifier = "main" ) => GridSettings.DEFAULT_SAVE_PATH.Replace( "%identifier%", identifier );
	public static bool Exists( string identifier = "main" ) => FileSystem.Data.FileExists( Grid.GetSavePath( identifier ) );
	public bool Exists() => FileSystem.Data.FileExists( SavePath );

	/// <summary>
	/// Load the grid from the save file
	/// </summary>
	/// <param name="identifier"></param>
	/// <returns></returns>
	public async static Task<Grid> Load( string identifier = "main" )
	{
		if ( !Grid.Exists( identifier ) )
			return null;

		using var stream = FileSystem.Data.OpenRead( GetSavePath( identifier ) );
		using var reader = new BinaryReader( stream );

		var currentGrid = new Grid( reader.ReadString() );
		currentGrid.Bounds = new BBox( reader.ReadVector3(), reader.ReadVector3() );
		currentGrid.StandableAngle = reader.ReadSingle();
		currentGrid.StepSize = reader.ReadSingle();
		currentGrid.CellSize = reader.ReadSingle();
		currentGrid.HeightClearance = reader.ReadSingle();

		await GameTask.RunInThreadAsync( () =>
		{
			var cellsToRead = reader.ReadInt32();

			for ( int i = 0; i < cellsToRead; i++ )
			{
				var cellPosition = reader.ReadVector3();
				var cellVertices = new float[4];
				for ( int vertex = 0; vertex < 4; vertex++ )
					cellVertices[vertex] = reader.ReadSingle();

				var cell = new Cell( currentGrid, cellPosition, cellVertices );
				currentGrid.AddCell( cell );
			}
		} );

		await currentGrid.Initialize( false );

		stream.Close();
		reader.Close();

		return currentGrid;
	}

	/// <summary>
	/// Save the grid on a file
	/// </summary>
	/// <returns></returns>
	public async Task<bool> Save()
	{
		using var stream = FileSystem.Data.OpenWrite( SavePath, System.IO.FileMode.OpenOrCreate );
		using var writer = new BinaryWriter( stream );

		writer.Write( Identifier );
		writer.Write( Bounds.Mins );
		writer.Write( Bounds.Maxs );
		writer.Write( StandableAngle );
		writer.Write( StepSize );
		writer.Write( CellSize );
		writer.Write( HeightClearance );

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
				}
			}
		} );

		stream.Close();
		writer.Close();

		return true;

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



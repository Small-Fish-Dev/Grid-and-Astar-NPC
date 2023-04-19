using Sandbox;
using System.IO;
using System.Runtime.CompilerServices;

namespace GridAStar;

public static partial class GridSettings
{
	public const string DEFAULT_SAVE_PATH = "./grid_%identifier%.dat";   // Where the grid is saved (%identifier% will be the grid's identifier)
}

public partial class Grid
{

	public async static Task<Grid> Load( string identifier = "main" )
	{
		Stopwatch loadingWatch = new Stopwatch();
		loadingWatch.Start();

		var filePath = GridSettings.DEFAULT_SAVE_PATH.Replace( "%identifier%", identifier );

		if ( !FileSystem.Data.FileExists( filePath ) )
		{
			loadingWatch.Stop();
			return null;
		}

		using var stream = FileSystem.Data.OpenRead( filePath );
		using var reader = new BinaryReader( stream );

		var currentGrid = new Grid( reader.ReadString() );
		currentGrid.Bounds = new BBox( reader.ReadVector3(), reader.ReadVector3() );
		currentGrid.StandableAngle = reader.ReadSingle();
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

			loadingWatch.Stop();
			Log.Info( $"Loading completed in {loadingWatch.ElapsedMilliseconds}ms" );

			if ( Grids.ContainsKey( identifier ) )
				Grids[identifier] = currentGrid;
			else
				Grids.Add( identifier, currentGrid );
		} );

		return currentGrid;
	}

	public bool Save()
	{
		Stopwatch savingWatch = new Stopwatch();
		savingWatch.Start();

		var filePath = GridSettings.DEFAULT_SAVE_PATH.Replace( "%identifier%", Identifier );

		using var stream = FileSystem.Data.OpenWrite( filePath, System.IO.FileMode.OpenOrCreate );
		using var writer = new BinaryWriter( stream );

		writer.Write( Identifier );
		writer.Write( Bounds.Mins );
		writer.Write( Bounds.Maxs );
		writer.Write( StandableAngle );
		writer.Write( CellSize );
		writer.Write( HeightClearance );

		var cellsCount = 0;

		foreach( var cellStack in Cells )
			foreach ( var cell in cellStack.Value )
				cellsCount++;

		writer.Write( cellsCount );

		foreach ( var cellStack in Cells )
		{
			foreach( var cell in cellStack.Value )
			{
				writer.Write( cell.Position );
				foreach ( var vertex in cell.Vertices )
					writer.Write( vertex );
			}
		}

		savingWatch.Stop();
		Log.Info( $"Saving completed in {savingWatch.ElapsedMilliseconds}ms" );

		return true;

	}

}



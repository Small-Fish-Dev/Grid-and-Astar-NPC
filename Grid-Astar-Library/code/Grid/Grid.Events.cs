namespace GridAStar;

public partial class Grid
{
	public const string LoadedAll = "grid.loadedall";

	public class LoadedAllAttribute : EventAttribute
	{
		public LoadedAllAttribute() : base( LoadedAll ) { }
	}
}

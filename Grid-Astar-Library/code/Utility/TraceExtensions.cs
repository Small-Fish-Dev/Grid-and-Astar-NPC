namespace GridAStar;

public static partial class TraceExtensions
{
	public static SceneTrace WithGridSettings( this SceneTrace self, Grid grid )
	{
		self = self.WithAllTags( grid.TagsToInclude )
			.WithoutTags( grid.TagsToExclude );

		return self;
	}
}

